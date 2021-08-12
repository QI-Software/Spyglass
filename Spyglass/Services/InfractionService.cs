using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Serilog.Core;
using Spyglass.Database;
using Spyglass.Database.Moderation;
using Spyglass.Utilities;

namespace Spyglass.Services
{
    public class InfractionService
    {
        private readonly ConfigurationService _config;
        private readonly DiscordClient _client;
        private readonly DiscordLogService _discordLog;
        private readonly Logger _log;

        public InfractionService(ConfigurationService config, DiscordClient client, DiscordLogService discordLog, Logger log)
        {
            _config = config;
            _client = client;
            _discordLog = discordLog;
            _log = log;

            _client.GuildBanAdded += OnGuildMemberBanned;
            _client.GuildBanRemoved += OnGuildMemberUnbanned;
            _client.GuildMemberUpdated += OnGuildMemberUpdated;
        }
        
        private async Task OnGuildMemberUpdated(DiscordClient sender, GuildMemberUpdateEventArgs e)
        {
            var config = _config.GetConfig();
            if (e.Guild.Id != config.MainGuildId)
            {
                return;
            }

            var self = await e.Guild.GetMemberAsync(_client.CurrentUser.Id);
            if (!self.Permissions.HasPermission(Permissions.ViewAuditLog))
            {
                return;
            }

            var mutedRole = e.Guild.GetRole(config.MutedRoleId);
            if (mutedRole == null)
            {
                return;
            }

            // Check if the muted role was added.
            if (e.RolesBefore.All(r => r.Id != mutedRole.Id) && e.RolesAfter.Any(r => r.Id == mutedRole.Id))
            {
                var entries = await e.Guild.GetAuditLogsAsync(5, action_type: AuditLogActionType.MemberRoleUpdate);
                var entry = entries
                    .Cast<DiscordAuditLogMemberUpdateEntry>()
                    .FirstOrDefault(t => t.Target.Id == e.Member.Id);
                
                if (entry != null && !entry.UserResponsible.IsBot)
                {
                    _ = AddInfractionToUserAsync(e.Member, entry.UserResponsible, InfractionType.Mute);
                }
            }
            
            // Check if the muted role was removed.
            if (e.RolesBefore.Any(r => r.Id == mutedRole.Id) && e.RolesAfter.All(r => r.Id != mutedRole.Id))
            {
                var entries = await e.Guild.GetAuditLogsAsync(5, action_type: AuditLogActionType.MemberRoleUpdate);
                var entry = entries
                    .Cast<DiscordAuditLogMemberUpdateEntry>()
                    .FirstOrDefault(t => t.Target.Id == e.Member.Id);
                
                if (entry != null && !entry.UserResponsible.IsBot)
                {
                    _ = AddInfractionToUserAsync(e.Member, entry.UserResponsible, InfractionType.Unmute);
                }
            }
        }

        private async Task OnGuildMemberBanned(DiscordClient sender, GuildBanAddEventArgs e)
        {
            if (e.Guild.Id != _config.GetConfig().MainGuildId)
            {
                return;
            }

            var self = await e.Guild.GetMemberAsync(_client.CurrentUser.Id);
            if (!self.Permissions.HasPermission(Permissions.ViewAuditLog))
            {
                return;
            }

            var logs = await e.Guild.GetAuditLogsAsync(10, action_type: AuditLogActionType.Ban);
            var entry = logs.Cast<DiscordAuditLogBanEntry>()
                .OrderByDescending(l => l.CreationTimestamp)
                .FirstOrDefault(l => l.Target.Id == e.Member.Id);

            if (entry != null)
            {
                if (!entry.UserResponsible.IsBot)
                {
                    await AddInfractionToUserAsync(e.Member, entry.UserResponsible, InfractionType.Ban, entry.Reason);
                }
            }
        }
        
