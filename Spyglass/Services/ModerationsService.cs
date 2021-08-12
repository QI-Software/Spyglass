using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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
    public class ModerationsService
    {
        public bool IsStarted { get; private set; }
        public bool IsConnected { get; private set; }
        
        private CancellationTokenSource _cancellationSource;
        private readonly ConfigurationService _config;
        private readonly DiscordClient _client;
        private readonly InfractionService _infractions;
        private readonly Logger _log;
        
        private IReadOnlyList<InfractionType> ValidTypes = new List<InfractionType>
        {
            InfractionType.Ban,
            InfractionType.Mute
        };
        
        public ModerationsService( DiscordClient client, ConfigurationService config, Logger log, InfractionService infractions)
        {
            _client = client;
            _config = config;
            _log = log;
            _infractions = infractions;

            _client.SocketClosed += (_, args) =>
            {
                IsConnected = false;
                return Task.CompletedTask;;
            };
            
            _client.SocketOpened += (_, args) =>
            {
                IsConnected = true;
                return Task.CompletedTask;
            };

            _client.GuildMemberAdded += OnGuildMemberAdded;
        }

        public void Start()
        {
            if (IsStarted)
            {
                return;
            }

            IsStarted = true;
            _cancellationSource = new CancellationTokenSource();
            
            Task.Run(async () =>
            {
                await TickModerationsAsync();
            });
            
            _log.Information("Moderations: Started ticking ongoing moderations.");
        }

        public void Stop()
        {
            if (!IsStarted)
            {
                return;
            }

            IsStarted = false;
            _cancellationSource.Cancel();
            
            _log.Information("Moderations: Sent cancellation request to ticker.");
        }

        public async Task<QueryResult> AddModerationAsync(Infraction infraction, DateTimeOffset endTime)
        {
            Assert.IsNotNull(infraction, $"Cannot add moderation with null infraction.");
            Assert.IsTrue(ValidTypes.Contains(infraction.Type), $"Infraction type '{infraction.Type}' is invalid for ongoing moderations.");

            SpyglassContext dbContext = null;

            try
            {
                dbContext = new SpyglassContext();
                var moderation = new OngoingModeration(infraction.UserId, endTime, infraction.Type, infraction.Id);
                dbContext.OngoingModerations.Add(moderation);

                await dbContext.SaveChangesAsync();
                return QueryResult.FromSuccess(
                    $"Successfully added ongoing moderation [{infraction.Type}] for user {infraction.UserId}.");
            }
            catch (Exception e)
            {
                _log.Error(e, $"Moderations: failed to add moderation.");
                return QueryResult.FromError($"Failed to add ongoing moderation: {e.Message}");
            }
            finally
            {
                dbContext?.Dispose();
            }
        }

        public async Task<QueryResult> RemoveModerationAsync(long caseId)
        {
            SpyglassContext dbContext = null;

            try
            {
                dbContext = new SpyglassContext();

                var moderation = dbContext.OngoingModerations.FirstOrDefault(m => m.LinkedInfractionId == caseId);
                if (moderation == null)
                {
                    return QueryResult.FromError($"Cannot find any ongoing moderation with linked case #{caseId}.");
                }

                dbContext.OngoingModerations.Remove(moderation);
                await dbContext.SaveChangesAsync();

                return QueryResult.FromSuccess($"Successfully deleted moderation linked to case #{caseId}.");
            }
            catch (Exception e)
            {
                _log.Error(e, $"Moderations: failed to remove moderation with case #{caseId}.");
                return QueryResult.FromError($"Failed to remove moderation linked to case #{caseId}.");
            }
            finally
            {
                dbContext?.Dispose();
            }
        }
        
        public QueryResult<List<OngoingModeration>> GetModerations()
        {
            SpyglassContext dbContext = null;
            
            try
            {
                dbContext = new SpyglassContext();
                return QueryResult<List<OngoingModeration>>.FromSuccess("Successfully retrieved ongoing moderations.",
                    dbContext.OngoingModerations.ToList());
            }
            catch (Exception e)
            {
                _log.Warning(e, "Moderations: failed to retrieve moderations.");
                return QueryResult<List<OngoingModeration>>.FromError($"Failed to retrieve ongoing moderations: {e.Message}");
            }
            finally
            {
                dbContext?.Dispose();
            }
        }

        public async Task TickModerationsAsync()
        {
            while (!_cancellationSource.IsCancellationRequested)
            {
                await Task.Delay(30 * 1000);

                if (!IsConnected)
                {
                    _log.Warning("Moderations: not connected to Discord, skipping tick.");
                    continue;
                }
                
                _log.Debug("Moderations: Ticking.");
                
                SpyglassContext dbContext = null;
                
                try
                {
                    dbContext = new SpyglassContext();
                    var finishedModerations = new List<OngoingModeration>();

                    foreach (var moderation in dbContext.OngoingModerations)
                    {
                        if (moderation.EndTime <= DateTimeOffset.Now)
                        {
                            finishedModerations.Add(moderation);
                        }
                    }

                    // Modifies the list to only keep handled moderations.
                    await HandleFinishedModerationsAsync(finishedModerations);

                    if (finishedModerations.Any())
                    {
                        dbContext.OngoingModerations.RemoveRange(finishedModerations);
                        await dbContext.SaveChangesAsync();
                    }
                }
                catch (Exception e)
                {
                    _log.Error(e, "Moderations: failed to tick moderations.");
                }
                finally
                {
                    dbContext?.Dispose();
                }
            }
            
            _log.Information("Moderations: Cancellation request received, stopped.");
        }

        public async Task HandleFinishedModerationsAsync(List<OngoingModeration> moderations)
        {
            if (!moderations.Any())
            {
                return;
            }
            
            var mainGuild = await DiscordUtils.TryGetGuildAsync(_client, _config.GetConfig().MainGuildId);
            if (mainGuild == null)
            {
                _log.Error("Moderations: failed to retrieve main server, ongoing moderations will not be checked.");
                return;
            }
            
            var bans = moderations.Any(m => m.Type == InfractionType.Ban)
                ? await mainGuild.GetBansAsync()
                : new List<DiscordBan>();

            for (int i = moderations.Count - 1; i >= 0; i--)
            {
                var moderation = moderations[i];
                var succeeded = false;
                var member = await DiscordUtils.TryGetMemberAsync(mainGuild, moderation.UserId);

                switch (moderation.Type)
                {
                    case InfractionType.Ban:
                        var ban = bans.FirstOrDefault(b => b.User.Id == moderation.UserId);
                        if (ban != null)
                        {
                            if (!(succeeded = await DiscordUtils.TryUnbanUserAsync(mainGuild, moderation.UserId)))
                            {
                                _log.Error(
                                    $"Moderations: failed to unban user {moderation.UserId}, retrying next tick.");
                                break;
                            }
                            _log.Information($"Moderations: unbanned user {moderation.UserId}.");
                            _ = _infractions.AddInfractionToUserAsync(ban.User, _client.CurrentUser, InfractionType.Unban,
                                "Automatic");
                        }
                        else
                        {
                            succeeded = true;
                            _log.Warning($"Moderations: user {moderation.UserId} was already unbanned, ignoring.");
                        }
                        break;
                    case InfractionType.Mute:
                        if (member != null && member.Roles.Any(r => r.Id == _config.GetConfig().MutedRoleId))
                        {
                            await member.RevokeRoleAsync(mainGuild.GetRole(_config.GetConfig().MutedRoleId),
                                "Ongoing moderation finished.");
                        }

                        _log.Information($"Moderations: unmuted user {moderation.UserId}.");
                        _ = _infractions.AddInfractionToUserAsync(moderation.UserId, _client.CurrentUser,
                            InfractionType.Unmute, "Automatic");
                        succeeded = true;
                        break;

                    default:
                        _log.Error($"Moderations: invalid moderation type '{moderation.Type}' in database, deleting.");
                        break;
                }

                if (!succeeded)
                {
                    moderations.RemoveAt(i);
                }
            }
        }

        private async Task OnGuildMemberAdded(DiscordClient client, GuildMemberAddEventArgs args)
        {
            if (args.Guild.Id != _config.GetConfig().MainGuildId)
            {
                return;
            }

            var query = GetModerations();
            if (!query.Successful)
            {
                _log.Error("Moderations: Could not access moderations on member join!");
                return;
            }

            var moderations = query.Result;
            if (moderations.Any(m => m.UserId == args.Member.Id && m.Type == InfractionType.Mute))
            {
                var mutedRole = args.Guild.GetRole(_config.GetConfig().MutedRoleId);
                await args.Member.GrantRoleAsync(mutedRole, "User joined but was supposed to be muted.");
                _log.Warning($"Moderations: User '{args.Member}' joined and was muted due to an ongoing infraction.");
            }
        }
    }
}