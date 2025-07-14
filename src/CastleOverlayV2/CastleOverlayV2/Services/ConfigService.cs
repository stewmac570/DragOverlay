using CastleOverlayV2.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace CastleOverlayV2.Services
{
    public static class ConfigService
    {
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "config.json");

        public static Config Load()
        {
            string folder = Path.GetDirectoryName(ConfigFilePath);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            if (!File.Exists(ConfigFilePath))
            {
                return new Config(); // fresh defaults
            }

            string json = File.ReadAllText(ConfigFilePath);
            return JsonConvert.DeserializeObject<Config>(json) ?? new Config();
        }


        public static void Save(Config config)
        {
            string folder = Path.GetDirectoryName(ConfigFilePath);

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            string json = JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(ConfigFilePath, json);
        }


        public static void SaveChannelVisibility(Dictionary<string, bool> channelVisibility)
        {
            var config = Load();
            config.ChannelVisibility = channelVisibility;
            Save(config);
        }
    }
}