        private async Task OnGuildMemberUnbanned(DiscordClient sender, GuildBanRemoveEventArgs e)
        {
            if (e.Guild.Id != _config.GetConfig().MainGuildId)
            {
                return;
            }

            var self = await e.Guild.GetMemberAsync(_client.CurrentUser.Id);
            if (!self.Permissions.HasPermission(Permissions.ViewAuditLog))
            {
                return;
            }

            var logs = await e.Guild.GetAuditLogsAsync(10, action_type: AuditLogActionType.Unban);
            var entry = logs.Cast<DiscordAuditLogBanEntry>()
                .OrderByDescending(l => l.CreationTimestamp)
                .FirstOrDefault(l => l.Target.Id == e.Member.Id);

            if (entry != null)
            {
                if (!entry.UserResponsible.IsBot)
                {
                    await AddInfractionToUserAsync(e.Member, entry.UserResponsible, InfractionType.Unban, entry.Reason);
                }
            }
        }

        /// <summary>
        /// Check if a user has an infraction.
        /// </summary>
        /// <param name="user"> The user to check against. </param>
        /// <returns> True if the user has one or more infractions. </returns>
        public QueryResult<bool> HasInfraction(DiscordUser user) => HasInfraction(user.Id);

        /// <summary>
        /// Check if a user has an infraction.
        /// </summary>
        /// <param name="userId"> The user to check against. </param>
        /// <returns> True if the user has one or more infractions. </returns>
        public QueryResult<bool> HasInfraction(ulong userId)
        {
            SpyglassContext dbContext = null;

            try
            {
                dbContext = new SpyglassContext();

                return QueryResult<bool>.FromSuccess(result: dbContext.Infractions.Any(i => i.UserId == userId));
            }
            catch (Exception e)
            {
                _log.Error(e, "Infractions: Failed to retrieve infractions from database.");
                return QueryResult<bool>.FromError(e.Message);
            }
            finally
            {
                dbContext?.Dispose();
            }
        }
        
        /// <summary>
        /// Get all of the infractions stored inside the bot's database.
        /// </summary>
        /// <returns> A list of all known infractions. </returns>
        public QueryResult<List<Infraction>> GetAllInfractions()
        {
            SpyglassContext dbContext = null;

            try
            {
                dbContext = new SpyglassContext();

                return QueryResult<List<Infraction>>.FromSuccess(result: dbContext.Infractions.ToList());
            }
            catch (Exception e)
            {
                _log.Error(e, "Infractions: Failed to retrieve all infractions from database.");
                return QueryResult<List<Infraction>>.FromError(e.Message);
            }
            finally
            {
                dbContext?.Dispose();
            }
        }

        /// <summary>
        /// Get the infractions committed by a specific user.
        /// </summary>
        /// <param name="user"> The DiscordUser to search for in the database. </param>
        /// <returns> The user's infractions if any. </returns>
        public QueryResult<List<Infraction>> GetUserInfractions(DiscordUser user) => GetUserInfractions(user.Id);
        
        /// <summary>
        /// Get the infractions committed by a specific user.
        /// </summary>
        /// <param name="id"> The Id of the user to search for. </param>
        /// <returns> The user's infractions if any. </returns>
        public QueryResult<List<Infraction>> GetUserInfractions(ulong id)
        {
            SpyglassContext dbContext = null;

            try
            {
                dbContext = new SpyglassContext();

                return QueryResult<List<Infraction>>.FromSuccess(result: 
                    dbContext.Infractions.AsQueryable()
                    .Where(i => i.UserId == id)
                    .ToList());
            }
            catch (Exception e)
            {
                _log.Error(e, $"Infractions: Failed to retrieve infractions for user {id}.");
                return QueryResult<List<Infraction>>.FromError(e.Message);
            }
            finally
            {
                dbContext?.Dispose();
            }
        }

