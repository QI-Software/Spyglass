using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;
using Spyglass.Preconditions;
using Spyglass.Services;

namespace Spyglass.Commands
{
    public class UtilityCommands : ApplicationCommandModule
    {
        private readonly EmbedService _embeds;

        public UtilityCommands(EmbedService embeds)
        {
            _embeds = embeds;
        }

        [SlashCommand("avatar", "Sends the avatar of the specified user (or yourself)")]
        public async Task GetAvatar(InteractionContext ctx,
            [Option("member", "The user to get the avatar from.")] DiscordUser user = null)
        {
            user ??= ctx.User;

            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, 
                new DiscordInteractionResponseBuilder().AddEmbed(_embeds.AvatarEmbed(ctx.User, user)));
        }
        
        [SlashCommand("echo", "Send a message to the specified channel.")]
        [SlashRequireGuild]
        [RequirePermissions(Permissions.ManageMessages)]
        public async Task EchoMessage(InteractionContext ctx, 
            [Option("channel", "The channel to send the message to")] DiscordChannel channel,
            [Option("message", "The message to send")] string message)
        {
            var interaction = new DiscordInteractionResponseBuilder {IsEphemeral = true}.WithContent("Message sent successfully.");
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, interaction);
            await channel.SendMessageAsync(message);
        }
        
        [SlashCommand("ping", "Check the latency of the Discord gateway.")]
        public async Task CheckPing(InteractionContext ctx)
        {
            var interaction = new DiscordInteractionResponseBuilder().AddEmbed(_embeds.Message($"Pong: websocket round trip time is {ctx.Client.Ping} milliseconds."));
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, interaction);
        }

        [SlashCommand("about", "Learn more about me.")]
        public async Task AboutMessage(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().AddEmbed(_embeds.AboutMeEmbed()));
        }
    }
}