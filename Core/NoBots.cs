using IL.Terraria;
using Microsoft.Xna.Framework;
using NoBotz.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using static Terraria.GameContent.UI.States.UIVirtualKeyboard;
using static TShockAPI.GetDataHandlers;
using Main = Terraria.Main;

namespace NoBotz.Core
{
    [ApiVersion(2, 1)]
    public class NoBots : TerrariaPlugin
    {
        public override string Author => "noia.site";

        public override string Description => "Helps with security against bad actors & terraria bots";

        public override string Name => "NoBots";

        public override Version Version => new Version(1, 0, 0, 0);

        public NoBots(Main game) : base(game)
        {
            Order = 1;
        }

        public override void Initialize()
        {
            string configPath = Path.Combine(TShock.SavePath, "NoBots.json");

            Watchdog.Configuration = new Misc.Configuration(configPath);

            if (!File.Exists(configPath))
                Watchdog.Configuration.Setup();

            Watchdog.Configuration.Reload();

            Commands.ChatCommands.Add(new Command(new List<string>() { "*", "nobots.management" }, HandleNoBotsManagement, "nobots"));

            ServerApi.Hooks.NetGetData.Register(this, new HookHandler<GetDataEventArgs>(Watchdog.OnNetGetData));
            ServerApi.Hooks.ServerLeave.Register(this, new HookHandler<LeaveEventArgs>(Watchdog.OnPlayerLeave));
            PlayerHooks.PlayerCommand += Watchdog.OnPlayerCommand;
            PlayerHooks.PlayerChat += Watchdog.OnPlayerChat;

            PlayerInfo.Register(new EventHandler<PlayerInfoEventArgs>(Watchdog.OnPlayerInfo));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (Watchdog.Configuration != null)
                    Watchdog.Configuration.Save();

                ServerApi.Hooks.NetGetData.Deregister(this, new HookHandler<GetDataEventArgs>(Watchdog.OnNetGetData));
                ServerApi.Hooks.ServerLeave.Deregister(this, new HookHandler<LeaveEventArgs>(Watchdog.OnPlayerLeave));

                PlayerHooks.PlayerCommand -= Watchdog.OnPlayerCommand;
                PlayerHooks.PlayerChat -= Watchdog.OnPlayerChat;

                PlayerInfo.UnRegister(new EventHandler<PlayerInfoEventArgs>(Watchdog.OnPlayerInfo));
            }

            base.Dispose(disposing);
        }

        private void HandlePacketLengthsCommand(CommandArgs args,List<string> parameters)
        {
            if (Watchdog.Configuration == null)
                return;

            TShock.Log.ConsoleInfo($"Length for command packet lengths: {parameters.Count}");

            if (parameters.Count() == 1)
            {
                args.Player.SendMessage("[c/00FFFF:=== NoBots Packet Lengths Management ===]", Color.White);

                if (Watchdog.Configuration.PacketTypeToMinAndMaxLengths.Count() == 0)
                    args.Player.SendErrorMessage("There are currently no packet types to min & max lengths configured.");

                foreach (var packetType in Watchdog.Configuration.PacketTypeToMinAndMaxLengths)
                    args.Player.SendMessage($"[c/FFFFFF:Packet Type: {packetType.Key}] - Min Length: {$"[c/FF0000:{packetType.Value.Key}]"} - Max Length: {$"[c/00FF00:{packetType.Value.Value}]"}.", 255, 255, 255);

                args.Player.SendMessage("[c/FFD700:Options:]", Color.White);
                args.Player.SendMessage($"[c/FFA500:- configure <packetType> <minLength> <maxLength> (Configures the packet type with the new min & max lengths)]", Color.White);
                args.Player.SendMessage($"[c/FFA500:- add <packetType> <minLength> <maxLength> (Adds new packet type with the given min & max length)]", Color.White);
                args.Player.SendMessage($"[c/FFA500:- remove <packetType> (Removes the given packet type from the min & max lengths dictionary)]", Color.White);
                args.Player.SendMessage("[c/87CEEB:- Example:] [c/FFFFFF:/nobots packetlengths add 1 400 5000]", Color.White);
                return;
            }

            if (parameters.Count() >= 2 && parameters[1].ToLower() == "configure")
            {
                if (parameters.Count() < 5)
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetlengths configure (example: /nobots packetlengths configure 1 120 500)");
                    return;
                }

                string packetType = parameters[2];

                if (!int.TryParse(packetType, out int packetID))
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetlengths configure (example: /nobots packetlengths configure 1 120 500)");
                    return;
                }

