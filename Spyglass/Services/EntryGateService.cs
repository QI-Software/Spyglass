using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Serilog.Core;
using Spyglass.Utilities;

namespace Spyglass.Services
{
    public class EntryGateService
    {
        private readonly ConfigurationService _config;
        private readonly Logger _log;
        
        public EntryGateService(ConfigurationService config, DiscordClient client, Logger log)
        {
            _config = config;
            _log = log;
            client.ComponentInteractionCreated += OnComponentInteractionCreated;
        }

        private async Task OnComponentInteractionCreated(DiscordClient sender, ComponentInteractionCreateEventArgs e)
        {
            var config = _config.GetConfig();
            if (!config.EntryGateEnabled || e.Message.Id != config.EntryGateMessageId || e.Id != "EntryGateButton")
            {
                return;
            }

            var role = e.Guild.GetRole(config.EntryGateRoleId);
            if (role == null)
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .WithContent("Uh oh! It seems like the entry gate role does not exist, please contact a staff member for help.")
                    .AsEphemeral(true));
                
                _log.Error("EntryGate: Missing a role, cannot grant access to users!");

                return;
            }
            
            var member = await e.Guild.GetMemberAsync(e.User.Id);
            if (member.Roles.Any(r => r.Id == role.Id))
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .WithContent($"You already have the {role.Name} role!")
                    .AsEphemeral(true));
                
                _log.Warning($"EntryGate: {member.Username}#{member.Discriminator} already has the {role.Name} role.");

                return;
            }

            var self = await e.Guild.GetMemberAsync(sender.CurrentUser.Id);
            if (!DiscordUtils.CanAddRoleToMember(role, self))
            {
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .WithContent($"Uh oh! I cannot give you the {role.Name} role because it is higher than my highest role. Please contact a staff member for help.")
                    .AsEphemeral(true));
                
                _log.Error($"EntryGate: The {role.Name} role is too high, I cannot grant it to {member.Username}#{member.Discriminator}.");

                return;
            }

            try
            {
                await member.GrantRoleAsync(role);
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .WithContent("You now have access to the server!")
                    .AsEphemeral(true));
                
                _log.Information($"EntryGate: Gave access to {member.Username}#{member.Discriminator}.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"EntryGate: Failed to give access to {member.Username}#{member.Discriminator}: {ex.Message}");
                await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder()
                    .WithContent($"Uh oh! An error occurred while giving you the {role.Name} role. Please contact a staff member for help.")
                    .AsEphemeral(true));

                return;
            }
        }
    }
}