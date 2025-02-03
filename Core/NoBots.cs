using IL.Terraria;
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

            Commands.ChatCommands.Add(new Command(Permissions.cfgreload, HandleConfigReload, "nobots-reload"));

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

        public void HandleConfigReload(CommandArgs args)
        {
            if (Watchdog.Configuration == null)
                return;

            if (!File.Exists(Path.Combine(TShock.SavePath, "NoBots.json")))
                Watchdog.Configuration.Setup();

            Watchdog.Configuration.Reload();

            args.Player.SendSuccessMessage("NoBotz configuration has been reloaded successfully.");
        }
    }
}