        /// <summary>
        /// Retrieves the infraction with the given id.
        /// </summary>
        /// <param name="id"> The id to query. </param>
        /// <returns> The infraction if it exists. </returns>
        public QueryResult<Infraction> GetInfractionFromId(long id)
        {
            SpyglassContext dbContext = null;

            try
            {
                dbContext = new SpyglassContext();

                var infraction = dbContext.Infractions.FirstOrDefault(i => i.Id == id);

                if (infraction == null)
                {
                    return QueryResult<Infraction>.FromError($"Could not find any infraction with id #{id}.");
                }
                else
                {
                    return QueryResult<Infraction>.FromSuccess(result: infraction);
                }
            }
            catch (Exception e)
            {
                _log.Error(e, $"An error has occured while retrieving infraction #{id}.");
                return QueryResult<Infraction>.FromError(e.Message);
            }
            finally
            {
                dbContext?.Dispose();
            }
        }

        /// <summary>
        /// Add an infraction to a user.
        /// </summary>
        /// <param name="user"> The user to which we should add the infraction. </param>
        /// <param name="staff"> The staff member who applied the infraction. </param>
        /// <param name="type"> The type of the infraction to add. </param>
        /// <param name="reason"> The reason for the infraction. </param>
        /// <returns> The infraction that was added to the user. </returns>
        public async Task<QueryResult<Infraction>> AddInfractionToUserAsync(DiscordUser user, DiscordUser staff, InfractionType type, string reason = null)
        {
            try
            {
                Assert.IsNotNull(user, $"{nameof(user)} is null.");
                Assert.IsNotNull(staff, $"{nameof(staff)} is null.");
            }
            catch (AssertionException e)
            {
                _log.Error(e, "Infractions: an assertion has failed while adding an infraction to a user.");
                return QueryResult<Infraction>.FromError(e.Message);
            }

            var infraction = new Infraction
            {
                Type = type,
                Timestamp = DateTimeOffset.Now,
                StaffId = staff.Id,
                UserId = user.Id,
                Reason = reason,
                StaffName = $"{staff.Username}#{staff.Discriminator}",
                UserName = $"{user.Username}#{user.Discriminator}"
            };

            SpyglassContext dbContext = null;
            
            try
            {
                // Create the db context.
                dbContext = new SpyglassContext();
                
                if (string.IsNullOrWhiteSpace(infraction.Reason))
                {
                    var nextId = dbContext.Infractions
                        .OrderByDescending(i => i.Id)
                        .FirstOrDefault()?.Id + 1 ?? 0;
                    infraction.Reason = $"*Responsible moderator please do /infractions update {nextId}*";
                }
                
                // Add the infraction.
                dbContext.Infractions.Add(infraction);
                
                // Save it to the database.
                await dbContext.SaveChangesAsync();

                _ = _discordLog.LogInfractionAsync(infraction);

                return QueryResult<Infraction>.FromSuccess("Successfully added infraction to user.", infraction);
            }
            catch (Exception e)
            {
                _log.Error(e, "Infractions: an error has occured while adding an infraction to a user.");
                return QueryResult<Infraction>.FromError($"Failed to add infraction to user.");
            }
            finally
            {
                // Dispose of the context.
                dbContext?.Dispose();
            }
        }

