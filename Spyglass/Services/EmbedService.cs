using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using Spyglass.Database.Moderation;
using Spyglass.Providers;
using Spyglass.Utilities;

namespace Spyglass.Services
{
    public class EmbedService
    {
        private readonly ConfigurationService _config;
        private readonly DiscordClient _client;
        
        const int EmbedCharacterLimit = 6000;
        const int EmbedDescriptionLimit = 2048;

        public EmbedService(ConfigurationService config, DiscordClient client)
        {
            _config = config;
            _client = client;
        }
        
        public DiscordEmbed Message(string content, DiscordColor? color = null)
        {
            color ??= DiscordColor.Green;

            var embed = new DiscordEmbedBuilder()
                .WithColor(color.Value)
                .WithDescription(content);

            return embed.Build();
        }

        public DiscordEmbed UserEmbed(DiscordUser user, int? infractionCount = null)
        {
            var createdAgo = $"{user.CreationTimestamp:ddd dd/MMM/yy HH:MM:ss zz}\n *{Format.GetTimespanString(DateTimeOffset.Now - user.CreationTimestamp)} ago*";
            var avatar = user.GetAvatarUrl(ImageFormat.Auto);

            var embed = new DiscordEmbedBuilder()
                .WithAuthor($"{user.Username}#{user.Discriminator} ({user.Id})",
                    iconUrl: avatar)
                .WithColor(DiscordColor.Blurple)
                .AddField("Created At", createdAgo, true)
                .AddField("Mention", user.Mention)
                .WithFooter(user.Id.ToString());
            
            if (infractionCount != null)
            {
                embed.AddField("Infractions", $"{infractionCount.Value}", true);
            }
            
            return embed.Build();
        }

        public DiscordEmbed MemberEmbed(DiscordMember member, bool withRoles = true, int? infractionCount = null)
        {
            var nickname = string.IsNullOrEmpty(member.Nickname)
                ? "None"
                : member.Nickname;

            DiscordEmoji status = null;
            string statusStr = "";

            var config = _config.GetConfig();
            switch (member.Presence?.Status)
            {
                case UserStatus.Online:
                    status = DiscordEmoji.FromUnicode("🟢");
                    statusStr = "Online";
                    break;
                case UserStatus.Idle:
                    status = DiscordEmoji.FromUnicode("🟠");
                    statusStr = "Idle";
                    break;
                case UserStatus.DoNotDisturb:
                    status = DiscordEmoji.FromUnicode("🔴");
                    statusStr = "Do Not Disturb";
                    break;
                default:
                    status = DiscordEmoji.FromUnicode("⚪");
                    statusStr = "Offline or Invisible";
                    break;
            }

            var joinedAgo = $"{member.JoinedAt:ddd dd/MMM/yy HH:MM:ss zz}\n *{Format.GetTimespanString(DateTimeOffset.Now - member.JoinedAt)} ago*";
            var createdAgo = $"{member.CreationTimestamp:ddd dd/MMM/yy HH:MM:ss zz}\n *{Format.GetTimespanString(DateTimeOffset.Now - member.CreationTimestamp)} ago*";

            var avatar = member.GetAvatarUrl(ImageFormat.Auto);

            var embed = new DiscordEmbedBuilder()
                .WithAuthor($"{member.Username}#{member.Discriminator} ({member.Id})",
                    iconUrl: avatar)
                .WithDescription(Formatter.Bold($"Nickname: {nickname}"))
                .WithColor(DiscordColor.Blurple);

            string activities = string.Empty;
            if (member.Presence?.Activities != null)
            {
                foreach (DiscordActivity activity in member.Presence.Activities)
                {
                    if (activity.ActivityType == ActivityType.Custom)
                    {
                        activities += $"{Formatter.Bold(activity.ActivityType.ToString() + ":")} {activity.CustomStatus.Name}\n";
                    }
                    else
                    {
                        activities += $"{Formatter.Bold(activity.ActivityType.ToString() + ":")} {activity.Name}\n";
                    }
                }
            }

            if (activities != string.Empty)
            {
                embed.AddField(Formatter.Underline("Activities"), activities, member.VoiceState != null);
            }

            if (member.VoiceState?.Channel != null)
            {
                embed.AddField(Formatter.Underline("Voice Channel"), member.VoiceState.Channel.Name, false);
            }
            
            embed.AddField("User Status", $"{status} {statusStr}", true)
                .AddField("Joined At", joinedAgo, true)
                .AddField("Created At", createdAgo, true)
                .AddField("Mention", member.Mention, true)
                .WithFooter(member.Id.ToString());

            if (infractionCount != null)
            {
                embed.AddField("Infractions", $"{infractionCount.Value}", true);
            }

            if (withRoles && member.Roles.Any())
            {
                embed.AddField($"Roles [{member.Roles.Count()}]",
                    member.Roles
                        .OrderByDescending(r => r.Position)
                        .Select(r => r.Mention)
                        .Aggregate("", (current, next) => $"{current} {next}"));
            }

            return embed.Build();
        }
        
