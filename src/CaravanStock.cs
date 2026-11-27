using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using UnityEngine;

namespace CaravanCommissions
{
    internal sealed class Pool
    {
        internal string Title;
        internal Races Kingdom;
        internal string[] Ids;
        internal Item[] Items;
        private static readonly MethodInfo GetItems = AccessTools.DeclaredMethod(typeof(GuestCaravan), "GetItems");
        private static readonly AccessTools.FieldRef<GuestCaravan, Races> GuestKingdom =
            AccessTools.FieldRefAccess<GuestCaravan, Races>("m_Kingdom");

        internal static Races VisitingKingdom(GuestCaravan guest) => GuestKingdom(guest);

        internal static string Identify(string key) => AddressablesManager.Instance.GetItem(key)?.GetIdentifier();
        internal static List<string> Stock(GuestCaravan guest) => (List<string>)GetItems.Invoke(guest, null);

        internal static List<Pool> Eligible(GuestCaravan guest)
        {
            var pools = new List<Pool>();
            var original = GuestKingdom(guest);
            // GetItems reads the visitor's kingdom. Query it synchronously so patched
            // getters and runtime pool replacements participate just as they do on arrival.
            try
            {
                foreach (Races race in Enum.GetValues(typeof(Races)))
                {
                    if (race == Races.None || race == RaceManager.Instance.GetRace()) continue;
                    GuestKingdom(guest) = race;
                    var pages = OfferIds.Pages(OfferIds.FromPool(Stock(guest), Identify)).ToArray();
                    for (int i = 0; i < pages.Length; i++)
                    {
                        var title = Helper.Instance.GetKingdomTitle(race);
                        if (pages.Length > 1) title += $" ({i + 1}/{pages.Length})";
                        pools.Add(new Pool { Title = title, Kingdom = race, Ids = pages[i],
                            Items = pages[i].Select(AddressablesManager.Instance.GetItem).ToArray() });
                    }
                }
            }
            finally { GuestKingdom(guest) = original; }
            return pools;
        }
    }

    internal static class DeliveryCards
    {
        private static readonly AccessTools.FieldRef<UIOverlayMerchant, GameObject> Prefab =
            AccessTools.FieldRefAccess<UIOverlayMerchant, GameObject>("itemCard");
        internal static GameObject ItemCardPrefab(UIOverlayMerchant screen) => Prefab(screen);
        private static readonly AccessTools.FieldRef<UIOverlayMerchant, UIEffectKeywordDictionary> Keywords =
            AccessTools.FieldRefAccess<UIOverlayMerchant, UIEffectKeywordDictionary>("keywordDictionary");
        private static readonly MethodInfo GetSelection = AccessTools.DeclaredMethod(typeof(UIOverlayMerchant), "GetSelection");
        private static readonly MethodInfo Select = AccessTools.DeclaredMethod(typeof(UIOverlayMerchant), "Select", new[] { typeof(UICard) });

        private static bool Grant(string id)
        {
            var manager = ItemManager.Instance;
            return ObserveSpawn(manager, id, key => manager.SpawnDragItem(key, new Vector2(0, 3)));
        }

        internal static bool ObserveSpawn(ItemManager manager, string id, Action<string> spawn)
        {
            var before = manager.GetDragItems().ToArray();
            try { spawn(id); }
            catch (Exception ex) { Plugin.Log?.LogWarning("Commission spawn hook failed: " + ex.Message); }
            // A provider can suppress the spawn, or throw after adding the item.
            return manager.GetDragItems().Any(drop => !(drop is null) && drop.GetItemIdentifier() == id &&
                !before.Any(previous => ReferenceEquals(previous, drop)));
        }

        // A prepaid reward uses the native card, focus, keywords and selection animation.
        // Shop AddItem/AddBuyButton patches apply exclusively to actual purchases.
        internal static void Show(UIOverlayMerchant screen, Plugin.Visit visit)
        {
            var promised = Plugin.Book.items;
            foreach (var id in promised)
            {
                if (!OfferIds.Available(id, Pool.Identify)) continue;
                var parent = (Transform)GetSelection.Invoke(screen, null);
                var card = UnityEngine.Object.Instantiate(Prefab(screen), parent).GetComponent<UICardItem>();
                card.Init(AddressablesManager.Instance.GetItem(id), Keywords(screen), true, () =>
                {
                    if (!visit.Available || Plugin.Book.items != promised || Plugin.Panel != null ||
                        !Plugin.IsCurrent(screen) || !Plugin.Owns(screen, visit)) return;
                    if (!Plugin.Book.Claim(visit.Day, visit.Number, promised, id, Pool.Identify, Grant))
                    {
                        // The content changed while the card animation was playing.
                        // Close the animated, disabled picker and retain the paid order.
                        OverlayManager.Instance.Cancel(true);
                        return;
                    }
                    OverlayManager.Instance.Confirm();
                    GameCamera.Instance.StartCutscene(Vector2.zero, 0f, GameCamera.Instance.EndCutscene,
                        reduceMusicVolume: true, forced: true);
                }, false);
                var button = card.GetCardButton();
                button.onClick.AddListener(() => Select.Invoke(screen, new object[] { card }));
                GamepadManager.Instance.SetGameObjectToSelect(button.gameObject);
            }
        }
    }
}
