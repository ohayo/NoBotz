using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace NoBotz.Misc
{
    public class HumanPlayer
    {
        public TSPlayer Player { get; set; }

        public HumanPlayer(TSPlayer player)
        {
            Player = player;
        }
    }
}