        public async Task<DiscordEmbed> GetInfractionInformationAsync(Infraction infraction, DiscordUser requester = null)
        {
            Assert.IsNotNull(infraction, "Null infraction");
            
            var authorBuilder = new StringBuilder();
            authorBuilder.Append($"Infraction #{infraction.Id} - {infraction.Type}");

            if (infraction.Timestamp != DateTimeOffset.UnixEpoch)
            {
                authorBuilder.AppendLine(" - " + infraction.Timestamp.ToString("ddd dd/MMM/yy HH:MM:ss zz"));
            }
            else authorBuilder.AppendLine();

            var infractionUser = await DiscordUtils.TryGetUserAsync(_client, infraction.UserId);
            var moderationUser = await DiscordUtils.TryGetUserAsync(_client, infraction.StaffId);
            var moderationName = moderationUser != null
                ? $"{moderationUser.Username}#{moderationUser.Discriminator}"
                : infraction.StaffName;

            var embed = new DiscordEmbedBuilder()
                .WithAuthor(authorBuilder.ToString())
                .WithColor(DiscordUtils.GetColorForInfraction(infraction.Type))
                .WithFooter(requester != null ? $"Requested by {requester.Username}#{requester.Discriminator}" : infraction.UserId.ToString());

            var descriptionBuilder = new StringBuilder();
            descriptionBuilder.AppendLine(Formatter.Bold("User:") + $" ({infraction.UserId}) {infraction.UserName}");
            descriptionBuilder.AppendLine(Formatter.Bold("Moderator:") + $" ({infraction.StaffId}) {moderationName}");

            if (infraction.LastUpdatedAt != null)
            {
                descriptionBuilder.AppendLine(Formatter.Bold("Updated At: ") +
                                              infraction.LastUpdatedAt.Value.ToString("ddd dd/MMM/yy HH:MM:ss zz"));
            }

            var leftChar = EmbedDescriptionLimit - descriptionBuilder.Length;

            string reason;
            if (string.IsNullOrEmpty(infraction.Reason))
            {
                reason = "No reason provided.";
            }
            else
            {
                reason = infraction.Reason.Length <= leftChar ? infraction.Reason : infraction.Reason.Substring(0, leftChar);
            }
            
            descriptionBuilder.AppendLine(Formatter.Bold("Reason:") + $" {reason}");
            embed.WithDescription(descriptionBuilder.ToString());

            return embed.Build();
        }
        