        /// <summary>
        /// Add an infraction to a user.
        /// </summary>
        /// <param name="userId"> The user id to which we should add the infraction. </param>
        /// <param name="staff"> The staff member who applied the infraction. </param>
        /// <param name="type"> The type of the infraction to add. </param>
        /// <param name="reason"> The reason for the infraction. </param>
        /// <param name="userName"> The username of the user to add the infraction to, if any is available. </param>
        /// <returns> True on success. </returns>
        public async Task<QueryResult<Infraction>> AddInfractionToUserAsync(ulong userId, DiscordUser staff, InfractionType type, string reason = null, string userName = null)
        {
            try
            {
                Assert.IsNotNull(staff, $"{nameof(staff)} is null.");
            }
            catch (AssertionException e)
            {
                _log.Error(e, "Infractions: an assertion has failed while adding an infraction to a user.");
                return QueryResult<Infraction>.FromError(e.Message);
            }

            if (string.IsNullOrEmpty(userName))
            {
                var query = GetUserInfractions(userId);
                if (query.Successful)
                {
                    userName = query.Result.Select(i => i.UserName).FirstOrDefault(u => !string.IsNullOrEmpty(u));
                }
            }
            
            var infraction = new Infraction
            {
                Type = type,
                Timestamp = DateTimeOffset.Now,
                StaffId = staff.Id,
                UserId = userId,
                Reason = reason,
                StaffName = $"{staff.Username}#{staff.Discriminator}",
                UserName = userName,
            };

            SpyglassContext dbContext = null;
            
            try
            {
                // Create the db context.
                dbContext = new SpyglassContext();

                if (string.IsNullOrWhiteSpace(infraction.Reason))
                {
                    var nextId = dbContext.Infractions
                        .OrderByDescending(i => i.Id)
                        .FirstOrDefault()?.Id + 1 ?? 0;
                    infraction.Reason = $"*Responsible moderator please do /infractions update {nextId}*";
                }
                
                // Add the infraction.
                await dbContext.Infractions.AddAsync(infraction);
                // Save it to the database.
                await dbContext.SaveChangesAsync();
                
                _ = _discordLog.LogInfractionAsync(infraction);

                return QueryResult<Infraction>.FromSuccess("Successfully added infraction to user.", infraction);
            }
            catch (Exception e)
            {
                _log.Error(e, "Infractions: an error has occured while adding an infraction to a user.");
                return QueryResult<Infraction>.FromError($"Failed to add infraction to user.");
            }
            finally
            {
                // Dispose of the context.
                dbContext?.Dispose();
            }
        }

        public async Task<QueryResult> RemoveInfractionAsync(long infractionId)
        {
            SpyglassContext dbContext = null;

            try
            {
                dbContext = new SpyglassContext();

                // Remove it from the database.
                var infraction = dbContext.Infractions.FirstOrDefault(i => i.Id == infractionId);

                if (infraction != null)
                {
                    dbContext.Remove(infraction);

                    // Commit the changes.
                    await dbContext.SaveChangesAsync();
                
                    return QueryResult.FromSuccess($"Removed infraction #{infractionId}");
                }
                
                return QueryResult.FromError($"Could not find any infraction with id #{infractionId}.");
            }
            catch (Exception e)
            {
                _log.Error(e, $"Failed to remove infraction #{infractionId}");
                return QueryResult.FromError($"Failed to remove infraction #{infractionId}");
            }
            finally
            {
                dbContext?.Dispose();
            }
        }

        public async Task<QueryResult> UpdateInfractionReasonAsync(long infractionId, string newReason)
        {
            if (string.IsNullOrEmpty(newReason))
            {
                return QueryResult.FromError("Please provide a valid reason.");
            }

            SpyglassContext dbContext = null;

            try
            {
                dbContext = new SpyglassContext();
                var infraction = dbContext.Infractions.FirstOrDefault(i => i.Id == infractionId);

                if (infraction == null)
                {
                    return QueryResult.FromError($"Could not find any infraction with id #{infractionId}.");
                }

                infraction.Reason = newReason;
                infraction.LastUpdatedAt = DateTimeOffset.Now;
                dbContext.Infractions.Update(infraction);
                await dbContext.SaveChangesAsync();
                
                return QueryResult.FromSuccess($"Successfully updated infraction #{infractionId}.");
            }
            catch (Exception e)
            {
                _log.Error(e, $"Infractions: failed to update infraction #{infractionId}.");
                return QueryResult.FromError($"Failed to update reason for infraction #{infractionId}: {e.Message}");
            }
            finally
            {
                dbContext?.Dispose();
            }
        }
    }
}