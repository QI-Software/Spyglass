using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.DependencyInjection;
using Spyglass.Services;

namespace Spyglass.Preconditions
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ContextRequiresPermissionAttribute : ContextMenuCheckBaseAttribute
    {
        public Permissions RequiredPermissions { get; }
            
        public ContextRequiresPermissionAttribute(Permissions perms)
        {
            RequiredPermissions = perms;
        }
            
        public override async Task<bool> ExecuteChecksAsync(ContextMenuContext ctx)
        {
            var embeds = ctx.Services.GetRequiredService<EmbedService>();
            
            if (ctx.Guild == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder()
                        .AddEmbed(embeds.Message("This command cannot be ran in a direct message.", DiscordColor.Red)));

                return false;
            }

            if (ctx.Member.Permissions.HasPermission(RequiredPermissions))
            {
                return true;
            }
            
            var response = new DiscordInteractionResponseBuilder()
                .AddEmbed(embeds.Message($"This command requires the following permissions: `{RequiredPermissions.ToPermissionString()}`", DiscordColor.Red));

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, response.AsEphemeral(true));
            return false;
        }
    }
}