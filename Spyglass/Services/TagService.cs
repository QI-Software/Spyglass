using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Serilog.Core;
using Spyglass.Database;
using Spyglass.Database.Tags;
using Spyglass.Providers;
using Spyglass.Utilities;

namespace Spyglass.Services
{
    public class TagService
    {
        private readonly ConfigurationService _config;
        private readonly DiscordClient _client;
        private readonly Logger _log;
        
        private List<Tag> _cachedTags;

        public IReadOnlyList<Tag> CachedTags => _cachedTags;

        public TagService(ConfigurationService config, DiscordClient client, Logger log)
        {
            _config = config;
            _client = client;
            _log = log;

            _client.MessageCreated += OnMessageCreated;
        }

        public void InvalidateCache(SpyglassContext dbContext = null)
        {
            try
            {
                dbContext ??= new SpyglassContext();
                _cachedTags = dbContext.Tags.ToList();
            }
            catch (Exception e)
            {
                _log.Error(e, $"TagService: Failed to invalidate cache with exception: {e.Message}");
            }
            finally
            {
                dbContext?.Dispose();
            }
        }

        public async Task<QueryResult<Tag>> CreateTagAsync(string name, string value, ulong creator)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return QueryResult<Tag>.FromError("Cannot create a tag with a null or empty name.");
            }

            if (name.Length > 32)
            {
                return QueryResult<Tag>.FromError("Cannot create a tag with a name longer than 32 characters.");
            }
            
            if (string.IsNullOrWhiteSpace(value))
            {
                return QueryResult<Tag>.FromError("Cannot create a tag with a null or empty value.");
            }
            
            if (value.Length >= 2000)
            {
                return QueryResult<Tag>.FromError("Tags must be shorter than 2000 characters in length.");
            }

            if (_cachedTags.Any(t => t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)))
            {
                return QueryResult<Tag>.FromError("A tag with the specified name already exists.");
            }

            SpyglassContext dbContext = null;
            try
            {
                dbContext = new SpyglassContext();
                var tag = new Tag(name, creator, value);
                dbContext.Tags.Add(tag);
                await dbContext.SaveChangesAsync();
                InvalidateCache(dbContext);
                
                return QueryResult<Tag>.FromSuccess($"Successfully created tag '{tag.Name}'.", tag);
            }
            catch (Exception e)
            {
                _log.Error(e, $"TagService: Failed to create tag '{name}' with exception: {e.Message}");
                return QueryResult<Tag>.FromError($"Failed to create tag '{name}', please contact a maintainer.");
            }
            finally
            {
                dbContext?.Dispose();
            }
        }
        
        public async Task<QueryResult<Tag>> UpdateTagAsync(long id, string name = null, string value = null)
        {
            if (name != null && name.Length > 32)
            {
                return QueryResult<Tag>.FromError("Cannot update a tag with a name longer than 32 characters.");
            }
            
            if (name != null && _cachedTags.Any(t => t.Id != id && t.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)))
            {
                return QueryResult<Tag>.FromError("A tag with the specified name already exists.");
            }
            
            if (value is { Length: >= 2000 })
            {
                return QueryResult<Tag>.FromError("Tags must be shorter than 2000 characters in length.");
            }
            
            SpyglassContext dbContext = null;
            try
            {
                dbContext = new SpyglassContext();
                var tag = dbContext.Tags.FirstOrDefault(t => t.Id == id);
                
                if (tag == null)
                {
                    return QueryResult<Tag>.FromError($"Could not find any tag with id {id}");
                }

                if (name != null)
                {
                    tag.Name = name;
                }

                if (value != null)
                {
                    tag.Value = value;
                }

                dbContext.Tags.Update(tag);
                await dbContext.SaveChangesAsync();
                InvalidateCache(dbContext);
                
                return QueryResult<Tag>.FromSuccess($"Successfully updated tag '{tag.Name}'.", tag);
            }
            catch (Exception e)
            {
                _log.Error(e, $"TagService: Failed to update tag {id} with exception: {e.Message}");
                return QueryResult<Tag>.FromError($"Failed to update tag {id}, please contact a maintainer.");
            }
            finally
            {
                dbContext?.Dispose();
            }
        }
        
        public async Task<QueryResult> DeleteTagAsync(long id)
        {
            if (_cachedTags.All(t => t.Id != id))
            {
                return QueryResult.FromError($"Could not find any tag with id {id}");
            }
            
            SpyglassContext dbContext = null;
            try
            {
                dbContext = new SpyglassContext();

                var tag = dbContext.Tags.FirstOrDefault(t => t.Id == id);
                if (tag == null)
                {
                    return QueryResult.FromError($"Could not find any tag with id {id}");
                }

                dbContext.Tags.Remove(tag);
                await dbContext.SaveChangesAsync();
                InvalidateCache(dbContext);
                
                return QueryResult.FromSuccess($"Successfully removed tag '{tag.Name}'.");
            }
            catch (Exception e)
            {
                _log.Error(e, $"TagService: Failed to remove tag {id} with exception: {e.Message}");
                return QueryResult.FromError($"Failed to remove tag {id}, please contact a maintainer.");
            }
            finally
            {
                dbContext?.Dispose();
            }
        }

        /// <summary>
        /// Increments the uses of a tag by 1.
        /// </summary>
        /// <param name="id"> The id of the tag to update. </param>
        /// <returns> Whether or not the query succeeded. </returns>
        public async Task<QueryResult> UpdateTagUsesAsync(long id)
        {
            if (_cachedTags.All(t => t.Id != id))
            {
                return QueryResult.FromError($"Could not find any tag with id {id}");
            }

            SpyglassContext dbContext = null;
            try
            {
                dbContext = new SpyglassContext();
                var tag = dbContext.Tags.FirstOrDefault(t => t.Id == id);

                if (tag == null)
                {
                    return QueryResult.FromError($"Could not find any tag with id {id}");
                }

                tag.Uses++;
                dbContext.Tags.Update(tag);
                await dbContext.SaveChangesAsync();
                InvalidateCache(dbContext);
                return QueryResult.FromSuccess("Successfully updated the tag's uses.");
            }
            catch (Exception e)
            {
                _log.Error(e, $"TagService: Failed to update uses of tag {id} with exception: {e.Message}");
                return QueryResult.FromError($"Failed to update uses of tag {id} with exception: {e.Message}");
            }
            finally
            {
                dbContext?.Dispose();
            }
        }

        private async Task OnMessageCreated(DiscordClient sender, MessageCreateEventArgs e)
        {
            if (_cachedTags.Count == 0 || string.IsNullOrWhiteSpace(e.Message.Content))
            {
                return;
            }

            var config = _config.GetConfig();
            if (!config.TagPrefix.HasValue)
            {
                return;
            }

            var regex = new Regex(@$"(?<!\w)\{config.TagPrefix.Value}(\w+)");

            var matches = regex.Matches(e.Message.Content);
            var results = matches
                .Select(m => _cachedTags.FirstOrDefault(t => t.Name.Equals(m.Groups[1].Value, StringComparison.CurrentCultureIgnoreCase)))
                .Where(t => t is not null)
                .Distinct()
                .Take(2);

            foreach (var tag in results)
            {
                _ = UpdateTagUsesAsync(tag.Id);
                await e.Message.RespondAsync(tag.Value);
            }
        }
    }
}