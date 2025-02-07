using ClientApi.Networking;
using IL.Terraria;
using NoBotz;
using NoBotz.Misc;
using On.Terraria;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.Localization;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
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

        public static Dictionary<string, string> IPsToCaptchas = new Dictionary<string, string>();

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

            TSPlayer player = e.Player;

            if (Configuration.Captcha && !Configuration.CaptchaBeforeJoin && IPsToCaptchas.ContainsKey(player.IP))
            {
                player.Disable("Please answer the captcha to continue.");
                player.SendErrorMessage("You cannot use any commands until you correctly answer the captcha given to you upon join.");
                e.Handled = true;
                return;
            }

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

            TSPlayer player = e.Player;

            if (Configuration.Captcha && !Configuration.CaptchaBeforeJoin && IPsToCaptchas.ContainsKey(player.IP))
            {
                string captcha = IPsToCaptchas[player.IP];

                if (e.RawText != captcha)
                {
                    player.Disable("Captcha was incorrect. Try again.");
                    player.SendErrorMessage("The captcha provided was incorrect. This incident has been logged. Try again.");
                }
                else
                {
                    IPsToCaptchas.Remove(player.IP);
                    player.SendSuccessMessage("You have successfully answered the captcha.");
                }

                e.Handled = true;
                return;
            }

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

            if (Configuration.Captcha && !Configuration.CaptchaBeforeJoin && IPsToCaptchas.ContainsKey(player.IP) && moduleId == 1)
                return false; //Allow them to answer the captcha

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

        private static Random random = new Random();

        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public static void OnNetGetData(GetDataEventArgs e)
        {
            if (Configuration == null || !Configuration.Enabled)
                return;

            TSPlayer plr = new TSPlayer(e.Msg.whoAmI);

            if (e.MsgID == PacketTypes.ConnectRequest && Configuration.Captcha && Configuration.CaptchaBeforeJoin)
            {
                if (IPsToCaptchas.ContainsKey(plr.IP))
                {
                    Terraria.NetMessage.SendData((int)PacketTypes.PasswordRequired, e.Msg.whoAmI);
                    e.Handled = true;
                    return;
                }

                string captcha = RandomString(Configuration.CaptchaLength);
                IPsToCaptchas.Add(plr.IP, captcha);

                Terraria.NetMessage.SendData((int)PacketTypes.Disconnect, e.Msg.whoAmI, text: NetworkText.FromLiteral($"This server requires a captcha before joining. Please reconnect and type this for the password:\n{captcha}"));
                e.Handled = true;
                return;
            }

            if (e.MsgID == PacketTypes.PasswordSend && Configuration.Captcha && Configuration.CaptchaBeforeJoin)
            {
                if (!IPsToCaptchas.ContainsKey(plr.IP))
                {
                    TShock.Log.ConsoleInfo($"{plr.IP} tried to send a password for the on before join captcha when they weren't told to do so. They have been banned for {Configuration.TempBanOnCaptchaFailedLengthInMins} minutes.");
                    Terraria.NetMessage.SendData((int)PacketTypes.Disconnect, e.Msg.whoAmI, text: NetworkText.FromLiteral($"Invalid captcha provided. This incident has been logged."));
                    TShock.Bans.InsertBan($"{Identifier.IP}{plr.IP}", "Invalid Captcha Provided", "NoBots", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(Configuration.TempBanOnCaptchaFailedLengthInMins));

                    e.Handled = true;
                    return;
                }

                using (var stream = new MemoryStream(e.Msg.readBuffer))
                {
                    stream.Position = e.Index;

                    using (var reader = new BinaryReader(stream))
                    {
                        string pw = reader.ReadString();

                        if (IPsToCaptchas[plr.IP] != pw)
                        {
                            TShock.Log.ConsoleInfo($"{plr.IP} failed the on before join captcha! They answered: {pw} when the correct answer was: {IPsToCaptchas[plr.IP]} -> They have been banned for {Configuration.TempBanOnCaptchaFailedLengthInMins} minutes.");
                            Terraria.NetMessage.SendData((int)PacketTypes.Disconnect, e.Msg.whoAmI, text: NetworkText.FromLiteral($"Invalid captcha provided. This incident has been logged."));
                            TShock.Bans.InsertBan($"{Identifier.IP}{plr.IP}", "Invalid Captcha Provided", "NoBots", DateTime.UtcNow, DateTime.UtcNow.AddMinutes(Configuration.TempBanOnCaptchaFailedLengthInMins));

                            e.Handled = true;
                            return;
                        }
                    }
                }

                IPsToCaptchas.Remove(plr.IP);

                Terraria.NetMessage.SendData((int)PacketTypes.ContinueConnecting, e.Msg.whoAmI, number: (byte)e.Msg.whoAmI);
                Terraria.Netplay.Clients[e.Msg.whoAmI].State = 2;

                e.Handled = true;
                return;
            }

            int packetType = (int)e.MsgID;
            int packetLength = e.Length;
            byte playerSlot = (byte)e.Msg.whoAmI;

            var player = plr;

            if (player.State == 10 && !player.FinishedHandshake)
                player.FinishedHandshake = true; //So there's this bug for some reason with finish handshake and this plugin. hot fix

            if (player != null && player.Active && !player.FinishedHandshake && Configuration.KickPacketsFromHandshakeBypass && !NecessaryPacket(packetType) && !IPsToCaptchas.ContainsKey(player.IP))
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

                if (Configuration.Captcha && !Configuration.CaptchaBeforeJoin && IPsToCaptchas.ContainsKey(player.IP) && !NecessaryPacket(packetType))
                {
                    player.Disable("Please answer the captcha to continue.");
                    e.Handled = true;
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

        internal static void OnPlayerJoin(JoinEventArgs args)
        {
            if (Configuration == null || !Configuration.Enabled)
                return;

            TSPlayer plr = new TSPlayer(args.Who);

            if (Configuration.Captcha && !Configuration.CaptchaBeforeJoin)
            {
                string captcha;

                if (!IPsToCaptchas.ContainsKey(plr.IP))
                {
                    captcha = RandomString(Configuration.CaptchaLength);
                    IPsToCaptchas.Add(plr.IP, captcha);

                    plr.Disable("Please enter the captcha to continue");
                    plr.SendErrorMessage($"This server requires a captcha before you can do anything. Please enter the following in chat: {captcha}");
                }
                else
                {
                    captcha = IPsToCaptchas[plr.IP];
                    IPsToCaptchas.Add(plr.IP, captcha);

                    plr.Disable("Please enter the captcha to continue");
                    plr.SendErrorMessage($"This server requires a captcha before you can do anything. Please enter the following in chat: {captcha}");
                }
            }
        }
    }
}
