using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Serilog.Core;
using Spyglass.Database.Moderation;
using Spyglass.Services.Models;
using Spyglass.Utilities;

namespace Spyglass.Services
{
    public class MailService
    {
        private readonly BlacklistService _blacklist;
        private readonly ConfigurationService _config;
        private readonly DiscordClient _client;
        private readonly EmbedService _embeds;
        private readonly HttpClient _httpClient;
        private readonly InfractionService _infractions;
        private readonly Logger _log;
        
        private readonly List<MailSession> _sessions;
        
        public MailService(BlacklistService blacklist, ConfigurationService config, DiscordClient client, EmbedService embeds, HttpClient httpClient, 
            InfractionService infractions, Logger log)
        {
            _blacklist = blacklist;
            _config = config;
            _client = client;
            _embeds = embeds;
            _httpClient = httpClient;
            _infractions = infractions;
            _log = log;
                        
            // Initialize the sessions.
            _sessions = new List<MailSession>();
            
            // When the bot finishes downloading guild data, grab existing mail channels and store them.
            _client.GuildAvailable += OnGuildAvailable;
            
            // Used to handle relaying DM messages.
            _client.MessageCreated += HandlePrivateMessageReceivedAsync;
            
            // Used to handle relaying ModMail channel messages.
            _client.MessageCreated += HandleModMailChannelMessageReceivedAsync;
            _client.ChannelDeleted += HandleModMailChannelDeletedAsync;
        }

