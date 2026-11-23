using System;
using System.Collections.Generic;
using System.Linq;

namespace CaravanCommissions
{
    internal static class OfferIds
    {
        // The native pool stores prefab keys. The card and inventory use Item.GetIdentifier().
        // Keep only canonical IDs that the game can also resolve for a later spawn.
        internal static string[] FromPool(IEnumerable<string> keys, Func<string, string> identify) =>
            Weighted(keys, identify).Distinct().ToArray();

        internal static string[] Weighted(IEnumerable<string> keys, Func<string, string> identify)
        {
            var result = new List<string>();
            foreach (var key in keys ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrEmpty(key)) continue;
                var id = identify(key);
                if (!string.IsNullOrEmpty(id) && identify(id) == id) result.Add(id);
            }
            return result.ToArray();
        }

        // Match the native successive draws for regular pools. Also terminate for
        // tiny pools and for a patched random source that repeats one index.
        internal static string[] Draw(string[] available, Func<int, int> random)
        {
            var chosen = new List<string>();
            int count = Math.Min(available.Distinct().Count(), 3);
            for (int i = 0; i < 64 && chosen.Count < count; i++)
            {
                int index = random(available.Length);
                if (index < 0 || index >= available.Length) break;
                if (!chosen.Contains(available[index])) chosen.Add(available[index]);
            }
            while (chosen.Count < count)
            {
                var remaining = available.Where(id => !chosen.Contains(id)).ToArray();
                int slot = random(remaining.Length);
                chosen.Add(remaining[slot >= 0 && slot < remaining.Length ? slot : 0]);
            }
            return chosen.ToArray();
        }

        internal static IEnumerable<string[]> Pages(string[] ids)
        {
            if (ids.Length < 3) yield break;
            for (int offset = 0; offset < ids.Length; offset += 3)
                yield return new[] { ids[offset], ids[(offset + 1) % ids.Length], ids[(offset + 2) % ids.Length] };
        }

        internal static bool Available(string id, Func<string, string> identify) =>
            !string.IsNullOrEmpty(id) && identify(id) == id;
    }
}
