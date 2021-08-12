using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Spyglass.Database.ReactionRoles;
using Spyglass.Services;
using Spyglass.Utilities;

namespace Spyglass.Commands
{
    [SlashCommandGroup("reactionroles", "Contains commands pertaining to the reaction roles service.")]
    [RequirePermissions(Permissions.ManageGuild)]
    public class ReactionRoleCommands : ApplicationCommandModule
    {
        private readonly EmbedService _embeds;
        private readonly ReactionRoleService _reactionService;

        public ReactionRoleCommands(EmbedService embeds, ReactionRoleService reactionService)
        {
            _embeds = embeds;
            _reactionService = reactionService;
        }
        
        [SlashCommand("add", "Add a new reaction role.")]
        public async Task AddReactionRole(InteractionContext ctx,
            [Option("channel", "The channel with the message to add a reaction role to.")] DiscordChannel channel,
            [Option("message", "The ID of the message to add a reaction role to.")] string messageId,
            [Option("emoji", "The emoji to use for the reaction role (ID for custom)")] string emojiName,
            [Option("role", "The role to grant to users that add the reaction.")] DiscordRole role)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            ulong parsedMessageId;
            if (!ulong.TryParse(messageId, out parsedMessageId))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message("An invalid message ID was specified, it must be a number.", DiscordColor.Red)));
                return;
            }

            var message = await DiscordUtils.TryGetMessageAsync(channel, parsedMessageId);
            if (message == null)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message($"Could not a find a message with ID '{messageId}' in {channel.Mention}.", DiscordColor.Red)));
                return;
            }

            ulong? emojiId = null;
            if (ulong.TryParse(emojiName, out ulong id))
            {
                emojiId = id;
            }

            DiscordEmoji parsedEmoji = null;
            if (emojiId != null)
            {
                DiscordEmoji.TryFromGuildEmote(ctx.Client, emojiId.Value, out parsedEmoji);
            }
            else
            {
                if (!DiscordEmoji.TryFromName(ctx.Client, emojiName, out parsedEmoji))
                {
                    DiscordEmoji.TryFromUnicode(ctx.Client, emojiName, out parsedEmoji);
                }
            }
            
            if (parsedEmoji == null)
            {
                if (emojiId != null)
                {
                    await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                        .AddEmbed(_embeds.Message($"Could not a find an emoji from ID '{emojiId.Value}'.", DiscordColor.Red)));
                    return;
                }
                
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message($"Could not a find an emoji with name '{emojiName}'. An example would be 'slight_smile'. Use the ID for custom server emotes.", DiscordColor.Red)));
                return;
            }

            var botMember = await ctx.Guild.GetMemberAsync(ctx.Client.CurrentUser.Id);
            var highestRole = botMember.Roles.OrderByDescending(r => r.Position).FirstOrDefault();

            if (highestRole == null || highestRole.Position <= role.Position)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message($"I cannot give out the role {role.Mention} as it is higher or at the same position as my highest role.", DiscordColor.Red)));
                return;
            }

            var name = emojiId.HasValue ? null : parsedEmoji.Name;
            var reactionRole = new ReactionRole(message.Id, role.Id, emojiId, name);

            var query = await _reactionService.AddReactionRoleAsync(reactionRole);

            if (query.Successful)
            {
                await message.CreateReactionAsync(parsedEmoji);
            }
            
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message(query.Message,
                query.Successful ? DiscordColor.Green : DiscordColor.Red)));
        }

        [SlashCommand("list", "Lists all reaction roles on a specific message.")]
        public async Task ListReactionRoles(InteractionContext ctx, 
            [Option("channel", "The channel for which to list reaction roles.")] DiscordChannel channel,
            [Option("message", "The ID of the message to get the reaction roles for.")] string messageId)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            if (!ulong.TryParse(messageId, out ulong parsedId))
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message("Invalid message ID specified, must be a number.", DiscordColor.Red)));
                return;
            }

            var message = await DiscordUtils.TryGetMessageAsync(channel, parsedId);
            if (message == null)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message($"Could not find a message with ID '{parsedId}' in {channel.Mention}.",
                    DiscordColor.Red)));
                return;
            }

            var roles = _reactionService.GetReactionRoles()
                .Where(r => r.MessageId == parsedId);

            var reactionRoles = roles as ReactionRole[] ?? roles.ToArray();
            if (!reactionRoles.Any())
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message($"Message '{parsedId}' has no reaction roles.",
                    DiscordColor.Green)));
            }
            else
            {
                var embed = new DiscordEmbedBuilder()
                    .WithAuthor($"Reaction Roles for message {parsedId}")
                    .WithColor(DiscordColor.Blurple);

                var sb = new StringBuilder();

                foreach(var role in reactionRoles)
                {
                    if (role.ReactionId != null && DiscordEmoji.TryFromGuildEmote(ctx.Client, parsedId, out var emoji)
                        || role.ReactionName != null && DiscordEmoji.TryFromName(ctx.Client, role.ReactionName, out emoji))
                    {
                        sb.AppendLine($"{emoji.GetDiscordName()} - <@&{role.RoleId}>");
                    }
                    else
                    {
                        sb.AppendLine(role.ReactionId != null ? $"ID: {role.Id} - {role.ReactionId} - <@&{role.RoleId}>" : $"ID: {role.Id} - {role.ReactionName} - <@&{role.RoleId}>");
                    }
                }

                embed.WithDescription(sb.ToString());
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed.Build()));
            }
        }

        [SlashCommand("remove", "Removes a reaction role by ID.")]
        public async Task RemoveReactionRole(InteractionContext ctx,
            [Option("id", "The ID of the reaction role (found using /reactionroles list).")] long id)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var query = await _reactionService.RemoveReactionRoleAsync(id);
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message(query.Message, query.Successful ? DiscordColor.Green : DiscordColor.Red)));
        }
    }
}