using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Serilog.Core;
using Spyglass.Database;
using Spyglass.Database.ReactionRoles;
using Spyglass.Providers;
using Spyglass.Utilities;

namespace Spyglass.Services
{
    public class ReactionRoleService
    {
        private readonly ConfigurationService _config;
        private readonly Logger _log;

        private List<ReactionRole> _cachedReactionRoles;

        public ReactionRoleService(ConfigurationService config, DiscordClient client, Logger log)
        {
            _config = config;
            _log = log;

            _cachedReactionRoles = new List<ReactionRole>();

            client.MessageReactionAdded += OnReactionAdded;
            client.MessageReactionRemoved += OnReactionRemoved;
        }

        public void Initialize()
        {
            SpyglassContext dbContext = null;

            try
            {
                _log.Information("ReactionRoles: Caching reaction roles.");
                dbContext = new SpyglassContext();
                _cachedReactionRoles = dbContext.ReactionRoles.ToList();
                _log.Information(string.Format(new PluralFormatProvider(), "ReactionRoles: Cached {0:reaction role;reaction roles}.", _cachedReactionRoles.Count));
            }
            catch (Exception e)
            {
                _log.Error(e, "ReactionRoles: Failed to cache reaction roles: " + e.Message);
            }
            finally
            {
                dbContext?.Dispose();
            }
        }

        public IReadOnlyList<ReactionRole> GetReactionRoles()
        {
            return _cachedReactionRoles;
        }

        public async Task<QueryResult> AddReactionRoleAsync(ReactionRole role)
        {
            if (role.ReactionId == null && string.IsNullOrWhiteSpace(role.ReactionName)
                || role.ReactionId != null && !string.IsNullOrWhiteSpace(role.ReactionName))
            {
                return QueryResult.FromError("Reaction roles must have either an ID or a name.");
            }

            if (_cachedReactionRoles.Any(r => r.Equals(role)))
            {
                return QueryResult.FromError("A reaction role with similar parameters already exists.");
            }

            if (_cachedReactionRoles.Any(r => r.MessageId == role.MessageId && r.RoleId == role.RoleId))
            {
                return QueryResult.FromError("A single reaction role cannot give multiple roles out.");
            }

            SpyglassContext dbContext = null;
            try
            {
                dbContext = new SpyglassContext();
                dbContext.ReactionRoles.Add(role);
                await dbContext.SaveChangesAsync();

                _cachedReactionRoles.Add(role);
                return QueryResult.FromSuccess("Successfully created a new reaction role.");
            }
            catch (Exception e)
            {
                _log.Error(e, $"ReactionRoles: Failed to add a new reaction role: {e.Message}");
                return QueryResult.FromError($"Failed to add a new reaction role: {e.Message}.");
            }
            finally
            {
                dbContext?.Dispose();
            }
        }

        public async Task<QueryResult> RemoveReactionRoleAsync(long id)
        {
            if (_cachedReactionRoles.All(r => r.Id != id))
            {
                return QueryResult.FromError($"There are no reaction roles with id {id}.");
            }

            SpyglassContext dbContext = null;
            try
            {
                dbContext = new SpyglassContext();

                var role = dbContext.ReactionRoles.FirstOrDefault(r => r.Id == id);
                if (role == null)
                {
                    return QueryResult.FromError($"There are no reaction roles with id {id}.");
                }
                
                
                dbContext.ReactionRoles.Remove(role);
                await dbContext.SaveChangesAsync();

                _cachedReactionRoles = dbContext.ReactionRoles.ToList();
                return QueryResult.FromSuccess($"Successfully removed reaction role {id}.");
            }
            catch (Exception e)
            {
                _log.Error(e, $"ReactionRoles: Failed to remove reaction role {id}: {e.Message}");
                return QueryResult.FromError($"Failed to remove reaction role {id}: {e.Message}.");
            }
            finally
            {
                dbContext?.Dispose();
            }
        }

