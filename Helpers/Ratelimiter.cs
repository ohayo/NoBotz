using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoBotz.Helpers
{
    public static class Ratelimiter
    {
        private static readonly Dictionary<int, Dictionary<int, (int count, DateTime timestamp)>> PlayerPacketCounts = new();

        public static Dictionary<byte, HashSet<byte>> Notified = new();

        public static bool PacketBeginsWithPlayerId(int packetType)
        {
            if (Watchdog.Configuration == null)
                return false;

            HashSet<int> packetsWithPlayerId = new()
            {
                22, 24, 27, 29, 30, 35, 36, 40, 43, 45, 50, 51, 55, 58, 62, 66,
                96, 99, 102, 121, 124, 125, 128, 134
            };

            return packetsWithPlayerId.Contains(packetType);
        }

        public static int CheckRateLimit(int playerId, int packetId)
        {
            if (Watchdog.Configuration == null || !Watchdog.Configuration.Enabled)
                return -1;

            var PacketRateLimits = Watchdog.Configuration.PacketTypeToMaxPerTimeFrame;

            if (!PacketRateLimits.TryGetValue(packetId, out int maxAllowed))
                return -1;

            if (!PlayerPacketCounts.ContainsKey(playerId))
                PlayerPacketCounts[playerId] = new Dictionary<int, (int, DateTime)>();

            if (!Notified.ContainsKey((byte)playerId))
                Notified[(byte)playerId] = new HashSet<byte>();

            var now = DateTime.UtcNow;
            int previousCount = 0;

            if (PlayerPacketCounts[playerId].TryGetValue(packetId, out var entry))
            {
                var (count, timestamp) = entry;
                previousCount = count;

                if ((now - timestamp).TotalMilliseconds >= Watchdog.Configuration.TimeframeForPacketsInMs)
                {
                    PlayerPacketCounts[playerId][packetId] = (1, now);

                    if (Notified.TryGetValue((byte)playerId, out var notifiedPackets))
                        notifiedPackets.Remove((byte)packetId);
                }
                else
                    PlayerPacketCounts[playerId][packetId] = (count + 1, timestamp);

                if (previousCount + 1 > maxAllowed)
                    return previousCount;
            }
            else
            {
                PlayerPacketCounts[playerId][packetId] = (1, now);
            }

            return -1;
        }

        public static bool HasNotifiedOfAbuse(byte packetType, byte playerId)
        {
            return Notified.TryGetValue(playerId, out var notifiedPackets) && notifiedPackets.Contains(packetType);
        }
    }
}
