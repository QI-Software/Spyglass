using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Spyglass.Preconditions;
using Spyglass.Services;
using Spyglass.Utilities;

namespace Spyglass.Commands
{
    [SlashCommandGroup("infractions", "Get, update or delete a user's infractions.")]
    [RequirePermissions(Permissions.ViewAuditLog)]
    public class InfractionCommands : ApplicationCommandModule
    {
        private readonly EmbedService _embeds;
        private readonly InfractionService _infractionService;

        public InfractionCommands(EmbedService embeds, InfractionService infractions)
        {
            _embeds = embeds;
            _infractionService = infractions;
        }

        [SlashCommand("get", "Get an infraction by its identifier.")]
        public async Task GetInfraction(InteractionContext ctx,
            [Option("id", "The infraction's unique numerical id")] long id)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            
            var result = _infractionService.GetInfractionFromId(id);
            if (result.Successful)
            {
                var embed = await _embeds.GetInfractionInformationAsync(result.Result);

                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(embed));
            }
            else
            {
                await ctx.FollowUpAsync(
                    new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message($"I could not find any infraction with id #{id}", DiscordColor.Red)));
            }
        }

        [SlashCommand("list", "List a user's infractions if any.")]
        public async Task GetUserInfractions(InteractionContext ctx,
            [Option("user", "The user to retrieve infractions for.")] DiscordUser user = null)
        {
            user ??= ctx.User;

            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var query = _infractionService.GetUserInfractions(user);
            
            if (!query.Successful)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message($"Failed to get user infractions: {query.Message}", DiscordColor.Red)));
                return;
            }
            
            if (!query.Result.Any())
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message($"{user.Username}#{user.Discriminator} has no infractions.")));
            }
            else
            {
                var embeds = await _embeds.UserInfractionsEmbeds(ctx.User, user, query.Result);
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbeds(embeds));
            }
        }
        
        [ContextMenu(ApplicationCommandType.UserContextMenu, "List Infractions")]
        public async Task ContextListInfractions(ContextMenuContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var user = ctx.TargetUser;
            var query = _infractionService.GetUserInfractions(user);
            
            if (!query.Successful)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(
                    _embeds.Message($"Failed to get user infractions: {query.Message}", DiscordColor.Red)));
                return;
            }
            
            if (!query.Result.Any())
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message($"{user.Username}#{user.Discriminator} has no infractions.")));
            }
            else
            {
                var embeds = await _embeds.UserInfractionsEmbeds(ctx.User, user, query.Result);
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbeds(embeds));
            }
        }

        [SlashCommand("update", "Update an infraction's reason.")]
        public async Task UpdateUserInfraction(InteractionContext ctx,
            [Option("id", "The id of the infraction to update.")] long id,
            [Option("reason", "The new reason to give the infraction. Leave empty for an interactive prompt.")] string reason = null)
        {
            if (reason != null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
                var result = await _infractionService.UpdateInfractionReasonAsync(id, reason);

                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message(result.Message, 
                    result.Successful ? DiscordColor.Green : DiscordColor.Red)));
                return;
            }

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(_embeds.Message($"Please type in the new reason for infraction #{id} (or 'cancel' to cancel):", 
                    DiscordColor.White)));

            reason = await DiscordUtils.PromptForInputAsync($"Please type in the new reason for infraction #{id} (or 'cancel' to cancel):", ctx);

            if (reason != null)
            {
                var queryResult = await _infractionService.UpdateInfractionReasonAsync(id, reason);
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message(queryResult.Message, 
                    queryResult.Successful ? DiscordColor.Green : DiscordColor.Red)));
            }
        }

        [SlashCommand("delete", "Delete an infraction.")]
        public async Task DeleteUserInfraction(InteractionContext ctx,
            [Option("id", "The id of the infraction to update.")] long id)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
            var result = await _infractionService.RemoveInfractionAsync(id);
            
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                .AddEmbed(_embeds.Message(result.Message, result.Successful ? DiscordColor.Green : DiscordColor.Red)));
        }
    }
}