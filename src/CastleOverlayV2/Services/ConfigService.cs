using System;
using System.IO;
using Newtonsoft.Json;
using CastleOverlayV2.Models;
using CastleOverlayV2.Services;

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
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DragOverlay");

            Directory.CreateDirectory(appDataPath);
            _configFilePath = Path.Combine(appDataPath, "config.json");

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
                try
                {
                    string json = File.ReadAllText(_configFilePath);
                    _config = JsonConvert.DeserializeObject<Config>(json) ?? new Config();
                    // An older saved file carries an older build number; the running app wins.
                    _config.BuildNumber = Config.CurrentBuildNumber;
                    NormalizePaneSettings();
                }
                catch
                {
                    // If file is corrupted, fall back and overwrite
                    _config = new Config();
                    Save();
                }
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

        public void SetChannelColor(string channelName, string? hex)
        {
            if (hex == null) _config.ChannelColors.Remove(channelName);
            else _config.ChannelColors[channelName] = hex;
            Save();
        }

        public void SetChannelBold(string channelName, bool bold)
        {
            _config.BoldChannels.Remove(channelName);
            if (bold) _config.BoldChannels.Add(channelName);
            Save();
        }

        public void SetChannelBand(string channelName, double height, double position)
        {
            if (height >= 1.0) _config.ChannelBands.Remove(channelName);
            else _config.ChannelBands[channelName] = new ChannelBand { Height = height, Position = position };
            Save();
        }

        public void SetPaneState(
            int paneCount,
            IReadOnlyDictionary<string, int> channelPanes,
            IEnumerable<double> paneHeights,
            IReadOnlyDictionary<int, string>? paneFocusedChannels = null)
        {
            _config.PaneCount = Math.Clamp(paneCount, 1, 3);
            _config.ChannelPanes = channelPanes
                .Where(pair => pair.Value >= 1 && pair.Value <= _config.PaneCount)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            _config.PaneHeights = NormalizeHeights(paneHeights, _config.PaneCount);
            _config.PaneFocusedChannels = (paneFocusedChannels ?? new Dictionary<int, string>())
                .Where(pair => pair.Key >= 1 && pair.Key <= _config.PaneCount &&
                               !string.IsNullOrWhiteSpace(pair.Value))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
            Save();
        }

        private void NormalizePaneSettings()
        {
            _config.PaneCount = Math.Clamp(_config.PaneCount, 1, 3);
            _config.ChannelPanes ??= new Dictionary<string, int>();
            foreach (string channel in _config.ChannelPanes.Keys.ToList())
                if (_config.ChannelPanes[channel] < 1 || _config.ChannelPanes[channel] > _config.PaneCount)
                    _config.ChannelPanes[channel] = 1;
            _config.PaneHeights = NormalizeHeights(_config.PaneHeights, _config.PaneCount);
            _config.PaneFocusedChannels ??= new Dictionary<int, string>();
            foreach (int paneNumber in _config.PaneFocusedChannels.Keys.ToList())
                if (paneNumber < 1 || paneNumber > _config.PaneCount ||
                    string.IsNullOrWhiteSpace(_config.PaneFocusedChannels[paneNumber]))
                    _config.PaneFocusedChannels.Remove(paneNumber);
        }

        private static List<double> NormalizeHeights(IEnumerable<double>? heights, int paneCount)
        {
            double[] values = heights?.Take(paneCount).ToArray() ?? Array.Empty<double>();
            if (values.Length != paneCount || values.Any(value => !double.IsFinite(value) || value <= 0))
                values = Enumerable.Repeat(1.0 / paneCount, paneCount).ToArray();
            double total = values.Sum();
            return values.Select(value => value / total).ToList();
        }

        public bool IsDebugLoggingEnabled() => _config?.EnableDebugLogging ?? false;

        public string GetBuildNumber() => Config.CurrentBuildNumber;


    }
}
