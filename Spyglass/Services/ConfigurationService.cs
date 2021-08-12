using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Spyglass.Services.Models;

namespace Spyglass.Services
{
    public class ConfigurationService
    {
        private ConfigurationModel _config;
        private string _configFilePath;
        private string _configDirPath;

        public string ConfigFilePath => _configFilePath;
        
        public string ConfigDirPath => _configDirPath;

        public ConfigurationService() {}

        public async Task InitializeAsync(string configPath)
        {
            var configFilePath = Path.Combine(configPath, "config.json");

            if (!Directory.Exists(configPath))
            {
                Directory.CreateDirectory(configPath);
            }

            if (!File.Exists(configFilePath))
            {
                Console.WriteLine($"No configuration file found at {configPath}. Do you want to generate one? (y/n)");
                var result = Console.ReadLine();

                // Check for y/yes, everything else will be considered a no.
                if (result != null && (result.ToLower() == "y" || result.ToLower() == "yes"))
                {
                    // Generate the config and serialize default values for QoL.
                    var config = new ConfigurationModel();
                    var data = JsonConvert.SerializeObject(config, Formatting.Indented, new JsonSerializerSettings
                    {
                        DefaultValueHandling = DefaultValueHandling.Include
                    });

                    // Write it to a file and we're done.
                    await File.WriteAllTextAsync(configFilePath, data);

                    Console.WriteLine("Configuration file written. You must configure the bot before running it. Press enter to quit.");
                    Console.ReadLine();
                }
                
                // The user will still need to configure this so we might as well just exit.
                Environment.Exit(0);
            }

            _configFilePath = configFilePath;
            _configDirPath = configPath;
            await LoadConfigAsync();
            
            // Re-save the configuration in case new configuration model keys were added.
            var json = JsonConvert.SerializeObject(_config, new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Include,
                Formatting = Formatting.Indented,
            });
            
            await File.WriteAllTextAsync(configFilePath, json);
        }

        public async Task LoadConfigAsync()
        {
            if (string.IsNullOrEmpty(_configFilePath))
            {
                throw new FileNotFoundException("No set config path to load the config from!");
            }

            var json = await File.ReadAllTextAsync(_configFilePath);
            _config = JsonConvert.DeserializeObject<ConfigurationModel>(json, new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Populate
            });
        }

        public async Task SaveConfigAsync()
        {
            if (string.IsNullOrEmpty(_configFilePath))
            {
                throw new FileNotFoundException("No set config path to save the config to!");
            }

            var json = JsonConvert.SerializeObject(_config, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                DefaultValueHandling = DefaultValueHandling.Include
            });

            await File.WriteAllTextAsync(_configFilePath, json);
        }

        public ConfigurationModel GetConfig()
        {
            return _config;
        }
    }
}