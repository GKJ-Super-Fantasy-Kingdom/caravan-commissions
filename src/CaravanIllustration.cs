using System.Collections.Generic;
using SuperFantasyKingdom;
using UnityEngine;

namespace CaravanCommissions
{
    internal static class CaravanIllustration
    {
        // First idle frames from the installed GuestCaravan animations.
        private static readonly Dictionary<string, Races> Kingdoms = new Dictionary<string, Races>
        {
            ["Caravans_0"] = Races.Human,
            ["Caravans_8"] = Races.Undead,
            ["Caravans_16"] = Races.Swamp,
            ["Caravans_24"] = Races.Desert,
            ["Caravans_32"] = Races.Demon,
            ["Caravans_40"] = Races.Dwarves,
            ["Caravans_48"] = Races.Barbarians,
            ["Caravans_56"] = Races.Nature,
            ["Caravans_64"] = Races.Elves,
        };
        private static readonly Dictionary<Races, Sprite> Sprites = new Dictionary<Races, Sprite>();

        internal static Sprite Get(Races kingdom)
        {
            if (!Kingdoms.ContainsValue(kingdom)) return null;
            if (Sprites.TryGetValue(kingdom, out var sprite) && sprite != null) return sprite;

            // The caravan's native animation controllers reference all nine kingdoms.
            foreach (var candidate in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (Kingdoms.TryGetValue(candidate.name, out var race))
                    Sprites[race] = candidate;
            }
            return Sprites.TryGetValue(kingdom, out sprite) ? sprite : null;
        }
    }
}
