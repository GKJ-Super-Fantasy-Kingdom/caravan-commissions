using System;
using System.IO;
using System.Collections.Generic;
using SuperFantasyKingdom;
using UnityEngine;

namespace CaravanCommissions
{
    internal static class ChoiceFrame
    {
        // Shared MIT-licensed CoinFrame artwork from Hold the Line's OfferCard.
        private static readonly Lazy<Sprite> Art = new Lazy<Sprite>(() => Load("ChoiceFrame"));
        private static readonly Lazy<Sprite> Review = new Lazy<Sprite>(() => Load("ReviewFrame"));
        private static readonly Lazy<Sprite> Landscape = new Lazy<Sprite>(() => Load("KingdomInset"));
        private static readonly Lazy<Sprite> Nameplate = new Lazy<Sprite>(() => Load("NameplateInset"));
        internal static Sprite Bronze => Art.Value;
        internal static Sprite Silver => Review.Value;
        private static readonly Lazy<Sprite> BronzeRim = new Lazy<Sprite>(() => Load("ChoiceFrameRim"));
        private static readonly Lazy<Sprite> SilverRim = new Lazy<Sprite>(() => Load("ReviewFrameRim"));
        private static readonly Lazy<Sprite> PreviousRim = new Lazy<Sprite>(() => Load("PreviousPoolRim"));
        private static readonly Lazy<Sprite> NextRim = new Lazy<Sprite>(() => Load("NextPoolRim"));
        private static readonly Dictionary<Races, (Sprite Scene, Sprite Plate)> KingdomArt =
            new Dictionary<Races, (Sprite, Sprite)>();

        internal static Sprite Rim(bool future) => future ? SilverRim.Value : BronzeRim.Value;
        internal static Sprite ArrowRim(bool previous) => previous ? PreviousRim.Value : NextRim.Value;

        internal static (Sprite Scene, Sprite Plate) ForKingdom(Races kingdom)
        {
            if (!KingdomArt.TryGetValue(kingdom, out var art))
            {
                art = (Load("Kingdom" + kingdom) ?? Landscape.Value,
                    Load("Nameplate" + kingdom) ?? Nameplate.Value);
                KingdomArt.Add(kingdom, art);
            }
            return art;
        }

        private static Sprite Load(string name)
        {
            using var stream = typeof(ChoiceFrame).Assembly.GetManifestResourceStream("CaravanCommissions." + name + ".png");
            if (stream == null) return null;
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, bytes.ToArray(), true))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.HideAndDontSave;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