        private async Task OnReactionAdded(DiscordClient sender, MessageReactionAddEventArgs e)
        {
            if (!_config.GetConfig().ReactionRolesEnabled)
            {
                return;
            }

            var reactionRole = _cachedReactionRoles.FirstOrDefault(r =>
            {
                return r.MessageId == e.Message.Id && 
                       (r.ReactionId != null && r.ReactionId.Value == e.Emoji.Id || r.ReactionName != null && e.Emoji.Name == r.ReactionName);
            });
            
            if (reactionRole == null)
            {
                return;
            }

            var role = e.Guild.GetRole(reactionRole.RoleId);
            if (role == null)
            {
                _log.Warning($"ReactionRoles: Reaction role {reactionRole.Id} is linked to an unknown role '{reactionRole.Id}'.");
                return;
            }

            var bot = e.Guild.GetMemberAsync(sender.CurrentUser.Id);
            var memberTask = e.Guild.GetMemberAsync(e.User.Id);
            await Task.WhenAll(bot, memberTask);

            if (memberTask.Result == null)
            {
                return;
            }

            var member = memberTask.Result;
            
            if (member.Roles.Any(r => r.Id == role.Id))
            {
                return;
            }

            if (!DiscordUtils.CanAddRoleToMember(role, memberTask.Result))
            {
                _log.Warning($"ReactionRoles: Cannot grant role '{role.Name}' to '{member.Username}#{member.Discriminator}': role is higher than my highest role.");
                return;
            }
            
            if (!bot.Result.Permissions.HasPermission(Permissions.ManageRoles))
            {
                _log.Warning("ReactionRoles: Cannot grant role to user: no permission.");
                return;
            }

            try
            {
                await member.GrantRoleAsync(role);
                _log.Information($"ReactionRoles: Granted role '{role.Name}' to '{member.Username}#{member.Discriminator}'");
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"ReactionRoles: Failed to grant role '{role.Name}' to '{member.Username}#{member.Discriminator}': {ex.Message}");
            }
        }
        
        
        private async Task OnReactionRemoved(DiscordClient sender, MessageReactionRemoveEventArgs e)
        {
            if (!_config.GetConfig().ReactionRolesEnabled)
            {
                return;
            }

            var reactionRole = _cachedReactionRoles.FirstOrDefault(r =>
            {
                return r.MessageId == e.Message.Id && 
                       (r.ReactionId != null && r.ReactionId.Value == e.Emoji.Id || r.ReactionName != null && e.Emoji.Name == r.ReactionName);
            });            
            
            if (reactionRole == null)
            {
                return;
            }

            var role = e.Guild.GetRole(reactionRole.RoleId);
            if (role == null)
            {
                _log.Warning($"ReactionRoles: Reaction role {reactionRole.Id} is linked to an unknown role '{reactionRole.Id}'.");
                return;
            }

            var bot = e.Guild.GetMemberAsync(sender.CurrentUser.Id);
            var memberTask = e.Guild.GetMemberAsync(e.User.Id);
            await Task.WhenAll(bot, memberTask);

            if (memberTask.Result == null)
            {
                return;
            }

            var member = memberTask.Result;
            
            if (member.Roles.All(r => r.Id != role.Id))
            {
                return;
            }

            if (!DiscordUtils.CanAddRoleToMember(role, memberTask.Result))
            {
                _log.Warning($"ReactionRoles: Cannot remove role '{role.Name}' from '{member.Username}#{member.Discriminator}': role is higher than my highest role.");
                return;
            }

            if (!bot.Result.Permissions.HasPermission(Permissions.ManageRoles))
            {
                _log.Warning("ReactionRoles: Cannot remove role from user: no permission.");
                return;
            }

            try
            {
                await member.RevokeRoleAsync(role);
                _log.Information($"ReactionRoles: Removed role '{role.Name}' from '{member.Username}#{member.Discriminator}'");
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"ReactionRoles: Failed to remove role '{role.Name}' from '{member.Username}#{member.Discriminator}': {ex.Message}");
            }
        }

    }
}