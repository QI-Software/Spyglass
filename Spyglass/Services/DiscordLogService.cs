using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Serilog.Core;
using Spyglass.Database.Moderation;
using Spyglass.Utilities;

namespace Spyglass.Services
{
    public class DiscordLogService
    {
        private readonly ConfigurationService _config;
        private readonly DiscordClient _client;
        private readonly EmbedService _embeds;
        private readonly Logger _log;

        public DiscordLogService(ConfigurationService config, DiscordClient client, EmbedService embeds, Logger log)
        {
            _config = config;
            _client = client;
            _embeds = embeds;
            _log = log;

            _client.GuildMemberAdded += OnMemberJoined;
        }

        public async Task LogInfractionAsync(Infraction infraction)
        {
            if (infraction == null)
            {
                throw new ArgumentNullException(nameof(infraction));
            }

            var config = _config.GetConfig();
            if (config.InfractionLogChannelId != 0)
            {
                var chnl = await DiscordUtils.TryGetChannelAsync(_client, config.InfractionLogChannelId);
                if (chnl != null)
                {
                    var embed = await _embeds.GetInfractionInformationAsync(infraction);
                    await chnl.SendMessageAsync(embed);
                }
            }
        }

        private Task OnMemberJoined(DiscordClient client, GuildMemberAddEventArgs e)
        {
            // if (e.Guild.Id != _config.GetConfig().MainGuildId)
            // {
            //     return;
            // }
            //
            // var channel = e.Guild.GetChannel(_config.GetConfig().JoinChannelId);
            // if (channel != null)
            // {
            //     await channel.SendMessageAsync(embed: _embeds.MemberJoined(e.Member));
            // }

            return Task.CompletedTask;
        }
    }
}