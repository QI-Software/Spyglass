using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Spyglass.Database.Moderation;
using Spyglass.Preconditions;
using Spyglass.Services;
using Spyglass.Utilities;

namespace Spyglass.Commands
{
    [SlashCommandGroup("blacklist", "Contains commands pertaining to blacklisting users from certain parts of the bot.")]
    [RequirePermissions(Permissions.ViewAuditLog)]
    public class BlacklistCommands : ApplicationCommandModule
    {
        private readonly BlacklistService _blacklists;
        private readonly EmbedService _embeds;

        public BlacklistCommands(BlacklistService blacklists, EmbedService embed)
        {
            _blacklists = blacklists;
            _embeds = embed;
        }

        [SlashCommand("add", "Adds a user to the blacklist")]
        public async Task BlacklistUser(InteractionContext ctx,
            [Option("user", "The user to add to the bot's blacklist.")] DiscordUser user,
            [Option("type", "The type of blacklist to add the user to.")] BlacklistType type,
            [Option("reason", "The reason why the user was blacklisted.")] string reason = null)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            
            reason ??= await DiscordUtils.PromptForInputAsync("Enter the reason for blacklisting the user (or 'cancel' to cancel):", ctx);
            
            var result = await _blacklists.AddUserToBlacklistAsync(type, user, ctx.User, reason);
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message(result.Message,
                result.Result ? DiscordColor.Green : DiscordColor.Red)));
        }
        
        [SlashCommand("remove", "Removes a user from the blacklist")]
        public async Task RemoveUser(InteractionContext ctx,
            [Option("user", "The user to remove from the bot's blacklist.")] DiscordUser user,
            [Option("type", "The type of blacklist to remove the user from.")] BlacklistType type)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            
            var result = await _blacklists.RemoveUserFromBlacklistAsync(type, user);
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message(result.Message,
                result.Result ? DiscordColor.Green : DiscordColor.Red)));
        }

        [SlashCommand("info", "Returns information about where a user is blacklisted.")]
        public async Task GetUserBlacklists(InteractionContext ctx,
            [Option("user", "The user to get information for.")] DiscordUser user)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var blacklistQuery = _blacklists.GetUserBlacklistInformation(user);

            if (!blacklistQuery.Successful)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message(blacklistQuery.Message, DiscordColor.Red)));
                return;
;            }

            var blacklists = blacklistQuery.Result;
            if (blacklists.Count == 0)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message($"{user.Username}#{user.Discriminator} is not blacklisted.", DiscordColor.Green)));
                return;
            }

            var embeds = await _embeds.UserBlacklistsEmbedsAsync(ctx.User, user, blacklists);

            var builder = new DiscordFollowupMessageBuilder()
                .AddEmbeds(embeds);

            await ctx.FollowUpAsync(builder);
        }
    }
}