        public async Task<List<DiscordEmbed>> UserInfractionsEmbeds(DiscordUser requester, DiscordUser user, List<Infraction> infractions)
        {
            var infractionBuilder = new StringBuilder();
            var values = Enum.GetValues(typeof(InfractionType)).Cast<InfractionType>().ToArray();
            var embeds = new List<DiscordEmbed>();
            
            foreach (var type in values)
            {
                var count = infractions.Count(i => i.Type == type);
                if (count == 0) continue;

                var name = Format.GetInfractionTypeString(type, count).ToLower();
                if (infractionBuilder.Length != 0) infractionBuilder.Append(", ");
                infractionBuilder.Append(name);
            }

            var mainEmbed = new DiscordEmbedBuilder()
                .WithAuthor($"{user.Username}#{user.Discriminator}'s infractions")
                .WithDescription(infractionBuilder.ToString())
                .WithColor(DiscordColor.Blurple)
                .WithFooter($"Requested by {requester.Username}#{requester.Discriminator}");

            var currentCharacterCount = 0;
            currentCharacterCount += mainEmbed.Author.Name.Length;
            currentCharacterCount += mainEmbed.Description.Length;
            currentCharacterCount += mainEmbed.Footer.Text.Length;

            var currentBuilder = mainEmbed;
            
            foreach (var infraction in infractions)
            {
                // Max field count in an embed
                if (currentBuilder.Fields.Count == 25)
                {
                    embeds.Add(currentBuilder.Build());
                    currentBuilder = new DiscordEmbedBuilder()
                        .WithColor(DiscordColor.Blurple)
                        .WithFooter($"Requested by {requester.Username}#{requester.Discriminator}");

                    currentCharacterCount = currentBuilder.Footer.Text.Length;
                }
                
                var titleBuilder = new StringBuilder();
                titleBuilder.Append($"#{infraction.Id} - {infraction.Type}");
                if (infraction.Timestamp != DateTimeOffset.UnixEpoch)
                {
                    titleBuilder.AppendLine(" - " + infraction.Timestamp.ToString("ddd dd/MMM/yy HH:MM:ss zz"));
                }
                else titleBuilder.AppendLine();

                var infractionUser = await DiscordUtils.TryGetUserAsync(_client, infraction.UserId);
                var moderationUser = await DiscordUtils.TryGetUserAsync(_client, infraction.StaffId);
                var moderationName = moderationUser != null
                    ? $"{moderationUser.Username}#{moderationUser.Discriminator}"
                    : infraction.StaffName;
                
                var valueBuilder = new StringBuilder();
                valueBuilder.AppendLine(Formatter.Bold("User:") + $" ({infraction.UserId}) {infraction.UserName}");
                valueBuilder.AppendLine(Formatter.Bold("Moderator:") + $" ({infraction.StaffId}) {moderationName}");
                
                if (infraction.LastUpdatedAt != null)
                {
                    valueBuilder.AppendLine(Formatter.Bold("Updated At: ") +
                                            infraction.LastUpdatedAt.Value.ToString("ddd dd/MMM/yy HH:MM:ss zz"));
                }
                
                var left = 1024 - valueBuilder.Length;
                var reason = string.Empty;

                if (infraction.Reason == null)
                {
                    reason = "No reason provided.";
                }
                else
                {
                    if (infraction.Reason.Length <= left)
                    {
                        reason = infraction.Reason;
                    }
                    else
                    {
                        reason = infraction.Reason.Substring(0, left);
                    }
                }
                
                valueBuilder.AppendLine(Formatter.Bold("Reason:") + $" {reason}");

                // Check character count.
                var cCount = titleBuilder.ToString().Length + valueBuilder.ToString().Length;
                if (currentCharacterCount +  cCount > EmbedCharacterLimit)
                {
                    embeds.Add(currentBuilder.Build());
                    currentBuilder = new DiscordEmbedBuilder()
                        .WithColor(DiscordColor.Blurple)
                        .WithFooter($"Requested by {requester.Username}#{requester.Discriminator}");

                    currentCharacterCount = currentBuilder.Footer.Text.Length;
                }
                
                currentBuilder.AddField(titleBuilder.ToString(), valueBuilder.ToString());
                currentCharacterCount += cCount;
            }
            
            embeds.Add(currentBuilder.Build());

            return embeds;
        }
        
        public async Task<List<DiscordEmbed>> UserBlacklistsEmbedsAsync(DiscordUser requester, DiscordUser user, List<BlacklistedUser> blacklists)
        {
            var blacklistBuilder = new StringBuilder();
            var embeds = new List<DiscordEmbed>();


            var mainEmbed = new DiscordEmbedBuilder()
                .WithAuthor($"{user.Username}#{user.Discriminator}'s infractions")
                .WithDescription(blacklistBuilder.ToString())
                .WithColor(DiscordColor.Blurple)
                .WithFooter($"Requested by {requester.Username}#{requester.Discriminator}");

            var currentCharacterCount = 0;
            currentCharacterCount += mainEmbed.Author.Name.Length;
            currentCharacterCount += mainEmbed.Description.Length;
            currentCharacterCount += mainEmbed.Footer.Text.Length;

            var currentBuilder = mainEmbed;
            
            foreach (var blacklist in blacklists)
            {
                // Max field count in an embed
                if (currentBuilder.Fields.Count == 25)
                {
                    embeds.Add(currentBuilder.Build());
                    currentBuilder = new DiscordEmbedBuilder()
                        .WithColor(DiscordColor.Blurple)
                        .WithFooter($"Requested by {requester.Username}#{requester.Discriminator}");

                    currentCharacterCount = currentBuilder.Footer.Text.Length;
                }
                
                var titleBuilder = new StringBuilder();
                titleBuilder.Append($"#{blacklist.Id} - {blacklist.Type}");
                titleBuilder.AppendLine();

                var moderationUser = await DiscordUtils.TryGetUserAsync(_client, blacklist.StaffId);
                var moderationName = moderationUser != null
                    ? $"{moderationUser.Username}#{moderationUser.Discriminator}"
                    : blacklist.StaffId.ToString();
                
                var valueBuilder = new StringBuilder();
                valueBuilder.AppendLine(Formatter.Bold("User:") + $" ({blacklist.UserId}) {user.Username}#{user.Discriminator}");
                valueBuilder.AppendLine(Formatter.Bold("Moderator:") + $" ({blacklist.StaffId}) {moderationName}");
                
                var left = 1024 - valueBuilder.Length;
                var reason = string.Empty;

                if (blacklist.Reason == null)
                {
                    reason = "No reason provided.";
                }
                else
                {
                    if (blacklist.Reason.Length <= left)
                    {
                        reason = blacklist.Reason;
                    }
                    else
                    {
                        reason = blacklist.Reason.Substring(0, left);
                    }
                }
                
                valueBuilder.AppendLine(Formatter.Bold("Reason:") + $" {reason}");

                // Check character count.
                var cCount = titleBuilder.ToString().Length + valueBuilder.ToString().Length;
                if (currentCharacterCount +  cCount > EmbedCharacterLimit)
                {
                    embeds.Add(currentBuilder.Build());
                    currentBuilder = new DiscordEmbedBuilder()
                        .WithColor(DiscordColor.Blurple)
                        .WithFooter($"Requested by {requester.Username}#{requester.Discriminator}");

                    currentCharacterCount = currentBuilder.Footer.Text.Length;
                }
                
                currentBuilder.AddField(titleBuilder.ToString(), valueBuilder.ToString());
                currentCharacterCount += cCount;
            }
            
            embeds.Add(currentBuilder.Build());

            return embeds;
        }
         
