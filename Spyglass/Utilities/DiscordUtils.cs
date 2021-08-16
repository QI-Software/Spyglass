using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.DependencyInjection;
using Spyglass.Database.Moderation;
using Spyglass.Services;

namespace Spyglass.Utilities
{
    public class DiscordUtils
    {
        public static async Task<DiscordUser> TryGetUserAsync(DiscordClient client, ulong userId)
        {
            try
            {
                return await client.GetUserAsync(userId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<DiscordGuild> TryGetGuildAsync(DiscordClient client, ulong guildId)
        {
            try
            {
                return await client.GetGuildAsync(guildId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<DiscordMember> TryGetMemberAsync(DiscordGuild guild, ulong userId)
        {
            try
            {
                return await guild.GetMemberAsync(userId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<DiscordChannel> TryGetChannelAsync(DiscordClient client, ulong channelId)
        {
            try
            {
                return await client.GetChannelAsync(channelId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<DiscordMessage> TryGetMessageAsync(DiscordChannel channel, ulong messageId)
        {
            try
            {
                return await channel.GetMessageAsync(messageId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<DiscordInvite> TryGetInvite(DiscordClient client, string invite)
        {
            try
            {
                return await client.GetInviteByCodeAsync(invite, true);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<bool> TryUnbanUserAsync(DiscordGuild guild, ulong userId)
        {
            try
            {
                await guild.UnbanMemberAsync(userId);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<DiscordMessage> TryMessageUserAsync(DiscordMember member, DiscordMessageBuilder builder)
        {
            try
            {
                return await member.SendMessageAsync(builder);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        public static bool CheckMemberCanTargetOther(DiscordMember member, DiscordMember other)
        {
            if (!other.Roles.Any()) return true;
            if (!member.Roles.Any()) return false;
            
            var memberRoles = member.Roles.Select(r => r.Position).OrderByDescending(i => i).First();
            var otherRoles = other.Roles.Select(r => r.Position).OrderByDescending(i => i).First();

            return memberRoles > otherRoles;
        }

        public static bool CanAddRoleToMember(DiscordRole role, DiscordMember botMember)
        {
            var highestRole = botMember.Roles.OrderByDescending(r => r.Position).FirstOrDefault();

            return highestRole == null || role.Position < highestRole.Position;
        }
        
        public static DiscordMember TryGetMemberFromName(DiscordGuild guild, string name)
        {
            var member =
                guild.Members.Values.FirstOrDefault(
                    m => m.Username.Equals(name, StringComparison.OrdinalIgnoreCase));

            member ??= guild.Members.Values.FirstOrDefault(m => m.Nickname != null && m.Nickname.Equals(name, StringComparison.OrdinalIgnoreCase));

            member ??= guild.Members.Values.FirstOrDefault(m => m.Username.ToLower().Contains(name.ToLower()));
            
            member ??= guild.Members.Values.FirstOrDefault(m => m.Nickname != null && m.Nickname.ToLower().Contains(name.ToLower()));

            return member;
        }
        
        public static DiscordColor GetColorForInfraction(InfractionType infractionType)
        {
            switch (infractionType)
            {
                case InfractionType.Note:
                    return DiscordColor.Blurple;
                case InfractionType.Unmute:
                case InfractionType.Unban:
                case InfractionType.Undeafen:
                    return DiscordColor.Green;
                case InfractionType.Warn:
                    return DiscordColor.Yellow;
                case InfractionType.Mute:
                    return DiscordColor.Orange;
                case InfractionType.Kick:
                case InfractionType.Ban:
                    return DiscordColor.Red;
                default:
                    return DiscordColor.White;
            }
        }

        public static async Task<string> PromptForInputAsync(string displayText, InteractionContext ctx, TimeSpan? timeout = null, int maxLength = 1024)
        {
            timeout ??= TimeSpan.FromMinutes(5);

            var embeds = ctx.Services.GetRequiredService<EmbedService>();
            await ctx.FollowUpAsync(
                new DiscordFollowupMessageBuilder().AddEmbed(embeds.Message(displayText, DiscordColor.White)));
            
            var message = await ctx.Channel.GetNextMessageAsync(ctx.User, timeout);
            if (message.TimedOut)
            {
                await ctx.FollowUpAsync(
                    new DiscordFollowupMessageBuilder().AddEmbed(embeds.Message("Timed out, please try again.",
                        DiscordColor.Red)));
                return null;
            }
            
            if (message.Result.Content.Trim().ToLower() == "cancel")
            {
                await ctx.FollowUpAsync(
                    new DiscordFollowupMessageBuilder().AddEmbed(embeds.Message("Cancelled.",
                        DiscordColor.Red)));
                return null;
            }

            if (message.Result.Content.Length > maxLength)
            {
                await ctx.FollowUpAsync(
                    new DiscordFollowupMessageBuilder().AddEmbed(embeds.Message($"Input is too long (must be shorter than {maxLength + 1} characters).",
                        DiscordColor.Red)));
                return null;
            }

            return message.Result.Content;
        }

        public static async Task<bool> AssertChannelType(InteractionContext ctx, DiscordChannel channel, ChannelType type, bool isEphemeral = false)
        {
            var embeds = ctx.Services.GetRequiredService<EmbedService>();
            if (channel == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder { IsEphemeral = isEphemeral }
                    .AddEmbed(embeds.Message("An invalid channel was specified.", DiscordColor.Red)));

                return false;
            }

            if (channel.Type != type)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder { IsEphemeral = isEphemeral }
                    .AddEmbed(embeds.Message($"Channel must be of type '{Enum.GetName(type)}'.", DiscordColor.Red)));

                return false;
            }

            return true;
        }

        public static async Task<TimeSpan?> ConvertToTimespanAsync(InteractionContext ctx, string argument)
        {
            var embeds = ctx.Services.GetRequiredService<EmbedService>();
            var durationSpan = ConvertArgumentToTimeSpan(argument);
            if (!durationSpan.HasValue || durationSpan.Value < TimeSpan.Zero)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder {IsEphemeral = true}
                        .AddEmbed(embeds.Message("Invalid duration, must be a time in the future (ex: 4w (4 weeks) 2d (2 days), 240m (240 minutes))",
                            DiscordColor.Red)));
                return null;
            }

            return durationSpan.Value;
        }

        public static Optional<TimeSpan> ConvertArgumentToTimeSpan(string argument)
        {
            return new TimeSpanConverter().ConvertFromString(argument);
        }
    }
}