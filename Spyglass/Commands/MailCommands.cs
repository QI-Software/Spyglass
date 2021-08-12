using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using Serilog.Core;
using Spyglass.Database.Moderation;
using Spyglass.Preconditions;
using Spyglass.Services;
using Spyglass.Utilities;

namespace Spyglass.Commands
{
    [SlashCommandGroup("mail", "Contains commands pertaining to moderation mail.")]
    [RequireBotPermissions(Permissions.ManageChannels | Permissions.SendMessages | Permissions.AddReactions)]
    public class MailCommands : ApplicationCommandModule
    {
        private readonly BlacklistService _blacklist;
        private readonly ConfigurationService _config;
        private readonly EmbedService _embeds;
        private readonly Logger _log;
        private readonly MailService _mail;

        public MailCommands(BlacklistService blacklist, ConfigurationService config, EmbedService embeds, MailService mail, Logger log)
        {
            _blacklist = blacklist;
            _config = config;
            _embeds = embeds;
            _log = log;
            _mail = mail;
        }

        [SlashCommand("setup", "Set up mod mail in the current server.")]
        [RequirePermissions(Permissions.ManageGuild)]
        public async Task SetupModmail(InteractionContext ctx)
        {
            var config = _config.GetConfig();
            if (config.ModMailEnabled)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().AddEmbed(_embeds.Message("Mod mail is already enabled, please disable it first.", DiscordColor.Red)));
                return;
            }

            var setupButton = new DiscordButtonComponent(ButtonStyle.Primary, "setupModMail", "Setup Mod Mail");
            var cancelButton = new DiscordButtonComponent(ButtonStyle.Danger, "cancelModMail", "Cancel");

