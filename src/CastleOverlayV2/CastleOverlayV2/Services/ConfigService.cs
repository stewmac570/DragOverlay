using System;
using System.IO;
using Newtonsoft.Json;
using CastleOverlayV2.Models;

namespace CastleOverlayV2.Services
{
    /// <summary>
    /// Handles loading and saving the user's config.json.
    /// </summary>
    public class ConfigService
    {
        private readonly string _configFilePath;
        private Config _config;

        /// <summary>
        /// Current in-memory Config.
        /// </summary>
        public Config Config => _config;

        /// <summary>
        /// Initializes the ConfigService with the default config file path.
        /// </summary>
        public ConfigService()
        {
            // Relative to your working directory: /Config/config.json
            _configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "config.json");
            Load();
        }

        /// <summary>
        /// Loads config.json into memory.
        /// Creates a new config if the file does not exist.
        /// </summary>
        public void Load()
        {
            if (File.Exists(_configFilePath))
            {
                string json = File.ReadAllText(_configFilePath);
                _config = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
            }
            else
            {
                _config = new Config();
                Save(); // Create initial file if missing
            }
        }

        /// <summary>
        /// Saves the current config state back to config.json.
        /// </summary>
        public void Save()
        {
            string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            string directory = Path.GetDirectoryName(_configFilePath);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_configFilePath, json);
        }

        /// <summary>
        /// Updates the ON/OFF state for a specific channel, then saves.
        /// </summary>
        public void SetChannelVisibility(string channelName, bool isVisible)
        {
            if (_config.ChannelVisibility.ContainsKey(channelName))
            {
                _config.ChannelVisibility[channelName] = isVisible;
            }
            else
            {
                _config.ChannelVisibility.Add(channelName, isVisible);
            }

            Save();
        }

        /// <summary>
        /// Updates the launch point alignment threshold, then saves.
        /// </summary>
        public void SetAlignmentThreshold(double threshold)
        {
            _config.AlignmentThreshold = threshold;
            Save();
        }

        public void SetRpmMode(bool isFourPole)
        {
            _config.IsFourPoleMode = isFourPole;
            Save();
        }

    }
}
