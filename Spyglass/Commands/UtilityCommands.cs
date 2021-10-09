using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using Serilog.Core;
using Spyglass.Preconditions;
using Spyglass.Services;
using Spyglass.Utilities;

namespace Spyglass.Commands
{
    public class UtilityCommands : ApplicationCommandModule
    {
        private readonly EmbedService _embeds;
        private readonly Logger _log;

        public UtilityCommands(EmbedService embeds, Logger log)
        {
            _embeds = embeds;
            _log = log;
        }

        [SlashCommand("avatar", "Sends the avatar of the specified user (or yourself)")]
        public async Task GetAvatar(InteractionContext ctx,
            [Option("member", "The user to get the avatar from.")] DiscordUser user = null)
        {
            user ??= ctx.User;

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, 
                new DiscordInteractionResponseBuilder().AddEmbed(_embeds.AvatarEmbed(ctx.User, user)));
        }

        [ContextMenu(ApplicationCommandType.UserContextMenu, "Display Avatar")]
        public async Task ContextGetAvatar(ContextMenuContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, 
                new DiscordInteractionResponseBuilder().AddEmbed(_embeds.AvatarEmbed(ctx.User, ctx.TargetUser)));
        }
        
        [SlashCommand("echo", "Send a message to the specified channel.")]
        [SlashRequireGuild]
        [RequirePermissions(Permissions.ManageMessages)]
        public async Task EchoMessage(InteractionContext ctx, 
            [Option("channel", "The channel to send the message to")] DiscordChannel channel,
            [Option("message", "The message to send")] string message)
        {
            var isValidChannel = await DiscordUtils.AssertChannelType(ctx, channel, ChannelType.Text, true);
            if (!isValidChannel)
            {
                return;
            }
            
            var interaction = new DiscordInteractionResponseBuilder {IsEphemeral = true}.WithContent("Message sent successfully.");
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, interaction);
            await channel.SendMessageAsync(message);
        }
        
        [SlashCommand("ping", "Check the latency of the Discord gateway.")]
        public async Task CheckPing(InteractionContext ctx)
        {
            var interaction = new DiscordInteractionResponseBuilder().AddEmbed(_embeds.Message($"Pong: websocket round trip time is {ctx.Client.Ping} milliseconds."));
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, interaction);
        }

        [SlashCommand("about", "Learn more about me.")]
        public async Task AboutMessage(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(_embeds.AboutMeEmbed()));
        }

        [SlashCommand("serverinfo", "Outputs information about this server.")]
        public async Task ServerInfo(InteractionContext ctx)
        {
            if (ctx.Guild == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .AddEmbed(_embeds.Message("This command can only be used on a server.", DiscordColor.Red)));

                return;
            }

            var owner = ctx.Guild.Owner;
            var boostTier = ctx.Guild.PremiumTier switch
            {
                0 => "None",
                var tier and > 0 and <= PremiumTier.Tier_3 => $"Level {(int)tier}",
                _ => null,
            };
            var members = ctx.Guild.MaxMembers.HasValue
                ? $"{ctx.Guild.MemberCount}/{ctx.Guild.MaxMembers.Value}"
                : $"{ctx.Guild.MemberCount}";
            
            var embed = new DiscordEmbedBuilder()
                .WithAuthor($"Server Information: {ctx.Guild.Name}")
                .WithThumbnail(ctx.Guild.IconUrl)
                .WithColor(DiscordColor.Blurple)
                .AddField("Owner", $"{owner.Username}#{owner.Discriminator} - {owner.Mention}", true)
                .AddField("Created At",
                    $"{ctx.Guild.CreationTimestamp:ddd dd/MMM/yy HH:MM:ss zz}\n *{Format.GetTimespanString(DateTimeOffset.Now - ctx.Guild.CreationTimestamp)} ago*")
                .AddField("Members", members, true)
                .AddField("Roles", $"{ctx.Guild.Roles.Count}", true)
                .WithFooter($"{ctx.Guild.Id}");

            if (boostTier != null)
            {
                embed.AddField("Nitro Boost", boostTier, true);
            }

            if (!string.IsNullOrWhiteSpace(ctx.Guild.VanityUrlCode))
            {
                embed.AddField("Vanity URL", ctx.Guild.VanityUrlCode);
            }

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                .AddEmbed(embed.Build()));
        }

        [SlashCommand("inviteinfo", "Outputs information about an invite.")]
        public async Task InviteInfo(InteractionContext ctx,
            [Option("invite", "The invite to get information for.")] string invite)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            var parsed = "";
            
            var regex = new Regex(@"(https?:\/\/)?(www.)?(discord.(gg|io|me|li)|discordapp.com\/invite)\/(?<code>[^\s\/]+?(?=\b))");
            var match = regex.Match(invite);

            if (match.Success)
            {
                var group = match.Groups["code"];
                if (group.Success)
                {
                    parsed = group.Value;
                }
            }

            if (string.IsNullOrWhiteSpace(parsed))
            {
                parsed = invite;
            }

            try
            {
                var inviteData = await ctx.Client.GetInviteByCodeAsync(parsed);
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.InviteMetadata(ctx.User, inviteData)));
            }
            catch (Exception e)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message($"An error has occured while requesting invite metadata: {e.Message}",
                    DiscordColor.Red)));
                _log.Error(e, $"Failed to retrieve invite metadata for '{invite}'");
            }
        }
    }
}