            var warningEmbed = _embeds.Message("You are about to setup moderation mail on **this** server. Mod mail requires a **lot** of channels, so I recommend doing this on a secondary server to avoid issues.",
                DiscordColor.Orange);
            
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder()
                    .AddComponents(setupButton, cancelButton)
                    .AddEmbed(warningEmbed));

            var message = await ctx.GetOriginalResponseAsync();

            var result = await message.WaitForButtonAsync(ctx.User, TimeSpan.FromMinutes(5));
            if (result.TimedOut)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message("Timed out.", DiscordColor.Red)));
                return;
            }

            if (result.Result.Id == cancelButton.CustomId)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message("Cancelled.", DiscordColor.Red)));
                return;
            }

            config.ModMailServerId = ctx.Guild.Id;

            var unansweredCategory = ctx.Guild.Channels.Values
                .FirstOrDefault(c => c.IsCategory && c.Name.ToLower().Equals("unanswered"));
            unansweredCategory ??= await ctx.Guild.CreateChannelCategoryAsync("Unanswered");
            
            var answeredCategory = ctx.Guild.Channels.Values
                .FirstOrDefault(c => c.IsCategory && c.Name.ToLower().Equals("answered"));
            answeredCategory ??= await ctx.Guild.CreateChannelCategoryAsync("Answered");

            config.ModMailUnansweredCategoryId = unansweredCategory.Id;
            config.ModMailAnsweredCategoryId = answeredCategory.Id;
            config.ModMailEnabled = true;
            
            try
            {
                await _config.SaveConfigAsync();
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message("Successfully enabled mod mail and saved configuration.", DiscordColor.Green)));
                _ = _mail.RecoverSessionsAsync();
                _log.Information($"{ctx.User} enabled mod mail.");
            }
            catch (Exception e)
            {
                _log.Error(e, "An exception has occurred while modifying the configuration.");
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message("Failed to save configuration, contact the maintainer.", DiscordColor.Red)));
            }
        }

        [SlashCommand("disable", "Disables mod mail, completely stopping incoming and outgoing mail.")]
        [RequirePermissions(Permissions.ManageGuild)]
        public async Task DisableModMail(InteractionContext ctx)
        {
            var config = _config.GetConfig();
            if (!config.ModMailEnabled)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().AddEmbed(_embeds.Message("Mod mail is already disabled, please enable it first.", DiscordColor.Red)));
                return;
            }

            var modMailGuild = await DiscordUtils.TryGetGuildAsync(ctx.Client, config.ModMailServerId);
            if (modMailGuild != null && ctx.Guild.Id != modMailGuild.Id)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().AddEmbed(_embeds.Message("This command can only be ran on the mail server for safety reasons, unless I do not have access to said server.", DiscordColor.Red)));
                return;
            }

            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            config.ModMailEnabled = false;
            config.ModMailServerId = 0;
            config.ModMailAnsweredCategoryId = 0;
            config.ModMailUnansweredCategoryId = 0;
            
            try
            {
                await _config.SaveConfigAsync();
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message("Successfully disabled mod mail.", DiscordColor.Green)));
                
                _log.Information($"{ctx.User} disabled mod mail.");
            }
            catch (Exception e)
            {
                _log.Error(e, "An exception has occurred while modifying the configuration.");
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message("Failed to save configuration, contact the maintainer.", DiscordColor.Red)));
            }
        }

        [SlashCommand("create", "Create a mod mail session to discuss with a user.")]
        public async Task CreateModMailSession(InteractionContext ctx, 
            [Option("user", "The user to create a mod mail session for.")] DiscordUser user)
        {
            var config = _config.GetConfig();
            if (!config.ModMailEnabled)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().AddEmbed(_embeds.Message("Mod mail is not enabled, please enable it first.", DiscordColor.Red)));
                return;
            }
            
            if (_mail.HasSession(user.Id))
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .AddEmbed(_embeds.Message("The specified user already has a mail session opened.", DiscordColor.Red)));
                return;
            }

            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            try
            {
                var session = await _mail.CreateSessionAsync(user.Id);
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message($"Created new session for user {user.Username}#{user.Discriminator}: <#{session.MailChannelId}>.", DiscordColor.Green)));
            }
            catch (Exception e)
            {
                _log.Error(e, $"ModMail: Failed to create session for user {user}: {e.Message}");
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message($"Failed to create session for {user.Username}#{user.Discriminator}: {e.Message}", DiscordColor.Red)));
            }
        }
        
        [SlashCommand("close", "Close a mod mail session.")]
        public async Task CloseModMailSession(InteractionContext ctx,
            [Option("channel", "The channel to close. Leave empty to pick the current one.")] DiscordChannel channel = null)
        {
            var config = _config.GetConfig();
            if (!config.ModMailEnabled)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().AddEmbed(_embeds.Message("Mod mail is not enabled, please enable it first.", DiscordColor.Red)));
                return;
            }
            
            channel ??= ctx.Channel;
            
            if (!_mail.IsChannelLinkedToSession(channel.Id))
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, 
                    new DiscordInteractionResponseBuilder().AddEmbed(_embeds.Message($"{channel.Mention} isn't linked to any mail session.", DiscordColor.Red)));
                return;
            }

            var session = _mail.GetSessionFromChannel(ctx.Channel.Id);
            
            var query = _blacklist.IsBlacklisted(BlacklistType.ModMail, session.MailUserId);
            if (query.Successful && !query.Result && ctx.Channel.Parent != null 
                && ctx.Channel.ParentId != config.ModMailUnansweredCategoryId)
            {
                var mainGuild = await DiscordUtils.TryGetGuildAsync(ctx.Client, config.MainGuildId);
                if (mainGuild != null)
                {
                    var sessionUser = await DiscordUtils.TryGetMemberAsync(mainGuild, session.MailUserId);
                    _ = DiscordUtils.TryMessageUserAsync(sessionUser, new DiscordMessageBuilder().WithEmbed(_embeds.Message(
                        "This moderation mail session is now closed, thank you!",
                        DiscordColor.Green)));
                }
            }
            
            await _mail.CloseMailSessionAsync(session);
        }
    }
}