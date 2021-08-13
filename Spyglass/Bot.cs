using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Spyglass.Database;
using Spyglass.Providers;
using Spyglass.Services;

namespace Spyglass
{
    public class Bot
    {
        private ConfigurationService _config;
        private DiscordClient _client;
        private IServiceProvider _services;
        private Logger _log;
        
        public async Task MainAsync(string[] args)
        {
            if (args.Length < 1 || string.IsNullOrEmpty(args[0]))
            {
                Console.WriteLine("Please specify the config directory's path as the first command line argument.");
                Console.ReadLine();
                Environment.Exit(1);
            }
            
            var connString = string.Empty;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                connString  = Environment.GetEnvironmentVariable("SPYGLASS_DBCONNSTRING", EnvironmentVariableTarget.User)
                              ?? Environment.GetEnvironmentVariable("SPYGLASS_DBCONNSTRING", EnvironmentVariableTarget.Machine);
            }
            else
            {
                connString = Environment.GetEnvironmentVariable("SPYGLASS_DBCONNSTRING", EnvironmentVariableTarget.Process);
            }

            if (string.IsNullOrWhiteSpace(connString))
            {
                Console.WriteLine("Please specify the database connection string as an environment variable.");
                Console.WriteLine("Example: SPYGLASS_DBCONNSTRING=\"Username=username;Password=password;Host=localhost;Port=5432;Database=spyglass;\"");
                Console.ReadLine();
                Environment.Exit(1);
            }
            
            // Create the configuration service.
            _config = new ConfigurationService();
            await _config.InitializeAsync(args[0]);
            var configData = _config.GetConfig();
            
            
            // Create the logger.
            _log = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .CreateLogger();

            Log.Logger = _log;
            
            // Migrate the database if it needs to.
            try
            {
                _log.Information("Spyglass: Checking for pending database migrations...");
                using (var context = new SpyglassContext())
                {
                    var sw = new Stopwatch();
                    sw.Start();
                    var migrations = await context.Database.GetPendingMigrationsAsync();
                    var count = migrations.Count();
                    if (count > 0)
                    {
                        _log.Warning(string.Format(new PluralFormatProvider(), "Spyglass: Database has {0:migration;migrations} pending. Applying.", count));
                        await context.Database.MigrateAsync();
                        _log.Information(string.Format(new PluralFormatProvider(), $"Spyglass: Finished applying {0:migration;migrations} in {sw.ElapsedMilliseconds}ms.", count));
                    }
                    else
                    {
                        _log.Information("Spyglass: No pending database migrations!");
                    }
                }
            }
            catch (Exception e)
            {
                _log.Error(e, $"Spyglass: An error occured while applying database migrations: {e.Message}.");
                _log.Error("Spyglass: The bot cannot continue any further.");
                Console.ReadLine();
                Environment.Exit(1);
            }
            
            // Initialize the Discord client.
            _client = new DiscordClient(new DiscordConfiguration
            {
                Token = _config.GetConfig().Token,
                TokenType = TokenType.Bot,
                LoggerFactory = new LoggerFactory().AddSerilog(),
                Intents = DiscordIntents.All,
            });

            _services = GetServices();
            _client.UseSlashCommands(new SlashCommandsConfiguration
            {
                Services = _services,
            });

            _client.UseInteractivity();
            
            _log.Information("Spyglass: Startup sequence initiated.");
            InitializeServices();

            _client.Ready += async (sender, eventArgs) =>
            {
                var status = _config.GetConfig().PlayingStatus;

                if (string.IsNullOrWhiteSpace(status))
                {
                    await sender.UpdateStatusAsync();
                }
                else
                {
                    await sender.UpdateStatusAsync(new DiscordActivity(status, ActivityType.Playing));
                }
            };
            
            await _client.ConnectAsync();
            await Task.Delay(-1);
        }
        
        /// <summary>
        /// Gets the required service for the bot.
        /// Add new services here, start them if necessary in Bot#InitializeServicesAsync()
        /// </summary>
        /// <returns> The service provider that contains all the services. </returns>
        private IServiceProvider GetServices()
        {
            var collectionBuilder = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_config)
                .AddSingleton(_log)
                .AddSingleton<SlashCommandsHandler>()
                .AddSingleton<InfractionService>()
                .AddSingleton<ModerationsService>()
                .AddSingleton<HttpClient>()
                .AddSingleton<Random>()
                .AddSingleton<EmbedService>()
                .AddSingleton<DiscordLogService>()
                .AddSingleton<ReactionRoleService>()
                .AddSingleton<EntryGateService>()
                .AddSingleton<BlacklistService>()
                .AddSingleton<MailService>()
                .AddSingleton<TagService>();

            return collectionBuilder.BuildServiceProvider();
        }

        private void InitializeServices()
        {
            _services.GetRequiredService<SlashCommandsHandler>();
            _services.GetRequiredService<ModerationsService>().Start();
            _services.GetRequiredService<ReactionRoleService>().Initialize();
            _services.GetRequiredService<EntryGateService>();
            _services.GetRequiredService<BlacklistService>().InvalidateCache();
            _services.GetRequiredService<TagService>().InvalidateCache();
            _services.GetRequiredService<MailService>();
        }
    }
}