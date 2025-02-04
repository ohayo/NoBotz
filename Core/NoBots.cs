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

            switch(parameters[0].ToLower())
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
            }
        }
    }
}
