using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Serilog.Core;
using Spyglass.Preconditions;
using Spyglass.Services;
using Spyglass.Utilities;

namespace Spyglass.Commands
{
    [SlashCommandGroup("tag", "Contains commands pertaining to the tag system.")]
    [RequirePermissions(Permissions.ManageMessages)]
    public class TagCommands : ApplicationCommandModule
    {
        private readonly ConfigurationService _config;
        private readonly EmbedService _embeds;
        private readonly Logger _log;
        private readonly TagService _tags;

        public TagCommands(ConfigurationService config, EmbedService embeds, Logger log, TagService tags)
        {
            _config = config;
            _embeds = embeds;
            _log = log;
            _tags = tags;
        }
        
        [SlashCommand("setprefix", "Set the single character prefix for tags.")]
        public async Task SetPrefix(InteractionContext ctx,
            [Option("prefix", "The single character prefix (default is $)")] string prefix)
        {
            char parsed;
            
            if (string.IsNullOrWhiteSpace(prefix) || !char.TryParse(prefix, out parsed))
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .AddEmbed(_embeds.Message("The prefix must be a valid, non-empty character.", DiscordColor.Red)));

                return;
            }

            var config = _config.GetConfig();
            config.TagPrefix = parsed;
            
            try
            {
                await _config.SaveConfigAsync();
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .AddEmbed(_embeds.Message($"Successfully set the tag prefix to '{parsed}'.", DiscordColor.Green)));
                _log.Information($"{ctx.User} modified the log configuration.");
            }
            catch (Exception e)
            {
                _log.Error(e, "An exception has occurred while modifying the configuration.");
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message("Failed to save configuration, contact the maintainer.", DiscordColor.Red)));
            }
        }

        [SlashCommand("create", "Create a new tag.")]
        public async Task CreateTag(InteractionContext ctx,
            [Option("name", "The name of the tag to create (max 32 characters)")] string name,
            [Option("value", "The value of the tag, leave empty for an interactive prompt")]  string value = null)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            value ??= await DiscordUtils.PromptForInputAsync("Please enter the value of the tag (or 'cancel' to cancel)", ctx, TimeSpan.FromMinutes(5), 1999);
            var query = await _tags.CreateTagAsync(name, value, ctx.User.Id);

            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message(query.Message, query.Successful ? DiscordColor.Green : DiscordColor.Red)));
        }
        
        [SlashCommand("delete", "Delete a tag from its name.")]
        public async Task DeleteTag(InteractionContext ctx,
            [Option("name", "The name of the tag to delete.")] string name) 
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var tag = _tags.CachedTags.FirstOrDefault(t => t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (tag == null)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message($"Could not find a tag named '{name}'.", DiscordColor.Red)));
                return;
            }
            

            var query = await _tags.DeleteTagAsync(tag.Id);
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message(query.Message, query.Successful ? DiscordColor.Green : DiscordColor.Red)));
        }
        
        [SlashCommand("rename", "Change a tag's name.")]
        public async Task RenameTag(InteractionContext ctx,
            [Option("name", "The name of the tag to rename.")] string name,
            [Option("new", "The new name of the tag.")] string renameTo)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var tag = _tags.CachedTags.FirstOrDefault(t => t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (tag == null)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message($"Could not find a tag named '{name}'.", DiscordColor.Red)));
                return;
            }
            

            var query = await _tags.UpdateTagAsync(tag.Id, renameTo);
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message(query.Message, query.Successful ? DiscordColor.Green : DiscordColor.Red)));
        }
        
        [SlashCommand("update", "Change a tag's value.")]
        public async Task UpdateTag(InteractionContext ctx,
            [Option("name", "The name of the tag to rename.")] string name,
            [Option("value", "The new value of the tag, leave empty for an interactive prompt")] string value = null)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            var tag = _tags.CachedTags.FirstOrDefault(t => t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (tag == null)
            {
                await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder()
                    .AddEmbed(_embeds.Message($"Could not find a tag named '{name}'.", DiscordColor.Red)));
                return;
            }
            
            value ??= await DiscordUtils.PromptForInputAsync("Please enter the new value of the tag (or 'cancel' to cancel)", ctx, TimeSpan.FromMinutes(5), 1999);
            
            var query = await _tags.UpdateTagAsync(tag.Id, value: value);
            await ctx.FollowUpAsync(new DiscordFollowupMessageBuilder().AddEmbed(_embeds.Message(query.Message, query.Successful ? DiscordColor.Green : DiscordColor.Red)));
        }

        [SlashCommand("info", "Gives information about the given tag.")]
        public async Task GetTagInformation(InteractionContext ctx,
            [Option("name", "The name of the tag to get information for.")] string name)
        {
            var tag = _tags.CachedTags.FirstOrDefault(t => t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (tag == null)
            {
                await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,  new DiscordInteractionResponseBuilder()
                    .AddEmbed(_embeds.Message($"Could not find a tag named '{name}'.", DiscordColor.Red)));
                return;
            }

            var embed = await _embeds.GetTagInformationAsync(ctx.User, tag);
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                .AddEmbed(embed));
        }

        [SlashCommand("list", "Lists the available tags.")]
        public async Task ListTags(InteractionContext ctx)
        {
            
        }
    }
}