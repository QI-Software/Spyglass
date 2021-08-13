using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Serilog.Core;
using Spyglass.Preconditions;
using Spyglass.Services;

namespace Spyglass.Commands
{
    public enum ConfigurationChannelType
    {
        [ChoiceName("Infraction Channel")]
        InfractionChannel,
        [ChoiceName("Member Joined Channel")]
        MemberJoinedChannel,
    }
    
    [SlashCommandGroup("config", "A group of configuration commands for Spyglass.")]
    [RequirePermissions(Permissions.ManageGuild)]
    public class ConfigurationCommands : ApplicationCommandModule
    {
        private readonly ConfigurationService _config;
        private readonly EmbedService _embeds;
        private readonly Logger _log;

        public ConfigurationCommands(ConfigurationService config, EmbedService embeds, Logger log)
        {
            _config = config;
            _embeds = embeds;
            _log = log;
        }
        
        [SlashCommand("logchannel", "Change the channel for a log category.")]
        public async Task ConfigureLogChannel(InteractionContext ctx,
            [Option("category", "The type of log category to configure.")] ConfigurationChannelType category,
            [Option("channel", "The channel to log to (leave empty to disable logging)")] DiscordChannel channel = null)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            if (channel != null)
            {
                if (channel.Type != ChannelType.Text)
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                        .AddEmbed(_embeds.Message($"I cannot use {channel.Mention} as it isn't a text channel.")));
                    return;
                }
                
                var self = await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id);
                var requiredPermissions = Permissions.AccessChannels | Permissions.SendMessages;
                var channelPerms = channel.PermissionsFor(self);
                if (!channelPerms.HasPermission(requiredPermissions))
                {
                    var missingPerms = requiredPermissions & ~channelPerms;
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                        .AddEmbed(_embeds.Message(
                            $"I cannot use {channel.Mention} as a logging channel because I am missing the following permissions: {missingPerms.ToPermissionString()}")));

                    return;
                }
            }

            var config = _config.GetConfig();
            var id = channel?.Id ?? 0;
            var categoryName = "";
            
            switch (category)
            {
                case ConfigurationChannelType.InfractionChannel:
                    categoryName = "infraction logging";
                    config.InfractionLogChannelId = id;
                    break;
                case ConfigurationChannelType.MemberJoinedChannel:
                    categoryName = "member join logging";
                    config.MemberJoinLogChannelId = id;
                    break;
            }

            try
            {
                await _config.SaveConfigAsync();
                if (channel != null)
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                        _embeds.Message($"Successfully set the {categoryName} channel to {channel.Mention}.", DiscordColor.Green)));
                }
                else
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                        _embeds.Message($"Successfully disabled {categoryName}.", DiscordColor.Green)));
                }

                _log.Information($"{ctx.User} modified the log configuration.");
            }
            catch (Exception e)
            {
                _log.Error(e, "An exception has occurred while modifying the configuration.");
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message("Failed to save configuration, contact the maintainer.", DiscordColor.Red)));
            }
        }

        [SlashCommand("setmainserver", "Sets the current server as the bot's main moderated server.")]
        public async Task ConfigureMainServer(InteractionContext ctx)
        {
            var config = _config.GetConfig();
            config.MainGuildId = ctx.Guild.Id;
            
            try
            {
                await _config.SaveConfigAsync();
                
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .AddEmbed(_embeds.Message($"Successfully set {ctx.Guild.Name} as the main server.", DiscordColor.Green)));
                
                _log.Information($"{ctx.User} modified the main server id configuration.");
            }
            catch (Exception e)
            {
                _log.Error(e, "An exception has occurred while modifying the configuration.");
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message("Failed to save configuration, contact the maintainer.", DiscordColor.Red)));
            }
        }

        [SlashCommand("setmutedrole", "Sets the role to give to muted users.")]
        [RequireConfiguredMainGuild(true)]
        public async Task SetMutedRole(InteractionContext ctx,
            [Option("role", "The role to use as the muted role.")] DiscordRole role)
        {
            var config = _config.GetConfig();

            var self = await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id);
            var highestRole = self.Roles.OrderByDescending(r => r.Position).FirstOrDefault();

            if (role.Position >= highestRole?.Position)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder {IsEphemeral = true}
                        .AddEmbed(_embeds.Message($"I cannot use {role.Mention} as the muted role as it is higher or equal to my current roles.", DiscordColor.Red)));

                return;
            }

            config.MutedRoleId = role.Id;
            try
            {
                await _config.SaveConfigAsync();
                _log.Information($"{ctx.User} set the muted role to {role.Name}.");
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().AddEmbed(_embeds.Message($"Successfully set {role.Mention} as the muted role.", DiscordColor.Green)));
            }
            catch (Exception e)
            {
                _log.Error(e, "Failed to save configuration after setting muted role id.");
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(_embeds.Message($"Failed to save configuration after setting the new muted role to {role.Mention}.", DiscordColor.Red)));
            }
        }

        [SlashCommand("togglereactionroles", "Enables/disables reaction roles.")]
        public async Task ToggleReationRoles(InteractionContext ctx)
        {
            var config = _config.GetConfig();
            config.ReactionRolesEnabled = !config.ReactionRolesEnabled;

            var value = config.ReactionRolesEnabled ? "enabled" : "disabled"; 
            
            try
            {
                await _config.SaveConfigAsync();
                
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .AddEmbed(_embeds.Message($"Successfully {value} reaction roles.", DiscordColor.Green)));
                
                _log.Information($"{ctx.User} {value} reaction roles.");
            }
            catch (Exception e)
            {
                _log.Error(e, "An exception has occurred while modifying the configuration.");
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message("Failed to save configuration, contact the maintainer.", DiscordColor.Red)));
            }
        }

        [SlashCommand("setstatus", "Sets the bot's playing status.")]
        public async Task SetStatusAsync(InteractionContext ctx,
            [Option("status", "The bot's new playing status")] string status = null)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var config = _config.GetConfig();
            config.PlayingStatus = status;

            if (string.IsNullOrWhiteSpace(config.PlayingStatus))
            {
                await ctx.Client.UpdateStatusAsync();
            }
            else
            {
                await ctx.Client.UpdateStatusAsync(new DiscordActivity(status, ActivityType.Playing));
            }

            try
            {
                await _config.SaveConfigAsync();
                var message = string.IsNullOrWhiteSpace(config.PlayingStatus)
                    ? "Successfully disabled my playing status."
                    : $"Successfully set my playing status to '{config.PlayingStatus}'.";

                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message(message, DiscordColor.Green)));
            }
            catch (Exception e)
            {
                _log.Error(e, "Failed to save configuration after setting the bot's playing status.");
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message("An error has occured while saving the configuration. Please contact a maintainer.", DiscordColor.Red)));
            }
        }
    }
}