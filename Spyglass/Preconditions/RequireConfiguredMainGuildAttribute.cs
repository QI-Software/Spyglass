using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Microsoft.Extensions.DependencyInjection;
using Spyglass.Services;
using Spyglass.Utilities;

namespace Spyglass.Preconditions
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireConfiguredMainGuildAttribute : SlashCheckBaseAttribute
    {
        public bool OnlyRunsOnMainGuild { get; }

        public RequireConfiguredMainGuildAttribute()
        {
            OnlyRunsOnMainGuild = false;
        }

        public RequireConfiguredMainGuildAttribute(bool onlyRunsOnMainGuild)
        {
            OnlyRunsOnMainGuild = onlyRunsOnMainGuild;
        }
        
        public override async Task<bool> ExecuteChecksAsync(InteractionContext ctx)
        {
            var config = ctx.Services.GetRequiredService<ConfigurationService>().GetConfig();
            var client = ctx.Services.GetRequiredService<DiscordClient>();

            var guild = await DiscordUtils.TryGetGuildAsync(client, config.MainGuildId);
            if (guild == null)
            {
                var embeds = ctx.Services.GetRequiredService<EmbedService>();
                var embed = embeds.Message(
                    "There is no main server configured, please use the `/config setmainserver` command first.",
                    DiscordColor.Red);

                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder {IsEphemeral = true}.AddEmbed(embed));

                return false;
            }

            if (OnlyRunsOnMainGuild && ctx.Guild.Id != guild.Id)
            {
                var embeds = ctx.Services.GetRequiredService<EmbedService>();
                var embed = embeds.Message(
                    "This command can only be ran on the main server.",
                    DiscordColor.Red);

                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder {IsEphemeral = true}.AddEmbed(embed));

                return false;
            }
            
            return true;
        }
    }
}