         public DiscordEmbed AvatarEmbed(DiscordUser requester, DiscordUser user)
         {
             var embed = new DiscordEmbedBuilder()
                 .WithAuthor($"{user.Username}#{user.Discriminator}'s avatar")
                 .WithImageUrl(user.GetAvatarUrl(ImageFormat.Auto))
                 .WithColor(DiscordColor.Blurple)
                 .WithFooter($"Requested by {requester.Username}#{requester.Discriminator}");

             return embed.Build();
         }
         
        public async Task<List<DiscordEmbed>> OngoingModerationsAsync(List<OngoingModeration> moderations)
        {
            if (moderations.Count == 0)
            {
                return new List<DiscordEmbed>() { Message("There are no ongoing moderations at this time.", DiscordColor.Green)};
            }

            var embed = new DiscordEmbedBuilder()
                .WithAuthor(string.Format(new PluralFormatProvider(), "{0:moderation;moderations}",
                    moderations.Count))
                .WithColor(DiscordColor.Blurple);
            
            var stringBuilder = new StringBuilder();
            var currentEmbedBuilder = embed;
            var currentCharCount = embed.Author.Name.Length;
            var embeds = new List<DiscordEmbed>();
            
            foreach (var mod in moderations)
            {
                var timeLeft = mod.EndTime - DateTimeOffset.Now;
                var timeString = timeLeft > TimeSpan.Zero
                    ? $"{Format.GetTimespanString(timeLeft)} left"
                    : "Expired";

                var user = await DiscordUtils.TryGetUserAsync(_client, mod.UserId);
                var userString = user != null
                    ? $"{user.Username}#{user.Discriminator}"
                    : mod.LinkedInfraction?.UserName ?? mod.UserId.ToString();
                
                var str = $"#{mod.LinkedInfractionId} ({mod.Type}) - {userString} - {timeString}";
                if (currentCharCount + str.Length + 2 < 2048)
                {
                    stringBuilder.AppendLine(str);
                    currentCharCount += str.Length + 2;
                }
                else
                {
                    currentEmbedBuilder.WithDescription(stringBuilder.ToString());
                    embeds.Add(currentEmbedBuilder.Build());
                    currentEmbedBuilder = new DiscordEmbedBuilder()
                        .WithColor(DiscordColor.Blurple);
                    currentCharCount = str.Length + 2;
                    
                    stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine(str);
                }
            }

            if (stringBuilder.Length != 0)
            {
                currentEmbedBuilder.WithDescription(stringBuilder.ToString());
                embeds.Add(currentEmbedBuilder.Build());
            }

            if (embeds.Count == 0)
            {
                embeds.Add(currentEmbedBuilder);
            }

            return embeds;
        }

        public DiscordEmbed AboutMeEmbed()
        {
            var embed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Blurple)
                .WithAuthor("About Me", iconUrl: _client.CurrentUser.GetAvatarUrl(ImageFormat.Auto))
                .WithDescription("*I'm Spyglass, vice admiral of the Remnant Fleet and, in this case, your flexible and extensible moderation bot.*")
                .AddField("Created By", "[Erlite#1337](https://github.com/Erlite/)")
                .AddField("GitHub", "https://github.com/Erlite/Spyglass")
                .AddField("Library", $"DSharpPlus v{_client.VersionString}")
                .WithThumbnail("https://i.imgur.com/Q8PqycD.png");

            return embed.Build();
        }
    }
}