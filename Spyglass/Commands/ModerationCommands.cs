using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using Spyglass.Database.Moderation;
using Spyglass.Preconditions;
using Spyglass.Services;
using Spyglass.Utilities;

namespace Spyglass.Commands
{
    [SlashRequireGuild]
    [RequireConfiguredMainGuild]
    public class ModerationCommands : ApplicationCommandModule
    {
        private readonly EmbedService _embeds;
        private readonly ConfigurationService _config;
        private readonly DiscordClient _client;
        private readonly InfractionService _infractionService;
        private readonly ModerationsService _moderations;

        public ModerationCommands(EmbedService embeds, ConfigurationService config, DiscordClient client, InfractionService infractionService, ModerationsService moderations)
        {
            _embeds = embeds;
            _config = config;
            _client = client;
            _infractionService = infractionService;
            _moderations = moderations;
        }

        [SlashCommand("moderations", "Displays non-permanent infractions and their time left.")]
        [RequirePermissions(Permissions.ViewAuditLog)]
        public async Task ViewModerations(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            var query = _moderations.GetModerations();
            
            if (!query.Successful)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message(query.Message, DiscordColor.Red)));

                return;
            }
            
            var moderations = query.Result;
            if (moderations.Count == 0)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message("There are no ongoing moderations at this time.",
                    DiscordColor.Green)));
            }
            else
            {
                var embeds = await _embeds.OngoingModerationsAsync(moderations);
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbeds(embeds));
            }
        }

        [SlashCommand("note", "Attaches a new note to a user's infractions.")]
        [Preconditions.RequirePermissions(Permissions.ViewAuditLog)]
        public async Task AddNoteToMember(InteractionContext ctx,
            [Option("user", "The user to add a note to.")] DiscordUser user,
            [Option("note", "The note to attach to the user. Leave empty for an interactive prompt.")] string note = null)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            if (!string.IsNullOrWhiteSpace(note))
            {
                var result = await _infractionService.AddInfractionToUserAsync(user, ctx.User, InfractionType.Note, note);
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message(result.Message, 
                    result.Successful ? DiscordColor.Green : DiscordColor.Red)));
                
                return;
            }

            var promptResult = await DiscordUtils.PromptForInputAsync("Please enter the note to add (or 'cancel' to cancel):", ctx);
            if (promptResult != null)
            {
                var result = await _infractionService.AddInfractionToUserAsync(user, ctx.User, InfractionType.Note, promptResult);
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message(result.Message, 
                    result.Successful ? DiscordColor.Green : DiscordColor.Red)));
            }
        }
        
        [SlashCommand("warn", "Sends a warning to the user and adds it to their infractions.")]
        [RequirePermissions(Permissions.ViewAuditLog)]
        public async Task WarnMember(InteractionContext ctx,
            [Option("user", "The user to warn.")] DiscordUser user,
            [Option("warning", "The warning to attach and send to the user. Leave empty for an interactive prompt.")] string reason = null)
        {
            if (ctx.User == user)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder {IsEphemeral = true}.AddEmbed(_embeds.Message("You cannot target yourself!", DiscordColor.Red)));
            
                return;
            }

            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            reason ??= await DiscordUtils.PromptForInputAsync("Please enter the warning to send (or 'cancel' to cancel):", ctx, maxLength: 2000);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                var result = await _infractionService.AddInfractionToUserAsync(user, ctx.User, InfractionType.Warn, reason);

                if (!result.Successful)
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                        .AddEmbed(_embeds.Message($"Failed to add a warning to the user: {result.Message}.",
                            DiscordColor.Red)));

                    return;
                }
                
                var mainGuild = _client.Guilds[_config.GetConfig().MainGuildId];
                var member = await DiscordUtils.TryGetMemberAsync(mainGuild, user.Id); 
                DiscordMessage message = null;
                
                if (member != null)
                {
                    var builder = new DiscordMessageBuilder()
                        .AddEmbed(_embeds.Message($"You have been warned on **{mainGuild.Name}** for the above reason.", DiscordColor.Red))
                        .WithContent(reason);

                    message = await DiscordUtils.TryMessageUserAsync(member, builder);
                }

                if (message != null)
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                        .AddEmbed(_embeds.Message($"Successfully warned and notified {user.Username}#{user.Discriminator}.", DiscordColor.Green)));
                }
                else
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                        .AddEmbed(_embeds.Message($"Successfully added a warning to {user.Username}#{user.Discriminator}. However, I was unable to notify them.",
                            DiscordColor.Orange)));
                }
            }
        }
        
        [SlashCommand("kick", "Kicks a user from the main server and adds it to their infractions.")]
        [RequirePermissions(Permissions.KickMembers)]
        [RequireBotPermissions(Permissions.KickMembers)]
        public async Task KickMember(InteractionContext ctx,
            [Option("user", "The user to kick.")] DiscordUser user,
            [Option("reason", "The reason to attach and send to the user. Leave empty for an interactive prompt.")] string reason = null)
        {
            if (ctx.User == user)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder {IsEphemeral = true}.AddEmbed(_embeds.Message("You cannot target yourself!", DiscordColor.Red)));
            
                return;
            }
            
            if (ctx.User.Id == ctx.Client.CurrentUser.Id)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder {IsEphemeral = true}.AddEmbed(_embeds.Message("You cannot target me!", DiscordColor.Red)));
            
                return;
            }
            
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var mainGuild = _client.Guilds[_config.GetConfig().MainGuildId];
            var targetTask = DiscordUtils.TryGetMemberAsync(mainGuild, user.Id);
            var senderTask = DiscordUtils.TryGetMemberAsync(mainGuild, ctx.User.Id);
            var botTask = DiscordUtils.TryGetMemberAsync(mainGuild, ctx.Client.CurrentUser.Id);

            await Task.WhenAll(targetTask, senderTask, botTask);

            var target = targetTask.Result;
            var sender = senderTask.Result;
            var bot = botTask.Result;
            
            if (target == null)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("I am unable to kick the specified user as I could not find them on the main server.", DiscordColor.Red)));
                return;
            }
            
            if (sender == null)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("I am unable to kick the specified user as I could not find you on the main server (required to check for permissions).", DiscordColor.Red)));
                return;
            }
            
            if (!DiscordUtils.CheckMemberCanTargetOther(sender, target))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("I am unable to kick the specified user as you cannot target them.", DiscordColor.Red)));
                return;
            }

            if (!DiscordUtils.CheckMemberCanTargetOther(bot, target))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("I am unable to kick the specified user as I cannot target them.", DiscordColor.Red)));
                return;
            }

            reason ??= await DiscordUtils.PromptForInputAsync("Please enter the reason to send (or 'cancel' to cancel):", ctx);
            
            if (string.IsNullOrWhiteSpace(reason))
            {
                return;
            }
            
            var kickButton = new DiscordButtonComponent(ButtonStyle.Primary, "kickButton", "Kick Member");
            var cancelButton = new DiscordButtonComponent(ButtonStyle.Danger, "cancelButton", "Cancel");

            var confirmationEmbed = _embeds.Message($"You are about to kick {target.Username}#{target.Discriminator} for the above reason. Please confirm using the buttons below.",
                DiscordColor.Orange);
            
            var confirmationBuilder = new DiscordFollowupMessageBuilder()
                .WithContent(reason)
                .AddEmbed(confirmationEmbed)
                .AddComponents(kickButton, cancelButton);

            var msg = await ctx.FollowUpAsync(confirmationBuilder);
            var buttonResult = await msg.WaitForButtonAsync(ctx.User, TimeSpan.FromMinutes(5));

            await msg.ModifyAsync(new DiscordMessageBuilder().WithContent(reason).AddEmbed(confirmationEmbed));
            if (buttonResult.TimedOut)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message("Timed out.", DiscordColor.Red)));
                return;
            }

            if (buttonResult.Result.Id == cancelButton.CustomId)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message("Cancelled.", DiscordColor.Red)));
                return;
            }
            
            var result = await _infractionService.AddInfractionToUserAsync(user, ctx.User, InfractionType.Kick, reason);

            if (!result.Successful)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message($"Failed to add infraction to the user: {result.Message}.",
                        DiscordColor.Red)));

                return;
            }
                
            var builder = new DiscordMessageBuilder()
                .AddEmbed(_embeds.Message($"You have been kicked from **{mainGuild.Name}** for the above reason.", DiscordColor.Red))
                .WithContent(reason);

            var message = await DiscordUtils.TryMessageUserAsync(target, builder);
            await target.RemoveAsync();
            
            if (message != null)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message($"Successfully kicked and notified {user.Username}#{user.Discriminator}.", DiscordColor.Green)));
            }
            else
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message($"Successfully kicked {user.Username}#{user.Discriminator}. However, I was unable to notify them.",
                        DiscordColor.Orange)));
            }
        }
        
        [SlashCommand("ban", "Bans a user from the main server and adds it to their infractions.")]
        [Preconditions.RequirePermissions(Permissions.BanMembers)]
        [Preconditions.RequireBotPermissions(Permissions.BanMembers)]
        public async Task BanMember(InteractionContext ctx,
            [Option("user", "The user to ban.")] DiscordUser user,
            [Option("duration", "The duration of the ban (0 for permanent).")] string duration,
            [Option("reason", "The reason to attach and send to the user. Leave empty for an interactive prompt.")] string reason = null)
        {
            if (ctx.User == user)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder {IsEphemeral = true}.AddEmbed(_embeds.Message("You cannot target yourself!", DiscordColor.Red)));
                return;
            }
            
            if (ctx.User.Id == ctx.Client.CurrentUser.Id)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder {IsEphemeral = true}.AddEmbed(_embeds.Message("You cannot target me!", DiscordColor.Red)));
                return;
            }

            var durationSpan = await DiscordUtils.ConvertToTimespanAsync(ctx, duration);
            if (durationSpan == null)
            {
                return;
            }

            var endTime = DateTimeOffset.Now + durationSpan.Value;
            
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var mainGuild = _client.Guilds[_config.GetConfig().MainGuildId];
            var targetTask = DiscordUtils.TryGetMemberAsync(mainGuild, user.Id);
            var senderTask = DiscordUtils.TryGetMemberAsync(mainGuild, ctx.User.Id);
            var botTask = DiscordUtils.TryGetMemberAsync(mainGuild, ctx.Client.CurrentUser.Id);

            await Task.WhenAll(targetTask, senderTask, botTask);

            var target = targetTask.Result;
            var sender = senderTask.Result;
            var bot = botTask.Result;

            if (sender == null)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("I am unable to kick the specified user as I could not find you on the main server (required to check for permissions).", DiscordColor.Red)));
                return;
            }
            
            if (target != null && !DiscordUtils.CheckMemberCanTargetOther(sender, target))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("I am unable to ban the specified user as you cannot target them.", DiscordColor.Red)));
                return;
            }
            
            if (target != null && !DiscordUtils.CheckMemberCanTargetOther(bot, target))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("I am unable to ban the specified user as I cannot target them.", DiscordColor.Red)));
                return;
            }

            reason ??= await DiscordUtils.PromptForInputAsync("Please enter the reason to send (or 'cancel' to cancel):", ctx);
            
            if (string.IsNullOrWhiteSpace(reason))
            {
                return;
            }
            
            var kickButton = new DiscordButtonComponent(ButtonStyle.Primary, "banButton", "Ban Member");
            var cancelButton = new DiscordButtonComponent(ButtonStyle.Danger, "cancelButton", "Cancel");

            var confirmationMessage = durationSpan.Value > TimeSpan.Zero
                ? $"You are about to ban {user.Username}#{user.Discriminator} for the above reason.\nThe ban will expire <t:{endTime.ToUnixTimeSeconds()}:R>.\nPlease confirm using the buttons below."
                : $"You are about to permanently ban {user.Username}#{user.Discriminator} for the above reason.\nPlease confirm using the buttons below.";
            
            var confirmationEmbed = _embeds.Message(confirmationMessage, DiscordColor.Orange);
            
            var confirmationBuilder = new DiscordFollowupMessageBuilder()
                .WithContent(reason)
                .AddEmbed(confirmationEmbed)
                .AddComponents(kickButton, cancelButton);

            var msg = await ctx.FollowUpAsync(confirmationBuilder);
            var buttonResult = await msg.WaitForButtonAsync(ctx.User, TimeSpan.FromMinutes(5));

            await msg.ModifyAsync(new DiscordMessageBuilder().WithContent(reason).AddEmbed(confirmationEmbed));
            if (buttonResult.TimedOut)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message("Timed out.", DiscordColor.Red)));
                return;
            }

            if (buttonResult.Result.Id == cancelButton.CustomId)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message("Cancelled.", DiscordColor.Red)));
                return;
            }
            
            var infractionQuery = await _infractionService.AddInfractionToUserAsync(user, ctx.User, InfractionType.Ban, reason);

            if (!infractionQuery.Successful)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message($"Failed to add infraction to the user: {infractionQuery.Message}.",
                        DiscordColor.Red)));

                return;
            }

            if (durationSpan.Value > TimeSpan.Zero)
            {
                _ = _moderations.AddModerationAsync(infractionQuery.Result, endTime);
            }
            
            var displayedMessage = durationSpan.Value > TimeSpan.Zero
                ? $"You have been banned from **{mainGuild.Name}** for the above reason.\nThis ban will expire <t:{endTime.ToUnixTimeSeconds()}:R>."
                : $"You have been permanently banned from **{mainGuild.Name}** for the above reason.";
                
            var builder = new DiscordMessageBuilder()
                .AddEmbed(_embeds.Message(displayedMessage, DiscordColor.Red))
                .WithContent(reason);

            var message = await DiscordUtils.TryMessageUserAsync(target, builder);

            if (target != null)
            {
                await target.BanAsync();
            }
            else
            {
                await mainGuild.BanMemberAsync(user.Id);
            }

            if (target != null)
            {
                if (message != null)
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                        .AddEmbed(_embeds.Message($"Successfully banned and notified {user.Username}#{user.Discriminator}.", DiscordColor.Green)));
                }
                else
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                        .AddEmbed(_embeds.Message($"Successfully banned {user.Username}#{user.Discriminator}. However, I was unable to notify them.",
                            DiscordColor.Orange)));
                }
            }
            else
            {
                await ctx.FollowUpAsync(
                    new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message($"Successfully banned {user.Username}#{user.Discriminator}.", DiscordColor.Green)));
            }
        }

        [SlashCommand("unban", "Unban a user from the main server.")]
        [RequirePermissions(Permissions.BanMembers)]
        [RequireBotPermissions(Permissions.BanMembers)]
        public async Task UnbanMember(InteractionContext ctx,
            [Option("user", "The user to unban.")] DiscordUser user,
            [Option("reason", "The reason the user was unbanned.")] string reason = null)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var config = _config.GetConfig();
            var mainGuild = ctx.Guild.Id == config.MainGuildId
                ? ctx.Guild
                : await DiscordUtils.TryGetGuildAsync(ctx.Client, config.MainGuildId);
            
            var bans = await mainGuild.GetBansAsync();
            var ban = bans.FirstOrDefault(b => b.User.Id == user.Id);

            if (ban == null)
            {
                await ctx.FollowUpAsync(
                    new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message($"Could not unban {user.Username}#{user.Discriminator} as they aren't banned.",
                        DiscordColor.Red)));

                return;
            }
            
            reason ??= await DiscordUtils.PromptForInputAsync("Enter the reason for unbanning the user (or 'cancel' to cancel):", ctx);

            if (string.IsNullOrWhiteSpace(reason))
            {
                return;
            }
            
            await mainGuild.UnbanMemberAsync(user.Id);
            var infractionQuery = await _infractionService.AddInfractionToUserAsync(ban.User, ctx.User, InfractionType.Unban, reason);

            if (infractionQuery.Successful)
            {
                await ctx.FollowUpAsync(
                    new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message($"Successfully unbanned {user.Username}#{user.Discriminator}.", DiscordColor.Green)));
            }
            else
            {
                await ctx.FollowUpAsync(
                    new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message($"Successfully unbanned {user.Username}#{user.Discriminator}. However, I was unable to log the infraction.",
                        DiscordColor.Orange)));
            }
        }

        [SlashCommand("mute", "Gives the configured muted role to the target user.")]
        [RequirePermissions(Permissions.MuteMembers)]
        [RequireBotPermissions(Permissions.ManageRoles)]
        public async Task MuteMember(InteractionContext ctx,
            [Option("user", "The user to give the muted role to.")] DiscordUser user,
            [Option("duration", "The time to mute the user for (0 is permanent).")] string duration,
            [Option("reason", "The reason why the user was muted (leave empty for an interactive prompt).")] string reason = null)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var config = _config.GetConfig();
            var mainGuild = ctx.Guild.Id == config.MainGuildId
                ? ctx.Guild
                : await DiscordUtils.TryGetGuildAsync(ctx.Client, config.MainGuildId);

            var mutedRole = mainGuild.GetRole(config.MutedRoleId);
            if (mutedRole == null)
            {
                await ctx.FollowUpAsync(
                    new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message("This server has no configured muted role, use `/config setmutedrole` first.",
                        DiscordColor.Red)));

                return;
            }
            
            var durationSpan = await DiscordUtils.ConvertToTimespanAsync(ctx, duration);
            if (durationSpan == null)
            {
                return;
            }

            var endTime = DateTimeOffset.Now + durationSpan.Value;
            
            var targetTask = DiscordUtils.TryGetMemberAsync(mainGuild, user.Id);
            var senderTask = DiscordUtils.TryGetMemberAsync(mainGuild, ctx.User.Id);
            var botTask = DiscordUtils.TryGetMemberAsync(mainGuild, ctx.Client.CurrentUser.Id);

            await Task.WhenAll(targetTask, senderTask, botTask);

            var target = targetTask.Result;
            var sender = senderTask.Result;
            var bot = botTask.Result;
            
            if (sender == null)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("I am unable to mute the specified user as I could not find you on the main server (required to check for permissions).", DiscordColor.Red)));
                return;
            }

            if (target == null)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("I am unable to mute the specified user as I could not find them on the main server.", DiscordColor.Red)));
                return;
            }

            if (target.Roles.Any(r => r.Id == mutedRole.Id))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("The target user is already muted.", DiscordColor.Red)));
                return;
            }
            
            if (!DiscordUtils.CheckMemberCanTargetOther(sender, target))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("I am unable to mute the specified user as you cannot target them.", DiscordColor.Red)));
                return;
            }
            
            if (!DiscordUtils.CheckMemberCanTargetOther(bot, target))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("I am unable to mute the specified user as I cannot target them.", DiscordColor.Red)));
                return;
            }
            
            reason ??= await DiscordUtils.PromptForInputAsync("Enter the reason for muting the user (or 'cancel' to cancel):", ctx);

            if (string.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            await target.GrantRoleAsync(mutedRole);
            var infractionQuery = await _infractionService.AddInfractionToUserAsync(target, sender, InfractionType.Mute, reason);
            
            if (!infractionQuery.Successful)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message($"Failed to add infraction to the user: {infractionQuery.Message}.",
                        DiscordColor.Red)));

                return;
            }

            if (durationSpan.Value > TimeSpan.Zero)
            {
                _ = _moderations.AddModerationAsync(infractionQuery.Result, endTime);
            }
            
            var displayedMessage = durationSpan.Value > TimeSpan.Zero
                ? $"You have been muted on **{mainGuild.Name}** for the above reason.\nThis mute will expire <t:{endTime.ToUnixTimeSeconds()}:R>."
                : $"You have been permanently muted on **{mainGuild.Name}** for the above reason.";
                
            var builder = new DiscordMessageBuilder()
                .AddEmbed(_embeds.Message(displayedMessage, DiscordColor.Red))
                .WithContent(reason);
            
            var message = await DiscordUtils.TryMessageUserAsync(target, builder);
            
            if (message != null)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message($"Successfully muted and notified {user.Username}#{user.Discriminator}.", DiscordColor.Green)));
            }
            else
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message($"Successfully muted {user.Username}#{user.Discriminator}. However, I was unable to notify them.",
                        DiscordColor.Orange)));
            }
        }

        [SlashCommand("unmute", "Unmutes the target user.")]
        [RequirePermissions(Permissions.MuteMembers)]
        [RequireBotPermissions(Permissions.ManageRoles)]
        public async Task UnmuteMember(InteractionContext ctx,
            [Option("user", "The user to unmute.")] DiscordUser user,
            [Option("reason", "The reason why the user was unmuted.")] string reason = null)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            
            var config = _config.GetConfig();
            var mainGuild = ctx.Guild.Id == config.MainGuildId
                ? ctx.Guild
                : await DiscordUtils.TryGetGuildAsync(ctx.Client, config.MainGuildId);

            var mutedRole = mainGuild.GetRole(config.MutedRoleId);
            if (mutedRole == null)
            {
                await ctx.FollowUpAsync(
                    new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message("This server has no configured muted role, use `/config setmutedrole` first.",
                        DiscordColor.Red)));

                return;
            }

            var targetTask = DiscordUtils.TryGetMemberAsync(mainGuild, user.Id);
            var senderTask = DiscordUtils.TryGetMemberAsync(mainGuild, ctx.User.Id);
            var botTask = DiscordUtils.TryGetMemberAsync(mainGuild, ctx.Client.CurrentUser.Id);
            
            await Task.WhenAll(targetTask, senderTask, botTask);

            var target = targetTask.Result;
            var sender = senderTask.Result;
            var bot = botTask.Result;
            
            if (sender == null)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("I am unable to unmute the specified user as I could not find you on the main server (required to check for permissions).", DiscordColor.Red)));
                return;
            }

            if (target == null)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("I am unable to unmute the specified user as I could not find them on the main server.", DiscordColor.Red)));
                return;
            }

            if (target.Roles.All(r => r.Id != mutedRole.Id))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("The target user isn't muted.", DiscordColor.Red)));
                return;
            }
            
            if (!DiscordUtils.CheckMemberCanTargetOther(sender, target))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("I am unable to unmute the specified user as you cannot target them.", DiscordColor.Red)));
                return;
            }
            
            if (!DiscordUtils.CheckMemberCanTargetOther(bot, target))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message("I am unable to unmute the specified user as I cannot target them.", DiscordColor.Red)));
                return;
            }
            
            reason ??= await DiscordUtils.PromptForInputAsync("Enter the reason for unmuting the user (or 'cancel' to cancel):", ctx);

            if (string.IsNullOrWhiteSpace(reason))
            {
                return;
            }

            await target.RevokeRoleAsync(mutedRole);
            
            var infractionQuery = await _infractionService.AddInfractionToUserAsync(target, ctx.User, InfractionType.Unmute, reason);

            if (infractionQuery.Successful)
            {
                await ctx.FollowUpAsync(
                    new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message($"Successfully unmuted {user.Username}#{user.Discriminator}.", DiscordColor.Green)));
            }
            else
            {
                await ctx.FollowUpAsync(
                    new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message($"Successfully unmuted {user.Username}#{user.Discriminator}. However, I was unable to log the infraction.",
                        DiscordColor.Orange)));
            }
        }
    }
}