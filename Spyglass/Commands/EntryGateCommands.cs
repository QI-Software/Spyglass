using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using Serilog.Core;
using Spyglass.Preconditions;
using Spyglass.Services;
using Spyglass.Utilities;

namespace Spyglass.Commands
{
    [SlashCommandGroup("entrygate", "Module for the server entry gate system.")]
    [RequireConfiguredMainGuild(true)]
    [RequirePermissions(Permissions.ManageGuild)]
    public class EntryGateCommands : ApplicationCommandModule
    {
        private readonly ConfigurationService _config;
        private readonly DiscordClient _client;
        private readonly EmbedService _embeds;
        private readonly Logger _log;

        public EntryGateCommands(ConfigurationService config, DiscordClient client, EmbedService embeds, Logger log)
        {
            _config = config;
            _client = client;
            _embeds = embeds;
            _log = log;
        }
        
        [SlashCommand("setup", "Setup the entry gate in the current channel.")]
        public async Task ToggleEntryGate(InteractionContext ctx,
            [Option("channel", "The channel in which to setup the entry gate in.")] DiscordChannel channel,
            [Option("role", "The role to add to users who pass the entry gate.")] DiscordRole role)
        {
            var config = _config.GetConfig();
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            if (config.EntryGateEnabled)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message("Entry gate is already enabled, use /entrygate disable first.", DiscordColor.Red)));
                return;
            }

            var member = await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id);
            if (!member.PermissionsIn(channel).HasPermission(Permissions.AccessChannels | Permissions.SendMessages))
            {
                var permsString = (Permissions.AccessChannels | Permissions.SendMessages).ToPermissionString();
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message($"I am missing the following permissions in {channel.Mention} to setup the entry gate: {permsString}.", DiscordColor.Red)));
                return;
            }
            
            if (!DiscordUtils.CanAddRoleToMember(role, member))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message($"I cannot give the role {role.Name} as it is higher or equal to my highest role.", DiscordColor.Red)));
                return;
            }

            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                .AddEmbed(_embeds.Message("Please type in the message to send in entry gate. Feel free to explain what to do to new members and how to enter.",
                    DiscordColor.Green)));

            var entryContentMessage = await ctx.Channel.GetNextMessageAsync(msg => msg.Author.Id == ctx.User.Id, TimeSpan.FromMinutes(5));

            if (entryContentMessage.TimedOut)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message("Timed out, please try again.", DiscordColor.Red)));

                return;
            }

            if (string.IsNullOrWhiteSpace(entryContentMessage.Result.Content))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message("The message cannot be empty, please try again.", DiscordColor.Red)));

                return;
            }
            
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                .AddEmbed(_embeds.Message("Please type in the label to display on the entry button (max 80 characters, example: 'Enter Server'))",
                    DiscordColor.Green)));
            
            var entryButtonLabelMessage = await ctx.Channel.GetNextMessageAsync(msg => msg.Author.Id == ctx.User.Id, TimeSpan.FromMinutes(5));
            
            if (entryButtonLabelMessage.TimedOut)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message("Timed out, please try again.", DiscordColor.Red)));

                return;
            }

            if (string.IsNullOrWhiteSpace(entryButtonLabelMessage.Result.Content))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message("The label cannot be empty, please try again.", DiscordColor.Red)));

                return;
            }
            
            if (entryButtonLabelMessage.Result.Content.Length > 80)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message("The maximum label size is 80 characters, please try again.", DiscordColor.Red)));

                return;
            }

            var entryButton = new DiscordButtonComponent(ButtonStyle.Primary, "EntryGateButton", entryButtonLabelMessage.Result.Content.Trim());
            var builder = new DiscordMessageBuilder()
                .WithContent(entryContentMessage.Result.Content)
                .AddComponents(entryButton);
            
            var entryMessage = await channel.SendMessageAsync(builder);
            config.EntryGateEnabled = true;
            config.EntryGateChannelId = channel.Id;
            config.EntryGateMessageId = entryMessage.Id;
            config.EntryGateRoleId = role.Id;
            
            try
            {
                await _config.SaveConfigAsync();
                _log.Information($"{ctx.User} enabled the entry gate for the main server.");
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message($"Successfully enabled the entry gate in {channel.Mention}.", DiscordColor.Green)));
            }
            catch (Exception e)
            {
                _log.Error(e, "Failed to save configuration after setting up the entry gate.");
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message("Failed to save configuration after enabling the entry gate, please contact a maintainer.", DiscordColor.Red)));
            }
        }

        [SlashCommand("disable", "Disables the entry gate.")]
        public async Task DisableEntryGate(InteractionContext ctx)
        {
            var config = _config.GetConfig();
            if (!config.EntryGateEnabled)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .AddEmbed(_embeds.Message("The entry gate is not enabled.", DiscordColor.Red)));
                return;
            }

            config.EntryGateEnabled = false;
            config.EntryGateChannelId = 0;
            config.EntryGateMessageId = 0;
            config.EntryGateRoleId = 0;

            try
            {
                await _config.SaveConfigAsync();
                _log.Information($"{ctx.User} disabled the entry gate for the main server.");
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .AddEmbed(_embeds.Message("Successfully disabled the entry gate.", DiscordColor.Green)));
            }
            catch (Exception e)
            {
                _log.Error(e, "Failed to save configuration after disabling the entry gate.");
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .AddEmbed(_embeds.Message("Failed to save configuration after disabling the entry gate, please contact a maintainer.", DiscordColor.Red)));
            }
        }
    }
}