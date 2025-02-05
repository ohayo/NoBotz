using ClientApi.Networking;
using NoBotz;
using NoBotz.Misc;
using On.Terraria;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using static System.Net.Mime.MediaTypeNames;
using static TShockAPI.GetDataHandlers;

namespace NoBotz.Helpers
{
    public static class Watchdog
    {
        public static Configuration? Configuration { get; set; }

        public static List<HumanPlayer> Players { get; set; }

        public static List<string> BlockedTemporarily = new List<string>();

        static Watchdog()
        {
            Players = new List<HumanPlayer>();
        }

        static HumanPlayer? GetHumanPlayer(TSPlayer player)
        {
            foreach (var humanPlayer in Players)
            {
                if (humanPlayer.Player == player)
                    return humanPlayer;
            }

            return null;
        }

        public static HumanPlayer? GetHumanPlayerByIndex(int who)
        {
            foreach (var humanPlayer in Players)
            {
                if (humanPlayer.Player.Index == who)
                    return humanPlayer;
            }

            return null;
        }

        static List<HumanPlayer> LookupHumanPlayersByIP(string ip) => Players.Where(x => x.Player.IP == ip).ToList();

        public static void OnPlayerCommand(PlayerCommandEventArgs e)
        {
            if (e.Player == null || Configuration == null || !Configuration.Enabled)
                return;

            byte playerSlot = (byte)e.Player.Index;
            byte packetType = 201;

            if (playerSlot < 255 && TShock.Players[playerSlot] != null && TShock.Players[playerSlot].Active && TShock.Players[playerSlot].FinishedHandshake)
            {
                var PacketTypeToMaxPerTimeFrame = Configuration.PacketTypeToMaxPerTimeFrame;

                if (PacketTypeToMaxPerTimeFrame.ContainsKey(packetType))
                {
                    int packetAmount = Ratelimiter.CheckRateLimit(playerSlot, packetType);

                    if (packetAmount > 0 && !Ratelimiter.HasNotifiedOfAbuse((byte)packetType, playerSlot))
                    {
                        TShock.Log.ConsoleInfo($"Detected chat (specifically commands) packet spam from {TShock.Players[playerSlot].Name} (player ID: {playerSlot}) (~{packetAmount} packets received)");

                        if (!Ratelimiter.Notified.ContainsKey(playerSlot))
                            Ratelimiter.Notified[playerSlot] = new HashSet<byte>();

                        Ratelimiter.Notified[playerSlot].Add((byte)packetType);
                        Kick(playerSlot, "Character Abnormality Detected");
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        public static void OnPlayerChat(PlayerChatEventArgs e)
        {
            if (e.Player == null || Configuration == null || !Configuration.Enabled)
                return;

            byte playerSlot = (byte)e.Player.Index;
            byte packetType = 201;
            var player = TShock.Players[playerSlot];

            if (playerSlot < 255 && player != null && player.Active && player.FinishedHandshake)
            {
                var PacketTypeToMaxPerTimeFrame = Configuration.PacketTypeToMaxPerTimeFrame;

                if (PacketTypeToMaxPerTimeFrame.ContainsKey(packetType))
                {
                    int packetAmount = Ratelimiter.CheckRateLimit(playerSlot, packetType);

                    if (packetAmount > 0 && !Ratelimiter.HasNotifiedOfAbuse((byte)packetType, playerSlot))
                    {
                        TShock.Log.ConsoleInfo($"Detected chat packet spam from {player.Name} (player ID: {playerSlot}) (~{packetAmount} packets received)");

                        if (!Ratelimiter.Notified.ContainsKey(playerSlot))
                            Ratelimiter.Notified[playerSlot] = new HashSet<byte>();

                        Ratelimiter.Notified[playerSlot].Add((byte)packetType);
                        Kick(playerSlot, "Character Abnormality Detected");
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        public static void OnPlayerLeave(LeaveEventArgs e)
        {
            int who = e.Who;

            HumanPlayer? player = GetHumanPlayerByIndex(who);

            if (player == null)
                return;

            Players.Remove(player);
        }

        private static void Kick(byte playerSlot, string message)
        {
            if (Configuration == null || !Configuration.KickOnTrip || !Configuration.Enabled)
                return;

            if (playerSlot == 255)
                return;

            HumanPlayer? humanPlayer = GetHumanPlayerByIndex(playerSlot);
            var player = TShock.Players[playerSlot];

            if (player != null && player.Active)
            {
                var ip = player.IP;
                TShock.Players[playerSlot].Kick(message);

                if (Configuration.BlockTemporarilyOnTrip && !BlockedTemporarily.Contains(ip))
                {
                    BlockedTemporarily.Add(ip);

                    if (Configuration.DisconnectAllFromSameIPOnBlock)
                    {
                        List<HumanPlayer> HumanPlayers = LookupHumanPlayersByIP(ip);

                        if (HumanPlayers.Count > 0)
                        {
                            foreach(var humanePlayer in HumanPlayers)
                            {
                                TShock.Log.ConsoleInfo($"{humanePlayer.Player.Name} kicked due to disconnect all from same ip on block predicament");

                                player.Kick("Invalid positioning sent from client");
                            }
                        }
                    }

                    new Thread(() =>
                    {
                        Thread.Sleep(Configuration.TimeoutInMSUntilBlockRemoved);

                        BlockedTemporarily.Remove(ip);

                        TShock.Log.ConsoleInfo($"{ip} has been removed from the blocked temporarily list. They can now join.");
                    }).Start();
                }
            }

            if (humanPlayer != null)
                Players.Remove(humanPlayer);
        }

        private static bool NecessaryPacket(int packetType)
        {
            HashSet<int> packets = new()
            {
                50, 4, 147, 12, 5, 8, 6, 147, 82
            };

            return packets.Contains(packetType);
        }

        private static bool OnNetModule(byte playerSlot, TSPlayer player, ushort moduleId, int packetLength)
        {
            if (Configuration == null || !Configuration.Enabled)
                return false;

            int packetType = 200 + moduleId;

            if (Configuration.PacketTypeToMinAndMaxLengths.ContainsKey(packetType))
            {
                var outValue = Configuration.PacketTypeToMinAndMaxLengths[packetType];

                if (packetLength < outValue.Key || packetLength >= outValue.Value)
                {
                    TShock.Log.ConsoleInfo($"Detected packet limits broken of packetId: {packetType} (net module ID: {moduleId}) from {player.Name} (player ID: {playerSlot}) (~{packetLength} length)");

                    Kick(playerSlot, "Character Abnormality Detected");
                    return true;
                }
            }

            var PacketTypeToMaxPerTimeFrame = Configuration.PacketTypeToMaxPerTimeFrame;

            if (PacketTypeToMaxPerTimeFrame.ContainsKey(packetType))
            {
                int packetAmount = Ratelimiter.CheckRateLimit(playerSlot, packetType);

                if (packetAmount > 0 && !Ratelimiter.HasNotifiedOfAbuse((byte)packetType, playerSlot))
                {
                    TShock.Log.ConsoleInfo($"Detected packet spam of packetId: {packetType} (net module ID: {moduleId}) from {player.Name} (player ID: {playerSlot}) (~{packetAmount} packets received)");

                    if (!Ratelimiter.Notified.ContainsKey(playerSlot))
                        Ratelimiter.Notified[playerSlot] = new HashSet<byte>();

                    Ratelimiter.Notified[playerSlot].Add((byte)packetType);
                    Kick(playerSlot, "Character Abnormality Detected");
                    return true;
                }
            }

            return false;
        }

        public static void OnNetGetData(GetDataEventArgs e)
        {
            if (Configuration == null || !Configuration.Enabled)
                return;

            int packetType = (int)e.MsgID;
            int packetLength = e.Length;
            byte playerSlot = (byte)e.Msg.whoAmI;

            var player = TShock.Players[playerSlot];

            if (player != null && player.Active && !player.FinishedHandshake && Configuration.KickPacketsFromHandshakeBypass && !NecessaryPacket(packetType))
            {
                TShock.Log.ConsoleInfo($"Detected malicious invisible actor spying on the server: {TShock.Players[playerSlot].Name} (packet ID: {packetType}) - they have been kicked.");

                Kick(playerSlot, "Malicious placement of tileframe");
                e.Handled = true;
                return;
            }

            if (playerSlot < 255 && player != null && player.Active && player.FinishedHandshake)
            {
                if (packetType == 82)
                {
                    using (var stream = new MemoryStream(e.Msg.readBuffer))
                    {
                        stream.Position = e.Index;

                        using (var reader = new BinaryReader(stream))
                        {
                            ushort moduleId = reader.ReadUInt16();

                            e.Handled = OnNetModule(playerSlot, player, moduleId, packetLength);
                        }
                    }
                    return;
                }

                if (Configuration.EnforcePacketLengthLimits && Configuration.PacketTypeToMinAndMaxLengths.ContainsKey(packetType))
                {
                    var outValue = Configuration.PacketTypeToMinAndMaxLengths[packetType];

                    if (packetLength < outValue.Key || packetLength >= outValue.Value)
                    {
                        TShock.Log.ConsoleInfo($"Detected packet limits broken of packetId: {packetType} from {player.Name} (player ID: {playerSlot}) (~{packetLength} length)");

                        Kick(playerSlot, "Character Abnormality Detected");
                        e.Handled = true;
                        return;
                    }
                }

                var PacketTypeToMaxPerTimeFrame = Configuration.PacketTypeToMaxPerTimeFrame;

                if (PacketTypeToMaxPerTimeFrame.ContainsKey(packetType))
                {
                    int packetAmount = Ratelimiter.CheckRateLimit(playerSlot, packetType);

                    if (packetAmount > 0 && !Ratelimiter.HasNotifiedOfAbuse((byte)packetType, playerSlot))
                    {
                        TShock.Log.ConsoleInfo($"Detected packet spam of packetId: {packetType} from {player.Name} (player ID: {playerSlot}) (~{packetAmount} packets received)");

                        if (!Ratelimiter.Notified.ContainsKey(playerSlot))
                            Ratelimiter.Notified[playerSlot] = new HashSet<byte>();

                        Ratelimiter.Notified[playerSlot].Add((byte)packetType);
                        Kick(playerSlot, "Character Abnormality Detected");
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        public static void OnPlayerInfo(object? sender, PlayerInfoEventArgs e)
        {
            if (Configuration == null || !Configuration.Enabled)
                return;

            var player = e.Player;

            if (Configuration.EnforceMaxClients)
            {
                List<HumanPlayer> HumanPlayers = LookupHumanPlayersByIP(player.IP);

                if (HumanPlayers.Count >= Configuration.MaxClientsPerIP)
                {
                    TShock.Log.ConsoleInfo($"{player.Name} broke the max client per IP threshold!");

                    Kick(e.PlayerId, "Broke the max client threshold. Contact server staff if you require an exemption.");
                    e.Handled = true;
                    return;
                }
            }

            if (BlockedTemporarily.Contains(player.IP))
            {
                e.Handled = true;
                return;
            }

            HumanPlayer? humanPlayer = GetHumanPlayer(player);

            if (humanPlayer == null)
            {
                humanPlayer = new HumanPlayer(player);

                Players.Add(humanPlayer);

                if (Configuration.EnforceSpawningPlayer && Configuration.MaxSecondsUntilSpawnNecessary > 0)
                {
                    new Thread(() =>
                    {
                        Thread.Sleep(Configuration.MaxSecondsUntilSpawnNecessary * 1000);

                        if (!e.Player.FinishedHandshake && e.Player.Active)
                        {
                            TShock.Log.ConsoleInfo($"Detected spawn player bypass from {e.Player.Name}");

                            Kick(e.PlayerId, "Character Abnormality Detected");
                            return;
                        }
                    }).Start();
                }
            }
        }
    }
}
