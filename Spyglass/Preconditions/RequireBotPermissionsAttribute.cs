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
    public class RequireBotPermissionsAttribute : SlashCheckBaseAttribute
    {
        public Permissions RequiredPermissions { get; private set; }

        public RequireBotPermissionsAttribute(Permissions perms)
        {
            RequiredPermissions = perms;
        }
        
        public override async Task<bool> ExecuteChecksAsync(InteractionContext ctx)
        {
            var config = ctx.Services.GetRequiredService<ConfigurationService>().GetConfig();
            var embeds = ctx.Services.GetRequiredService<EmbedService>();
            
            if (!ctx.Client.Guilds.ContainsKey(config.MainGuildId))
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .AddEmbed(embeds.Message("There is no main server configured, please use the `/config setmainserver` command first.", DiscordColor.Red)));

                return false;
            }

            var mainGuild = ctx.Client.Guilds[config.MainGuildId];
            var botMember = await mainGuild.GetMemberAsync(ctx.Client.CurrentUser.Id);

            if (!botMember.Permissions.HasPermission(RequiredPermissions))
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .AddEmbed(embeds.Message($"I am missing the follow required permissions: `{RequiredPermissions.ToPermissionString()}`", DiscordColor.Red)));

                return false;
            }

            return true;
        }
    }
}