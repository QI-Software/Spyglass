using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Serilog.Core;
using Spyglass.Commands;

namespace Spyglass.Services
{
    public class SlashCommandsHandler
    {
        private readonly DiscordClient _client;
        private readonly Logger _log;
        
        public SlashCommandsHandler(Logger log, DiscordClient client)
        {
            _client = client;
            _log = log;
            
            var slash = _client.GetSlashCommands();
            
            slash.SlashCommandExecuted += (sender, eventArgs) =>
            {
                _log.Information(
                    $"SlashCommands: {eventArgs.Context.User} used command '{eventArgs.Context.CommandName}'.");
                return Task.CompletedTask;
            };

            slash.SlashCommandErrored += (sender, args) =>
            {
                _log.Error(
                    $"SlashCommands: {args.Context.User} failed to use command '{args.Context.CommandName}' with error: {args.Exception.Message}.");
                return Task.CompletedTask;
            };

            var commands = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(ApplicationCommandModule)));
            
            foreach (var t in commands)
            {
                slash.RegisterCommands(t);
            }
        }
    }
}