        private Task OnGuildAvailable(DiscordClient sender, GuildCreateEventArgs e)
        {
            var config = _config.GetConfig();
            if (config is {ModMailEnabled: true})
            {
                if (e.Guild.Id == config.ModMailServerId)
                {
                    _ = RecoverSessionsAsync();
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Check if a user has an active session.
        /// </summary>
        /// <param name="userId"> The user to search for. </param>
        public bool HasSession(ulong userId)
        {
            return _sessions.Any(s => s.MailUserId == userId);
        }

        /// <summary>
        /// Get a user's session if they have one.
        /// </summary>
        /// <param name="userId"> The user to grab the session for. </param>
        /// <exception cref="InvalidOperationException"> Thrown if the user does not have any active session. </exception>
        public MailSession GetSession(ulong userId)
        {
            if (!HasSession(userId))
            {
                throw new InvalidOperationException($"User {userId} does not have an active session.");
            }

            return _sessions.First(s => s.MailUserId == userId);
        }

        /// <summary>
        /// Check if the specified channel ID is linked to any session.
        /// </summary>
        /// <param name="channelId"> The ID to check for. </param>
        public bool IsChannelLinkedToSession(ulong channelId)
        {
            return _sessions.Any(s => s.MailChannelId == channelId);
        }

        /// <summary>
        /// Get a mail session from a channel ID.
        /// </summary>
        /// <param name="channelId"> The channel ID for which to retrieve the mail session. </param>
        /// <returns> The session or null if none. </returns>
        public MailSession GetSessionFromChannel(ulong channelId)
        {
            if (!IsChannelLinkedToSession(channelId)) return null;

            return _sessions.First(s => s.MailChannelId == channelId);
        }
        
        /// <summary>
        /// Recover existing sessions with their existing channels if any.
        /// </summary>
        /// <exception cref="ArgumentNullException"> Thrown if the ConfigurationService hasn't finished loading yet. </exception>
        public async Task RecoverSessionsAsync()
        {
            if (_config.GetConfig() == null) throw new ArgumentNullException(nameof(_config));
            var config = _config.GetConfig();

            _log.Information("ModMail: Recovering sessions...");

            // Grab the main server for verification.
            var modMailGuild = await _client.GetGuildAsync(config.ModMailServerId);

            // Get the channels inside the ModMail server.
            var validChannels = modMailGuild.Channels.Values;
            
            // Get the channels IDs that correspond to a modmail session and the user each belongs to.
            // A modmail session channel's name is the user ID (ulong) to which it belongs.
            var sessionIds = GetSessionIdentifiers(validChannels);
            
            // Construct sessions if they don't exist yet.
            // On bot startup, none will exist, but if the bot reconnects and fails to continue the previous session, 
            // the Ready event will fire again and call this method, meaning some sessions might still exist.
            foreach (var channelId in sessionIds.Keys)
            {
                // Continue if the session already exists.
                if (HasSession(sessionIds[channelId]))
                {
                    _log.Information($"ModMail: Found existing session for user {sessionIds[channelId]}.");
                    continue;
                }
                
                _log.Information($"ModMail: Creating new session for user {sessionIds[channelId]} with existing channel id {channelId}");
                await CreateSessionAsync(channelId, sessionIds[channelId]);
            }
        }
        
        /// <summary>
        /// Handle relaying private messages to their mail sessions.
        /// </summary>
        private async Task HandlePrivateMessageReceivedAsync(DiscordClient client, MessageCreateEventArgs args)
        {
            if (!_config.GetConfig().ModMailEnabled)
            {
                return;
            }
            
            var message = args.Message;
            
            // Check if the message originated from a user.
            if (args.Author.IsBot) return;
            
            // Check if the message was received in a public context.
            if (!message.Channel.IsPrivate) return;
            
            // Check if the message starts with / 
            // Ignore it if so.
            if (args.Message.Content.StartsWith("/")) return;

            MailSession session = null;
            
            var mailGuild = await _client.GetGuildAsync(_config.GetConfig().ModMailServerId);
            if (mailGuild == null)
            {
                return;
            }

            var self = await DiscordUtils.TryGetMemberAsync(mailGuild, _client.CurrentUser.Id);
            var requiredPermissions = Permissions.ManageChannels | Permissions.SendMessages | Permissions.AddReactions;
            
            if (!self.Permissions.HasPermission(Permissions.ManageChannels | Permissions.SendMessages))
            {
                _ = args.Channel.SendMessageAsync(
                    $"Uh oh! There was an issue processing your request, please send this to a staff member: missing required permissions on the mail server ({requiredPermissions.ToPermissionString()})");

                return;
            }
            
            // If the user doesn't have a session, create it.
            if (!HasSession(args.Author.Id))
            {
                // Check if the user's blacklist.
                if (_blacklist.IsBlacklisted(BlacklistType.ModMail, args.Author).Result)
                {
                    _log.Warning($"ModMail: Rejected mail session from blacklisted user {args.Author.Id}.");
                    return;
                }
                
                // Check that there's enough space in the unanswered mail.
                var unanswered = await _client.GetChannelAsync(_config.GetConfig().ModMailUnansweredCategoryId);

                if (unanswered != null)
                {
                    if (mailGuild.Channels.Values.Count(c => c.Parent != null && c.ParentId == unanswered.Id) == 50)
                    {
                        await args.Channel.SendMessageAsync(_embeds.Message(
                            "We are currently experiencing high volumes of moderation mail, please try again later.",
                            DiscordColor.Red));
                        return;
                    }
                }
                
                session = await CreateSessionAsync(args.Author.Id);
                var mainGuild = await DiscordUtils.TryGetGuildAsync(_client, _config.GetConfig().MainGuildId);
                var user = mainGuild != null ? await DiscordUtils.TryGetMemberAsync(mainGuild, args.Author.Id) : null;
                
                if (user != null)
                {
                    _ = user.SendMessageAsync("Thank you for contacting us, we will reply shortly.");
                }
            }
            else // retrieve it
            {
                session = GetSession(args.Author.Id);
            }
            
            // Get the mail channel in order to relay the message there.
            var modmailGuild = await DiscordUtils.TryGetGuildAsync(_client, _config.GetConfig().ModMailServerId);

            if (modmailGuild == null) return;
            var mailChannel = await DiscordUtils.TryGetChannelAsync(_client, session.MailChannelId);

            // Relay the message through the session's webhook to simulate a normal user.
            // However, attachments cannot be sent through by webhooks (only one, but D#+ doesn't support it yet anyway).
            if (!string.IsNullOrEmpty(message.Content))
            {
                if (message.Content.Length >= 2000)
                {
                    await args.Channel.SendMessageAsync("Warning: your message wasn't relayed as it was too long (must be shorter than 2000 characters).");
                    return;
                }
                
                await BroadcastHookAsync(session, args.Author, args.Message);
            }
            
            // Create the streams for each attachment if any and send them in bulk.
            if (message.Attachments.Any())
            {
                var fileStreams = new Dictionary<string, Stream>();

                foreach (var attachment in message.Attachments)
                {
                    var stream = await _httpClient.GetStreamAsync(attachment.Url);
                    fileStreams.Add(attachment.FileName, stream);
                }
                
                // Send the message and each attachments.
                var builder = new DiscordMessageBuilder()
                    .WithContent("**Attachments received:**")
                    .WithFiles(fileStreams);
                
                await mailChannel.SendMessageAsync(builder);
            }
        }
        
        /// <summary>
        /// Handle relaying messages from the mail channels to their private channels.
        /// </summary>
        private async Task HandleModMailChannelMessageReceivedAsync(DiscordClient client, MessageCreateEventArgs args)
        {
            if (!_config.GetConfig().ModMailEnabled)
            {
                return;
            }
            
            var author = args.Author;
            // Ignore if the message was from a bot.
            if (author.IsBot) return;
            
            // Check if the message starts with / 
            // Ignore it if so.
            if (args.Message.Content.StartsWith("/")) return;

            var session = _sessions.FirstOrDefault(s => s.MailChannelId == args.Channel.Id);
            // Ignore if the message wasn't sent in a channel linked to a MailSession.
            if (session == null) return;
            
            // Move this channel to the answered category if it isn't there.
            var channel = args.Channel;
            if (channel.Parent != null && channel.ParentId == _config.GetConfig().ModMailUnansweredCategoryId)
            {
                var answered = args.Guild.GetChannel(_config.GetConfig().ModMailAnsweredCategoryId);
                if (answered != null)
                {
                    await channel.ModifyAsync(c => c.Parent = answered);
                }
            }

            // Get the user from the main server.
            // This cannot be done otherwise.
            var mainGuild = await _client.GetGuildAsync(_config.GetConfig().MainGuildId);
            var user = await DiscordUtils.TryGetMemberAsync(mainGuild, session.MailUserId);

            if (user == null)
            {
                await args.Message.CreateReactionAsync(DiscordEmoji.FromUnicode("🛑"));
                return;
            }
            
            // Relay the message, with attachments if any.
            var message = args.Message;

            if (message.Attachments.Any())
            {
                var fileStreams = new Dictionary<string, Stream>();

                foreach (var attachment in message.Attachments)
                {
                    var stream = await _httpClient.GetStreamAsync(attachment.Url);
                    fileStreams.Add(attachment.FileName, stream);
                }
                
                // Send the message and each attachments.
                try
                {
                    var builder = new DiscordMessageBuilder()
                        .WithContent(string.IsNullOrEmpty(message.Content)
                            ? $"{author.Username}#{author.Discriminator}: **Attachments Received**"
                            : $"{author.Username}#{author.Discriminator}: {message.Content}")
                        .WithFiles(fileStreams);
                    
                    await user.SendMessageAsync(builder);
                    _ = args.Message.CreateReactionAsync(DiscordEmoji.FromUnicode("📨"));
                }
                catch (Exception)
                {
                    _ = args.Message.CreateReactionAsync(DiscordEmoji.FromUnicode("🛑"));
                }
            }
            else
            {
                var formattedMessage = $"**{author.Username}#{author.Discriminator}:** {message.Content}";

                if (formattedMessage.Length > 2000)
                {
                    await channel.SendMessageAsync(embed: _embeds.Message("**Warning: your message was not sent as it was too long. Please make it shorter.**", DiscordColor.Red));
                    return;
                }

                var dmMessage = await DiscordUtils.TryMessageUserAsync(user, new DiscordMessageBuilder().WithContent(formattedMessage));
                if (dmMessage != null)
                {
                    _ = args.Message.CreateReactionAsync(DiscordEmoji.FromUnicode("📨"));
                }
                else
                {
                    _ = args.Message.CreateReactionAsync(DiscordEmoji.FromUnicode("🛑"));
                }
            }
        }
        
        private async Task HandleModMailChannelDeletedAsync(DiscordClient client, ChannelDeleteEventArgs e)
        {
            if (!_config.GetConfig().ModMailEnabled)
            {
                return;
            }
            
            var chnl = e.Channel;
            var guild = e.Guild;

            if (e.Guild?.Id != _config.GetConfig().ModMailServerId)
            {
                return;
            }

            var session = GetSessionFromChannel(e.Channel.Id);
            if (session == null) return;

            var query = _blacklist.IsBlacklisted(BlacklistType.ModMail, session.MailUserId);
            if (query.Successful && !query.Result && e.Channel.Parent != null 
                && e.Channel.ParentId != _config.GetConfig().ModMailUnansweredCategoryId)
            {
                var mainGuild = await DiscordUtils.TryGetGuildAsync(_client, _config.GetConfig().MainGuildId);
                if (mainGuild != null)
                {
                    var sessionUser = await DiscordUtils.TryGetMemberAsync(mainGuild, session.MailUserId);
                    if (sessionUser != null)
                    {
                        var builder = new DiscordMessageBuilder()
                            .WithContent("This moderation mail session is now closed, thank you for contacting us!");
                        
                        await DiscordUtils.TryMessageUserAsync(sessionUser, builder);
                    }
                }

            }
            
            await CloseMailSessionAsync(session);
        }
        
        private static async Task BroadcastHookAsync(MailSession session, DiscordUser author, DiscordMessage message)
        {
            var client = session.Webhook;

            // Broadcast the message.
            var webhook = new DiscordWebhookBuilder()
                .WithContent(message.Content)
                .WithUsername(author.Username)
                .WithAvatarUrl(author.GetAvatarUrl(ImageFormat.Png));

            await client.ExecuteAsync(webhook);
            await message.CreateReactionAsync(DiscordEmoji.FromUnicode("📨"));
        }

        /// <summary>
        /// Get modmail sessions from a collection of channels.
        /// </summary>
        /// <param name="channels"></param>
        /// <returns> Dictionary(ulong channelId, ulong userId) </returns>
        private static Dictionary<ulong, ulong> GetSessionIdentifiers(IEnumerable<DiscordChannel> channels)
        {
            var dictionary = new Dictionary<ulong, ulong>();
            
            foreach (var channel in channels)
            {
                if (!ulong.TryParse(channel.Topic, out var userId)) continue;
                
                dictionary.Add(channel.Id, userId);
            }

            return dictionary;
        }

        /// <summary>
        /// Create a session with a mail channel for a specified user.
        /// </summary>
        /// <param name="userId"> The user to create the session for. </param>
        /// <exception cref="InvalidOperationException"> Called if a session already exists for the specified user. </exception>
        public async Task<MailSession> CreateSessionAsync(ulong userId)
        {
            // Throw an error if a session already exists for the user.
            if (HasSession(userId))
            {
                _log.Error($"ModMail: Cannot create a session for {userId}: a session for this user already exists.");
                throw new InvalidOperationException($"User {userId} already has a session opened.");
            }
            
            // Create a channel for the modmail session.
            var channel = await CreateModMailChannelAsync(userId);
            // Create the webhook for the channel and its client.
            var webClient = await GetOrCreateWebhookAsync(channel);
            // Create the session itself.
            var session = new MailSession(channel.Id, userId, webClient);
            // Add it to the tracked sessions.
            _sessions.Add(session);
            
            // Send member and infraction data if any.
            var mainGuild = await DiscordUtils.TryGetGuildAsync(_client, _config.GetConfig().MainGuildId);
            if (mainGuild == null) return session;

            var member = await DiscordUtils.TryGetMemberAsync(mainGuild, userId);
            if (member == null) return session;

            var memberInfoEmbed = _embeds.MemberEmbed(member, false);
            var memberInfoHook = new DiscordWebhookBuilder()
                .AddEmbed(memberInfoEmbed)
                .WithUsername(_client.CurrentUser.Username)
                .WithAvatarUrl(_client.CurrentUser.GetAvatarUrl(ImageFormat.Auto));

            await webClient.ExecuteAsync(memberInfoHook);

            var infractions = _infractions.GetUserInfractions(userId);
            if (infractions.Successful && infractions.Result.Count > 0)
            {
                var embeds = await _embeds.UserInfractionsEmbeds(_client.CurrentUser, member, infractions.Result);
                var infractionHook = new DiscordWebhookBuilder()
                    .AddEmbeds(embeds)
                    .WithUsername(_client.CurrentUser.Username)
                    .WithAvatarUrl(_client.CurrentUser.GetAvatarUrl(ImageFormat.Auto));

                await webClient.ExecuteAsync(infractionHook);
            }
            
            return session;
        }
       
        /// <summary>
        /// Create a session from an existing channel.
        /// </summary>
        /// <param name="channelId"> The Discord ID of the existing channel to use. </param>
        /// <param name="userId"> The user to bind to that channel. </param>
        private async Task<MailSession> CreateSessionAsync(ulong channelId, ulong userId)
        {
            var modMailServerId = await _client.GetGuildAsync(_config.GetConfig().ModMailServerId);
            
            // Check if the channel does exist first.
            // If not, create it.
            if (modMailServerId.Channels.Values.All(channel => channel.Id != channelId))
            {
                _log.Error($"ModMail: Attempted to create session from non existing channel {channelId}. A new channel will be created.");
                var channel = await CreateModMailChannelAsync(userId);
                channelId = channel.Id;
            }
            
            // Get the channel.
            var mChannel = modMailServerId.GetChannel(channelId);
            // Get or create the webhook and webhook client for that session.
            var webClient = await GetOrCreateWebhookAsync(mChannel);
            // Create the session itself.
            var session = new MailSession(channelId, userId, webClient);
            // Add it to the tracked sessions.
            _sessions.Add(session);

            return session;
        }
        
        /// <summary>
        /// Get a channel's existing relay hook or create it. Then return a client for that hook.
        /// </summary>
        /// <param name="channel"> The channel to get or create the hook in. </param>
        private async Task<DiscordWebhook> GetOrCreateWebhookAsync(DiscordChannel channel)
        {
            DiscordWebhook hook;
            
            // Get the existing webhook if any and set it.
            var existingWebhooks = await channel.GetWebhooksAsync();
            if (existingWebhooks.Any())
            {
                hook = existingWebhooks[0];
            }
            else // Else create it.
            {
                hook = await channel.CreateWebhookAsync("Relay Hook");
            }
            
            return hook;
        }

        private async Task<DiscordChannel> CreateModMailChannelAsync(ulong userId)
        {
            // Get the main guild and the user.
            // The user will be null if it can't find him for some reason.
            var mainGuild = await _client.GetGuildAsync(_config.GetConfig().MainGuildId);
            var modMailGuild = await _client.GetGuildAsync(_config.GetConfig().ModMailServerId);
            var user = await mainGuild.GetMemberAsync(userId);
            var modMailCategory = modMailGuild.GetChannel(_config.GetConfig().ModMailUnansweredCategoryId);
            
            // Create the channel, place it in the ModMail category.
            var channel = await modMailGuild.CreateChannelAsync($"{user.Username}-{user.Discriminator}", ChannelType.Text, modMailCategory);
            // Set the topic with information about the user. 
            await channel.ModifyAsync(m => 
            { 
                m.Topic = userId.ToString();
            });
            
            _log.Information($"ModMail: Created new modmail channel with ID {channel.Id} for user {userId.ToString()}");
            return channel;
        }

        public async Task CloseMailSessionAsync(MailSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            
            await CloseMailSessionAsync(session.MailUserId);
        }

        public async Task CloseMailSessionAsync(ulong userId)
        {
            if (!HasSession(userId))
                throw new InvalidOperationException($"No mail sessions linked to user {userId} have been found.");
            
            // Get the session.
            var session = GetSession(userId);
            
            // Remove the session from the list of tracked sessions.
            _sessions.Remove(session);
            
            // Delete the channel from the Discord server.
            var channelModMail = (await _client.GetGuildAsync(_config.GetConfig().ModMailServerId))
                .GetChannel(session.MailChannelId);

            if (channelModMail != null)
            {
                await channelModMail.DeleteAsync("MailSession ended.");
            }
            
            _log.Information($"ModMail: Closed session for user {session.MailUserId}.");
        }
    }
}