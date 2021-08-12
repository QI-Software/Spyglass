using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Spyglass.Preconditions;
using Spyglass.Services;

namespace Spyglass.Commands
{
    [ContextRequiresPermission(Permissions.ViewAuditLog)]
    public class InfractionContextMenu : ApplicationCommandModule
    {
        private readonly EmbedService _embeds;
        private readonly InfractionService _infractionService;

        public InfractionContextMenu(EmbedService embeds, InfractionService infractions)
        {
            _embeds = embeds;
            _infractionService = infractions;
        }
        
        [ContextMenu(ApplicationCommandType.UserContextMenu, "List Infractions")]
        public async Task ContextListInfractions(ContextMenuContext ctx)
        {
            var user = ctx.TargetUser;
            var query = _infractionService.GetUserInfractions(user);
            
            if (!query.Successful)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder {IsEphemeral = true}.AddEmbed(
                        _embeds.Message($"Failed to get user infractions: {query.Message}", DiscordColor.Red)));
                return;
            }
            
            if (!query.Result.Any())
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder {IsEphemeral = true}.AddEmbed(_embeds.Message($"{user.Username}#{user.Discriminator} has no infractions.")));
            }
            else
            {
                var embeds = await _embeds.UserInfractionsEmbeds(ctx.User, user, query.Result);
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder {IsEphemeral = true}.AddEmbeds(embeds));
            }
        }       
    }
}