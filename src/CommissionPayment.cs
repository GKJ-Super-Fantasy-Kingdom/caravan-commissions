using System;
using System.Collections.Generic;
using System.Linq;
using SuperFantasyKingdom;

namespace CaravanCommissions
{
    internal static class CommissionPayment
    {
        internal const int Price = 3;

        internal static ResourcePacket[] Units(IEnumerable<ResourcePacket> packets, ResourceType type) =>
            packets.Where(p => p != null && !p.IsReserved() && p.GetResourceType() == type && p.GetAmount() == 1).ToArray();

        internal static bool Take(ResourcePacket[] units, ResourceStorage storage, Func<int, ResourcePacket> take,
            Action<ResourcePacket> restore)
        {
            if (units.Length < Price) return false;
            var attempted = new List<ResourcePacket>(Price);
            bool paid = false;
            try
            {
                foreach (var packet in units.Take(Price))
                {
                    if (packet.IsReserved() || !ReferenceEquals(storage.GetResourcePacket(packet.GetIdentifier()), packet))
                        return false;
                    attempted.Add(packet);
                    var actual = take(packet.GetIdentifier());
                    if (!ReferenceEquals(actual, packet))
                    {
                        if (actual != null) attempted.Add(actual);
                        return false;
                    }
                }
                paid = attempted.All(p => storage.GetResourcePacket(p.GetIdentifier()) == null);
                return paid;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("Commission payment hook failed: " + ex.Message);
                return false;
            }
            finally
            {
                if (!paid)
                    foreach (var packet in attempted)
                    {
                        if (storage.GetResourcePacket(packet.GetIdentifier()) != null) continue;
                        try { restore(packet); }
                        catch (Exception ex) { Plugin.Log?.LogWarning("Commission refund hook failed: " + ex.Message); }
                        if (storage.GetResourcePacket(packet.GetIdentifier()) == null) storage.AddResourcePacket(packet);
                    }
            }
        }

        internal static bool TakeStarting(Dictionary<ResourceType, int> stock, ResourceType type)
        {
            if (!stock.TryGetValue(type, out var balance) || balance < Price) return false;
            stock[type] = balance - Price;
            return true;
        }
    }
}
