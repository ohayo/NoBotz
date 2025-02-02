using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NoBotz.Misc
{
    public class Configuration
    {
        [JsonIgnore]
        private readonly string? _configPath;

        public bool BlockTemporarilyOnTrip { get; set; }

        public int TimeoutInMSUntilBlockRemoved { get; set; }

        public bool DisconnectAllFromSameIPOnBlock { get; set; }

        public bool KickOnTrip { get; set; }

        public bool EnforcePacketLengthLimits { get; set; }

        public bool EnforceSpawningPlayer { get; set; }

        public bool EnforceMaxClients { get; set; }

        public bool KickPacketsFromHandshakeBypass { get; set; }

        public int MaxClientsPerIP { get; set; }

        public int MaxSecondsUntilSpawnNecessary { get; set; }

        public int TimeframeForPacketsInMs { get; set; }

        public Dictionary<int, KeyValuePair<int, int>> PacketTypeToMinAndMaxLengths { get; set; } = new();

        public Dictionary<int, int> PacketTypeToMaxPerTimeFrame { get; set; } = new();

        public Configuration() { }

        public Configuration(string fullPath)
        {
            _configPath = fullPath;
        }

        public void Setup()
        {
            if (_configPath == null)
                return;

            File.WriteAllText(_configPath, JsonSerializer.Serialize(new Configuration(string.Empty)
            {
                BlockTemporarilyOnTrip = true,
                DisconnectAllFromSameIPOnBlock = true,
                TimeoutInMSUntilBlockRemoved = 1000 * 60 * 2,
                KickOnTrip = true,
                EnforcePacketLengthLimits = false,
                EnforceSpawningPlayer = true,
                EnforceMaxClients = true,
                KickPacketsFromHandshakeBypass = true,
                MaxClientsPerIP = 1,
                MaxSecondsUntilSpawnNecessary = 5,
                TimeframeForPacketsInMs = 1000 * 60, //Per minute
                PacketTypeToMaxPerTimeFrame = new Dictionary<int, int>() //Max packet amounts from a player per minute
                    {
                        { 24, 100 },   // Strike NPC with held item
                        { 27, 150 },  // Projectile Update
                        { 29, 150 },  // Destroy Projectile
                        { 30, 50 },   // Toggle PVP
                        { 35, 50 },   // Heal Effect
                        { 36, 100 },   // Player Zone
                        { 40, 100 },   // Set Active NPC
                        { 43, 50 },   // Mana Effect
                        { 45, 50 },   // Player Team
                        { 50, 50 },   // Player Buff
                        { 51, 50 },   // Special NPC Effect
                        { 55, 50 },   // Add Player Buff
                        { 58, 100 },   // Play Music Item
                        { 62, 50 },   // Player Dodge
                        { 66, 20 },   // Heal Other Player
                        { 96, 30 },   // Player Teleport Portal
                        { 99, 50 },   // Update Minion Target
                        { 102, 50 },  // Nebula Level Up
                        { 121, 50 },  // TEDisplayDollItemSync
                        { 124, 50 },  // TEHatRackItemSync
                        { 125, 150 },  // SyncTilePicking
                        { 128, 100 },  // LandGolfBallInCup
                        { 134, 100 },  // UpdatePlayerLuckFactors
                        { 201, 50 }, //Fake packet id for chat (packet 82 + 2) - also applies to commands used
                        { 202, 500 }, //Fake packet id for particles (packet 82 + 9)
                    },
                PacketTypeToMinAndMaxLengths = new Dictionary<int, KeyValuePair<int, int>>()
                    {
                        { 4, new KeyValuePair<int, int>(20, 500) } //Player Info
                    }
            }, options: new JsonSerializerOptions()
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            }));
        }

        public void Reload()
        {
            if (_configPath == null)
                return;

            if (File.Exists(_configPath))
            {
                string json = File.ReadAllText(_configPath);

                var loadedConfig = JsonSerializer.Deserialize<Configuration>(json);

                if (loadedConfig != null)
                {
                    BlockTemporarilyOnTrip = loadedConfig.BlockTemporarilyOnTrip;
                    TimeoutInMSUntilBlockRemoved = loadedConfig.TimeoutInMSUntilBlockRemoved;
                    DisconnectAllFromSameIPOnBlock = loadedConfig.DisconnectAllFromSameIPOnBlock;
                    KickOnTrip = loadedConfig.KickOnTrip;
                    EnforcePacketLengthLimits = loadedConfig.EnforcePacketLengthLimits;
                    EnforceSpawningPlayer = loadedConfig.EnforceSpawningPlayer;
                    EnforceMaxClients = loadedConfig.EnforceMaxClients;
                    MaxSecondsUntilSpawnNecessary = loadedConfig.MaxSecondsUntilSpawnNecessary;
                    KickPacketsFromHandshakeBypass = loadedConfig.KickPacketsFromHandshakeBypass;
                    MaxClientsPerIP = loadedConfig.MaxClientsPerIP;
                    TimeframeForPacketsInMs = loadedConfig.TimeframeForPacketsInMs;
                    PacketTypeToMinAndMaxLengths = loadedConfig.PacketTypeToMinAndMaxLengths;
                    PacketTypeToMaxPerTimeFrame = loadedConfig.PacketTypeToMaxPerTimeFrame;
                }
            }
        }

        public void Save()
        {
            if (_configPath == null)
                return;

            if (File.Exists(_configPath))
            {
                File.WriteAllText(_configPath, JsonSerializer.Serialize(this, options: new JsonSerializerOptions()
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true
                }));
            }
        }
    }
}