                string minLength = parameters[3];
                string maxLength = parameters[4];

                if (!int.TryParse(minLength, out int minLengthVal))
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetlengths configure (example: /nobots packetlengths configure 1 120 500)");
                    return;
                }

                if (!int.TryParse(maxLength, out int maxLengthVal))
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetlengths configure (example: /nobots packetlengths configure 1 120 500)");
                    return;
                }

                if (!Watchdog.Configuration.PacketTypeToMinAndMaxLengths.ContainsKey(packetID))
                {
                    args.Player.SendErrorMessage($"Lengths dictionary doesn't contain that packet type. Perhaps you meant to use /nobots packetlengths add {packetID}");
                    return;
                }

                Watchdog.Configuration.PacketTypeToMinAndMaxLengths[packetID] = new KeyValuePair<int, int>(minLengthVal, maxLengthVal);
                Watchdog.Configuration.Save();

                args.Player.SendSuccessMessage($"{packetID} has been modified with a minimum packet length of {minLengthVal} to maximum: {maxLengthVal}");
            }
            else if (parameters.Count() >= 2 && parameters[1].ToLower() == "add")
            {
                if (parameters.Count() < 5)
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetlengths add (example: /nobots packetlengths add 1 120 500)");
                    return;
                }

                string packetType = parameters[2];

                if (!int.TryParse(packetType, out int packetID))
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetlengths add (example: /nobots packetlengths add 1 120 500)");
                    return;
                }

                string minLength = parameters[3];
                string maxLength = parameters[4];

                if (!int.TryParse(minLength, out int minLengthVal))
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetlengths add (example: /nobots packetlengths add 1 120 500)");
                    return;
                }

                if (!int.TryParse(maxLength, out int maxLengthVal))
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetlengths add (example: /nobots packetlengths add 1 120 500)");
                    return;
                }

                if (Watchdog.Configuration.PacketTypeToMinAndMaxLengths.ContainsKey(packetID))
                {
                    args.Player.SendErrorMessage($"Lengths dictionary already contains that packet type. Perhaps you meant to use /nobots packetlengths configure {packetID} {minLengthVal} {maxLengthVal}");
                    return;
                }

                Watchdog.Configuration.PacketTypeToMinAndMaxLengths.Add(packetID, new KeyValuePair<int, int>(minLengthVal, maxLengthVal));
                Watchdog.Configuration.Save();

                args.Player.SendSuccessMessage($"{packetID} has been added to the lengths dictionary with a min packet length of {minLengthVal} to max: {maxLengthVal}");
            }
            else if (parameters.Count() >= 2 && parameters[1] == "remove")
            {
                if (parameters.Count() < 3)
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetlengths remove (example: /nobots packetlengths remove 1)");
                    return;
                }

                string packetType = parameters[2];

                if (!int.TryParse(packetType, out int packetID))
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetlengths remove (example: /nobots packetlengths remove 1)");
                    return;
                }

                if (!Watchdog.Configuration.PacketTypeToMinAndMaxLengths.ContainsKey(packetID))
                {
                    args.Player.SendErrorMessage($"Packet Types to min & max lengths doesn't contain that packet type. Perhaps you meant to use /nobots packetlengths add {packetID} <minLength> <maxLength>");
                    return;
                }

                Watchdog.Configuration.PacketTypeToMinAndMaxLengths.Remove(packetID);
                Watchdog.Configuration.Save();

                args.Player.SendSuccessMessage($"Packet type {packetID} has been removed from the lengths dictionary.");
            }
            else
                args.Player.SendErrorMessage("Missing arguments for /nobots packetlengths (example: /nobots packetlengths add 1 50 100)");
        }

        private void HandlePacketsMaxCommand(CommandArgs args, List<string> parameters)
        {
            if (Watchdog.Configuration == null)
                return;

            TShock.Log.ConsoleInfo($"Length for command packet max : {parameters.Count}");

            if (parameters.Count() == 1)
            {
                args.Player.SendMessage("[c/00FFFF:=== NoBots Packets To Max Per Timeframe Management ===]", Color.White);

                if (Watchdog.Configuration.PacketTypeToMaxPerTimeFrame.Count() == 0)
                    args.Player.SendErrorMessage("There are currently no packet types to max per timeframe configured.");

                foreach (var packetType in Watchdog.Configuration.PacketTypeToMaxPerTimeFrame)
                    args.Player.SendMessage($"[c/FFFFFF:Packet Type: {packetType.Key}] - Max Per Timeframe ([c/FFA500:{Watchdog.Configuration.TimeframeForPacketsInMs}]ms): {$"[c/FF0000:{packetType.Value}]"}.", 255, 255, 255);

                args.Player.SendMessage("[c/FFD700:Options:]", Color.White);
                args.Player.SendMessage($"[c/FFA500:- configure <packetType> <maxPerTimeFrame> (Configures the packet type with the new max amount per timeframe)]", Color.White);
                args.Player.SendMessage($"[c/FFA500:- add <packetType> <maxPerTimeFrame> (Adds new packet type with the max amount per timeframe)]", Color.White);
                args.Player.SendMessage($"[c/FFA500:- remove <packetType> (Removes the given packet type from the max per timeframe dictionary)]", Color.White);
                args.Player.SendMessage("[c/87CEEB:- Example:] [c/FFFFFF:/nobots packetsmax configure 1 50]", Color.White);
                return;
            }

            if (parameters.Count() >= 2 && parameters[1] == "configure")
            {
                if (parameters.Count() < 4)
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetsmax configure (example: /nobots packetsmax configure 1 50)");
                    return;
                }

                string packetType = parameters[2];

                if (!int.TryParse(packetType, out int packetID))
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetsmax configure (example: /nobots packetsmax configure 1 50)");
                    return;
                }

                string maxTimeFrame = parameters[3];

                if (!int.TryParse(maxTimeFrame, out int maxPerTimeFrame))
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetsmax configure (example: /nobots packetsmax configure 1 50)");
                    return;
                }

                if (!Watchdog.Configuration.PacketTypeToMaxPerTimeFrame.ContainsKey(packetID))
                {
                    args.Player.SendErrorMessage($"Packet Types to max per timeframe doesn't contain that packet type. Perhaps you meant to use /nobots packetsmax add {packetID} <maxPerTimeFrame>");
                    return;
                }

                Watchdog.Configuration.PacketTypeToMaxPerTimeFrame[packetID] = maxPerTimeFrame;
                Watchdog.Configuration.Save();

                args.Player.SendSuccessMessage($"{packetID} has been modified with a maximum of {maxPerTimeFrame} packets per timeframe.");
            }
            else if (parameters.Count() >= 2 && parameters[1] == "add")
            {
                if (parameters.Count() < 4)
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetsmax add (example: /nobots packetsmax add 1 50)");
                    return;
                }

                string packetType = parameters[2];

                if (!int.TryParse(packetType, out int packetID))
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetsmax add (example: /nobots packetsmax add 1 50)");
                    return;
                }

                string maxTimeFrame = parameters[3];

                if (!int.TryParse(maxTimeFrame, out int maxPerTimeFrame))
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetsmax add (example: /nobots packetsmax add 1 50)");
                    return;
                }

                if (Watchdog.Configuration.PacketTypeToMaxPerTimeFrame.ContainsKey(packetID))
                {
                    args.Player.SendErrorMessage($"Packet Types to max per timeframe already contains that packet type. Perhaps you meant to use /nobots packetsmax configure {packetID} <maxPerTimeFrame>");
                    return;
                }

                Watchdog.Configuration.PacketTypeToMaxPerTimeFrame.Add(packetID, maxPerTimeFrame);
                Watchdog.Configuration.Save();

                args.Player.SendSuccessMessage($"{packetID} has been added with a maximum of {maxPerTimeFrame} packets per timeframe.");
            }
            else if (parameters.Count() >= 2 && parameters[1] == "remove")
            {
                if (parameters.Count() < 3)
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetsmax remove (example: /nobots packetsmax remove 1)");
                    return;
                }

                string packetType = parameters[2];

                if (!int.TryParse(packetType, out int packetID))
                {
                    args.Player.SendErrorMessage("Missing arguments for /nobots packetsmax remove (example: /nobots packetsmax remove 1)");
                    return;
                }

                if (!Watchdog.Configuration.PacketTypeToMaxPerTimeFrame.ContainsKey(packetID))
                {
                    args.Player.SendErrorMessage($"Packet Types to max per timeframe doesn't contain that packet type. Perhaps you meant to use /nobots packetsmax add {packetID} <maxPerTimeFrame>");
                    return;
                }

                Watchdog.Configuration.PacketTypeToMaxPerTimeFrame.Remove(packetID);
                Watchdog.Configuration.Save();

                args.Player.SendSuccessMessage($"{packetID} has been removed from the max packets per timeframe dictionary.");
            }
            else
                args.Player.SendErrorMessage("Missing arguments for /nobots packetsmax (example: /nobots packetsmax add 1 60)");
        }

        private void HandleRegularSubcommand(CommandArgs args, List<string> parameters)
        {
            if (Watchdog.Configuration == null)
                return;

            switch (parameters[1].ToLower())
            {
                default:
                    args.Player.SendErrorMessage("Invalid command. Use /nobots help for options.");
                    break;
                case "block":
                    {
                        if (parameters.Count() < 3)
                        {
                            args.Player.SendErrorMessage("Missing arguments for block state toggling. Expected on/off but got nothing. (Example of proper usage: /nobots config block on)");
                            return;
                        }

                        bool newState = parameters[2].ToLower() == "on";

                        Watchdog.Configuration.BlockTemporarilyOnTrip = newState;

                        args.Player.SendMessage($"[c/FFFFFF:NoBots BlockTemporarilyOnTrip has been] {(Watchdog.Configuration.BlockTemporarilyOnTrip ? ("[c/00FF00:Enabled]") : ("[c/FF0000:Disabled]"))}.", 255, 255, 255);

                        Watchdog.Configuration.Save();
                    }
                    break;
                case "kick":
                    {
                        if (parameters.Count() < 3)
                        {
                            args.Player.SendErrorMessage("Missing arguments for kick state toggling. Expected on/off but got nothing. (Example of proper usage: /nobots config kick on)");
                            return;
                        }

                        bool newState = parameters[2].ToLower() == "on";

                        Watchdog.Configuration.KickOnTrip = newState;

                        args.Player.SendMessage($"[c/FFFFFF:NoBots KickOnTrip has been] {(Watchdog.Configuration.KickOnTrip ? ("[c/00FF00:Enabled]") : ("[c/FF0000:Disabled]"))}.", 255, 255, 255);

                        Watchdog.Configuration.Save();
                    }
                    break;
                case "disconnectall":
                    {
                        if (parameters.Count() < 3)
                        {
                            args.Player.SendErrorMessage("Missing arguments for disconnect all ips on block toggling. Expected on/off but got nothing. (Example of proper usage: /nobots config disconnectall on)");
                            return;
                        }

                        bool newState = parameters[2].ToLower() == "on";

                        Watchdog.Configuration.DisconnectAllFromSameIPOnBlock = newState;

                        args.Player.SendMessage($"[c/FFFFFF:NoBots DisconnectAllFromSameIPOnBlock has been] {(Watchdog.Configuration.DisconnectAllFromSameIPOnBlock ? ("[c/00FF00:Enabled]") : ("[c/FF0000:Disabled]"))}.", 255, 255, 255);

                        Watchdog.Configuration.Save();
                    }
                    break;
                case "packetlengths":
                    {
                        if (parameters.Count() < 3)
                        {
                            args.Player.SendErrorMessage("Missing arguments for packetlengths toggling. Expected on/off but got nothing. (Example of proper usage: /nobots config packetlengths on)");
                            return;
                        }

                        bool newState = parameters[2].ToLower() == "on";

                        Watchdog.Configuration.EnforcePacketLengthLimits = newState;

                        args.Player.SendMessage($"[c/FFFFFF:NoBots EnforcePacketLengths has been] {(Watchdog.Configuration.EnforcePacketLengthLimits ? ("[c/00FF00:Enabled]") : ("[c/FF0000:Disabled]"))}.", 255, 255, 255);

                        Watchdog.Configuration.Save();
                    }
                    break;
                case "max":
                    {
                        if (parameters.Count() < 3)
                        {
                            args.Player.SendErrorMessage("Missing arguments for maximum clients per IP toggling. Expected on/off but got nothing. (Example of proper usage: /nobots config max on)");
                            return;
                        }

                        bool newState = parameters[2].ToLower() == "on";

                        Watchdog.Configuration.EnforceMaxClients = newState;

                        args.Player.SendMessage($"[c/FFFFFF:NoBots EnforceMaxClients has been] {(Watchdog.Configuration.EnforceMaxClients ? ("[c/00FF00:Enabled]") : ("[c/FF0000:Disabled]"))}.", 255, 255, 255);

                        Watchdog.Configuration.Save();
                    }
                    break;
                case "spawn":
                    {
                        if (parameters.Count() < 3)
                        {
                            args.Player.SendErrorMessage("Missing arguments for player must spawn toggling. Expected on/off but got nothing. (Example of proper usage: /nobots config spawn on)");
                            return;
                        }

                        bool newState = parameters[2].ToLower() == "on";

                        Watchdog.Configuration.EnforceSpawningPlayer = newState;

                        args.Player.SendMessage($"[c/FFFFFF:NoBots EnforceSpawningPlayer has been] {(Watchdog.Configuration.EnforceSpawningPlayer ? ("[c/00FF00:Enabled]") : ("[c/FF0000:Disabled]"))}.", 255, 255, 255);

                        Watchdog.Configuration.Save();
                    }
                    break;
                case "kickhandshake":
                    {
                        if (parameters.Count() < 3)
                        {
                            args.Player.SendErrorMessage("Missing arguments for kicking clients who bypass handshake toggling. Expected on/off but got nothing. (Example of proper usage: /nobots config handshake on)");
                            return;
                        }

                        bool newState = parameters[2].ToLower() == "on";

                        Watchdog.Configuration.KickPacketsFromHandshakeBypass = newState;

                        args.Player.SendMessage($"[c/FFFFFF:NoBots KickPacketsFromHandshakeBypass has been] {(Watchdog.Configuration.KickPacketsFromHandshakeBypass ? ("[c/00FF00:Enabled]") : ("[c/FF0000:Disabled]"))}.", 255, 255, 255);

                        Watchdog.Configuration.Save();
                    }
                    break;
                case "timeoutblock":
                    {
                        if (parameters.Count() < 3)
                        {
                            args.Player.SendErrorMessage("Missing arguments for timeoutblock changing. Expected a value in milliseconds but got nothing. (Example of proper usage: /nobots config timeoutblock 5000)");
                            return;
                        }

                        string timeoutBlockRaw = parameters[2];

                        if (!int.TryParse(timeoutBlockRaw, out int TimeoutBlock))
                        {
                            args.Player.SendErrorMessage("Missing arguments for timeoutblock changing. Expected a value in milliseconds but got nothing. (Example of proper usage: /nobots config timeoutblock 5000)");
                            return;
                        }

                        Watchdog.Configuration.TimeoutInMSUntilBlockRemoved = TimeoutBlock;

                        args.Player.SendMessage($"[c/FFFFFF:NoBots TimeoutInMSUntilBlockRemoved has been changed to] {$"[c/00FF00:{Watchdog.Configuration.TimeoutInMSUntilBlockRemoved}]ms"}.", 255, 255, 255);

                        Watchdog.Configuration.Save();
                    }
                    break;
                case "timeframe":
                    {
                        if (parameters.Count() < 3)
                        {
                            args.Player.SendErrorMessage("Missing arguments for timeframe changing. Expected a value in milliseconds but got nothing. (Example of proper usage: /nobots config timeframe 60000)");
                            return;
                        }

                        string timeframeRaw = parameters[2];

                        if (!int.TryParse(timeframeRaw, out int Timeframe))
                        {
                            args.Player.SendErrorMessage("Missing arguments for timeframe changing. Expected a value in milliseconds but got nothing. (Example of proper usage: /nobots config timeframe 60000)");
                            return;
                        }

                        Watchdog.Configuration.TimeframeForPacketsInMs = Timeframe;

                        args.Player.SendMessage($"[c/FFFFFF:NoBots TimeframeForPacketsInMS has been changed to] {$"[c/00FF00:{Watchdog.Configuration.TimeframeForPacketsInMs}]ms"}.", 255, 255, 255);

                        Watchdog.Configuration.Save();
                    }
                    break;
                case "maxspawn":
                    {
                        if (parameters.Count() < 3)
                        {
                            args.Player.SendErrorMessage("Missing arguments for max seconds until spawn changing. Expected a value in milliseconds but got nothing. (Example of proper usage: /nobots config maxspawn 8)");
                            return;
                        }

                        string maxSpawn = parameters[2];

                        if (!int.TryParse(maxSpawn, out int maxSpawnSeconds))
                        {
                            args.Player.SendErrorMessage("Missing arguments for max seconds until spawn changing. Expected a value in milliseconds but got nothing. (Example of proper usage: /nobots config maxspawn 8)");
                            return;
                        }

                        Watchdog.Configuration.MaxSecondsUntilSpawnNecessary = maxSpawnSeconds;

                        args.Player.SendMessage($"[c/FFFFFF:NoBots MaxSecondsUntilSpawnNecessary has been changed to] {$"[c/00FF00:{Watchdog.Configuration.MaxSecondsUntilSpawnNecessary}]s"}.", 255, 255, 255);

                        Watchdog.Configuration.Save();
                    }
                    break;
                case "maxclients":
                    {
                        if (parameters.Count() < 3)
                        {
                            args.Player.SendErrorMessage("Missing arguments for maximum clients per IP changing. Expected a number but got nothing. (Example of proper usage: /nobots config maxclients 3)");
                            return;
                        }

                        string maxClients = parameters[2];

                        if (!int.TryParse(maxClients, out int maxClientsVal))
                        {
                            args.Player.SendErrorMessage("Missing arguments for maximum clients per IP changing. Expected a number but got nothing. (Example of proper usage: /nobots config maxclients 3)");
                            return;
                        }

                        Watchdog.Configuration.MaxClientsPerIP = maxClientsVal;

                        args.Player.SendMessage($"[c/FFFFFF:NoBots MaxClientsPerIP has been changed to] {$"[c/00FF00:{Watchdog.Configuration.MaxClientsPerIP}]"}.", 255, 255, 255);

                        Watchdog.Configuration.Save();
                    }
                    break;
            }
        }

        private void HandleConfig(CommandArgs args, List<string> parameters)
        {
            if (Watchdog.Configuration == null)
                return;

            if (parameters.Count() == 1)
            {
                args.Player.SendMessage("[c/00FFFF:=== NoBots Configuration Management ===]", Color.White);
                args.Player.SendMessage($"[c/FFFFFF:NoBots is] {(Watchdog.Configuration.Enabled ? "[c/00FF00:Enabled]" : "[c/FF0000:Disabled]")}.", Color.White);
                args.Player.SendMessage($"[c/FFFFFF:Blocking temporarily on trip is] {(Watchdog.Configuration.BlockTemporarilyOnTrip ? "[c/00FF00:Enabled]" : "[c/FF0000:Disabled]")}.", Color.White);
                args.Player.SendMessage($"[c/FFFFFF:Timeout in milliseconds until IP block is removed:] [c/00FF00:{Watchdog.Configuration.TimeoutInMSUntilBlockRemoved}]ms", Color.White);
                args.Player.SendMessage($"[c/FFFFFF:Disconnect all clients from the same IP on block is] {(Watchdog.Configuration.DisconnectAllFromSameIPOnBlock ? "[c/00FF00:Enabled]" : "[c/FF0000:Disabled]")}.", Color.White);
                args.Player.SendMessage($"[c/FFFFFF:Kicking on trip is] {(Watchdog.Configuration.KickOnTrip ? "[c/00FF00:Enabled]" : "[c/FF0000:Disabled]")}.", Color.White);
                args.Player.SendMessage($"[c/FFFFFF:Enforcing Packet Length Limits is] {(Watchdog.Configuration.EnforcePacketLengthLimits ? "[c/00FF00:Enabled]" : "[c/FF0000:Disabled]")}.", Color.White);
                args.Player.SendMessage($"[c/FFFFFF:Enforcing spawning player is] {(Watchdog.Configuration.EnforceSpawningPlayer ? "[c/00FF00:Enabled]" : "[c/FF0000:Disabled]")}.", Color.White);
                args.Player.SendMessage($"[c/FFFFFF:Enforcing maximum clients is] {(Watchdog.Configuration.EnforceMaxClients ? "[c/00FF00:Enabled]" : "[c/FF0000:Disabled]")}.", Color.White);
                args.Player.SendMessage($"[c/FFFFFF:Kick clients who attempt to bypass handshake is] {(Watchdog.Configuration.KickPacketsFromHandshakeBypass ? "[c/00FF00:Enabled]" : "[c/FF0000:Disabled]")}.", Color.White);
                args.Player.SendMessage($"[c/FFFFFF:The maximum clients per IP is:] [c/00FF00:{Watchdog.Configuration.MaxClientsPerIP}]", Color.White);
                args.Player.SendMessage($"[c/FFFFFF:The maximum seconds until spawn is necessary is:] [c/00FF00:{Watchdog.Configuration.MaxSecondsUntilSpawnNecessary}]", Color.White);
                args.Player.SendMessage($"[c/FFFFFF:The timeframe for packets in milliseconds is:] [c/00FF00:{Watchdog.Configuration.TimeframeForPacketsInMs}]", Color.White);

                args.Player.SendMessage("[c/FFD700:Options:]", Color.White);
                args.Player.SendMessage($"[c/FFA500:- block on/off] (Set to: {(Watchdog.Configuration.BlockTemporarilyOnTrip ? "[c/00FF00:ON]" : "[c/FF0000:OFF]")}) (Toggle blocking of client IPs temporarily on trip)", Color.White);
                args.Player.SendMessage($"[c/FFA500:- timeoutblock <valueInMilliseconds>] (Set to: [c/00FF00:{Watchdog.Configuration.TimeoutInMSUntilBlockRemoved}]ms) (Adjusts the timeout until an IP block is removed)", Color.White);
                args.Player.SendMessage($"[c/FFA500:- disconnectall on/off] (Set to: {(Watchdog.Configuration.DisconnectAllFromSameIPOnBlock ? "[c/00FF00:ON]" : "[c/FF0000:OFF]")}) (Toggle disconnection of all clients from the same IP on block)", Color.White);
                args.Player.SendMessage($"[c/FFA500:- kick on/off] (Set to: {(Watchdog.Configuration.KickOnTrip ? "[c/00FF00:ON]" : "[c/FF0000:OFF]")}) (Toggle kicking players on trip)", Color.White);
                args.Player.SendMessage($"[c/FFA500:- packetlength on/off] (Set to: {(Watchdog.Configuration.EnforcePacketLengthLimits ? "[c/00FF00:ON]" : "[c/FF0000:OFF]")}) (Toggle enforcement of packet length limits)", Color.White);
                args.Player.SendMessage($"[c/FFA500:- spawn on/off] (Set to: {(Watchdog.Configuration.EnforceSpawningPlayer ? "[c/00FF00:ON]" : "[c/FF0000:OFF]")}) (Toggle enforcement of player spawning)", Color.White);
                args.Player.SendMessage($"[c/FFA500:- max on/off] (Set to: {(Watchdog.Configuration.EnforceMaxClients ? "[c/00FF00:ON]" : "[c/FF0000:OFF]")}) (Toggle enforcement of max clients)", Color.White);
                args.Player.SendMessage($"[c/FFA500:- kickhandshake on/off] (Set to: {(Watchdog.Configuration.KickPacketsFromHandshakeBypass ? "[c/00FF00:ON]" : "[c/FF0000:OFF]")}) (Toggle kicking clients who attempt to bypass handshake)", Color.White);
                args.Player.SendMessage($"[c/FFA500:- maxclients <value>] (Set to: [c/00FF00:{Watchdog.Configuration.MaxClientsPerIP}]) (Set max clients per IP)", Color.White);
                args.Player.SendMessage($"[c/FFA500:- maxspawn <value>] (Set to: [c/00FF00:{Watchdog.Configuration.MaxSecondsUntilSpawnNecessary}]) (Set max seconds until spawn is necessary)", Color.White);
                args.Player.SendMessage($"[c/FFA500:- timeframe <value>] (Set to: [c/00FF00:{Watchdog.Configuration.TimeframeForPacketsInMs}]) (Set timeframe for packet analysis in ms)", Color.White);
                args.Player.SendMessage("[c/87CEEB:- Example:] [c/FFFFFF:/nobots config timeoutblock 5000]", Color.White);
                return;
            }

            if (parameters.Count() >= 2)
                HandleRegularSubcommand(args, parameters);
        }

        public void HandleNoBotsManagement(CommandArgs args)
        {
            if (Watchdog.Configuration == null)
                return;

            if (!File.Exists(Path.Combine(TShock.SavePath, "NoBots.json")))
                Watchdog.Configuration.Setup();

            var parameters = args.Parameters;

            if (parameters.Count() == 0)
            {
                args.Player.SendMessage($"[c/FFFFFF:NoBots is] {(Watchdog.Configuration.Enabled ? ("[c/00FF00:Enabled]") : ("[c/FF0000:Disabled]"))}. Type /nobots [c/00FFFF:help] for further information.", 255, 255, 255);
                return;
            }

            switch (parameters[0].ToLower())
            {
                default:
                    args.Player.SendErrorMessage("Invalid command. Use /nobots help for options.");
                    break;
                case "help":
                    {
                        args.Player.SendMessage("[c/00FFFF:=== NoBots Help ===]", Color.White);
                        args.Player.SendMessage("[c/FFFFFF:Usage: /nobots <option>]", Color.White);
                        args.Player.SendMessage("[c/FFD700:Features:]", Color.White);
                        args.Player.SendMessage("[c/FFA500:- Blocks bots from joining the server]", Color.White);
                        args.Player.SendMessage("[c/FFA500:- Prevents multiple clients from the same IP]", Color.White);
                        args.Player.SendMessage("[c/FFA500:- Protects against packet spam & certain exploits]", Color.White);
                        args.Player.SendMessage("[c/FFA500:- Adjustable limits for packet frequency and abuse detection]", Color.White);
                        args.Player.SendMessage("[c/FFA500:- Enforces spawn & handshake integrity]", Color.White);
                        args.Player.SendMessage("[c/FFD700:Options:]", Color.White);
                        args.Player.SendMessage("[c/FF0000:- off/disable:] [c/FFFFFF:Turns off NoBots protection]", Color.White);
                        args.Player.SendMessage("[c/00FF00:- on/enable:] [c/FFFFFF:Turns on NoBots protection]", Color.White);
                        args.Player.SendMessage("[c/00FFFF:- reload:] [c/FFFFFF:Reloads NoBots configuration]", Color.White);
                        args.Player.SendMessage("[c/CC66FF:- config:] [c/FFFFFF:Manages NoBots configuration]", Color.White);
                        args.Player.SendMessage("[c/00BFFF:- packetlengths:] [c/FFFFFF:Modifies packet types with their min/max allowed lengths and displays current values]", Color.White);
                        args.Player.SendMessage("[c/32CD32:- packetsmax:] [c/FFFFFF:Controls packet type limits within a set timeframe and shows current usage]", Color.White);
                        args.Player.SendMessage("[c/87CEEB:- Example:] [c/FFFFFF:/nobots enable]", Color.White);
                    }
                    break;
                case "on":
                case "enable":
                case "off":
                case "disable":
                    bool newState = parameters[0].ToLower() == "on" || parameters[0].ToLower() == "enable";
                    Watchdog.Configuration.Enabled = newState;
                    Watchdog.Configuration.Save();
                    args.Player.SendMessage($"[c/FFFFFF:NoBots has been] {(newState ? "[c/00FF00:Enabled]" : "[c/FF0000:Disabled]")}.", 255, 255, 255);
                    break;
                case "reload":
                    Watchdog.Configuration.Reload();
                    args.Player.SendMessage($"[c/FFFFFF:NoBots's configuration has been] [c/00FF00:reloaded].", 255, 255, 255);
                    break;
                case "config":
                    HandleConfig(args, parameters);
                    break;
                case "packetlengths":
                    HandlePacketLengthsCommand(args, parameters);
                    break;
                case "packetsmax":
                    HandlePacketsMaxCommand(args, parameters);
                    break;
            }
        }
    }
}
