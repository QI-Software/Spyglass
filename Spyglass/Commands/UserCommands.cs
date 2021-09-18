using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Spyglass.Services;

namespace Spyglass.Commands
{
    public class UserCommands : ApplicationCommandModule
    {
        private readonly EmbedService _embeds;
        private readonly InfractionService _infractions;

        public UserCommands(EmbedService embeds, InfractionService infractions)
        {
            _embeds = embeds;
            _infractions = infractions;
        }
        
        [SlashCommand("userinfo", "Displays information about the given user.")]
        public async Task GetUserInfo(InteractionContext ctx, 
            [Option("user", "The user to get information from")] DiscordUser user = null)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            if (ctx.Channel.IsPrivate)
            {
                user ??= ctx.User;
            }
            else
            {
                user ??= ctx.Member;
            }
            
            int? infractionCount = null;
            
            if (ctx.Member != null && ctx.Member.Permissions.HasPermission(Permissions.ViewAuditLog))
            {
                var infractions = _infractions.GetUserInfractions(user.Id);
                if (infractions.Successful)
                {
                    infractionCount = infractions.Result.Count;
                }
            }
            
            if (user is DiscordMember member)
            {
                var memberInfo = _embeds.MemberEmbed(member, infractionCount: infractionCount);
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(memberInfo));
            }
            else
            {
                var userInfo = _embeds.UserEmbed(user, infractionCount);
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(userInfo));
            }
        }
        
        [ContextMenu(ApplicationCommandType.UserContextMenu, "Show User Info")]
        public async Task ContextShowInfo(ContextMenuContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            int? infractionCount = null;
            
            if (ctx.Member != null && ctx.Member.Permissions.HasPermission(Permissions.ViewAuditLog))
            {
                var infractions = _infractions.GetUserInfractions(ctx.TargetUser.Id);
                if (infractions.Successful)
                {
                    infractionCount = infractions.Result.Count;
                }
            }
            
            if (ctx.TargetMember is not null)
            {
                var memberInfo = _embeds.MemberEmbed(ctx.TargetMember, infractionCount: infractionCount);
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(memberInfo));
            }
            else
            {
                var userInfo = _embeds.UserEmbed(ctx.TargetUser, infractionCount);
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(userInfo));
            }
        }
    }
}