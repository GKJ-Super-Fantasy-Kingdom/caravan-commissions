using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using UnityEngine;

namespace CaravanCommissions
{
    [BepInPlugin("gkj.sfk.caravancommissions", "Caravan Commissions", "0.1.0")]
    internal sealed class Plugin : BaseUnityPlugin
    {
        internal static OrderBook Book = new OrderBook();
        internal static CommissionPanel Panel;
        private static ConditionalWeakTable<UIOverlayMerchant, Visit> screens = new ConditionalWeakTable<UIOverlayMerchant, Visit>();
        private static ConditionalWeakTable<GuestCaravan, Visit> arrivals = new ConditionalWeakTable<GuestCaravan, Visit>();
        private static int lastDay = -1, sequence;
        private Harmony harmony;

        private void Awake()
        {
            harmony = new Harmony("gkj.sfk.caravancommissions");
            harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo("Caravan Commissions 0.1.0 loaded");
        }

        private void OnDestroy()
        {
            if (Panel != null) Panel.Dismiss();
            harmony?.UnpatchSelf();
        }

        internal static void Reset(OrderBook saved)
        {
            if (Panel != null) Panel.Dismiss();
            Book = saved ?? new OrderBook();
            arrivals = new ConditionalWeakTable<GuestCaravan, Visit>();
            screens = new ConditionalWeakTable<UIOverlayMerchant, Visit>();
            lastDay = -1;
            sequence = 0;
        }

        internal sealed class Visit
        {
            internal GuestCaravan Guest;
            internal int Day, Number;
            internal bool Available => Book.Available(Day, Number) && DaytimeManager.Instance.CurrentDay() == Day;
        }

        private static Visit Arrive(GuestCaravan guest, int day)
        {
            if (lastDay != day) { lastDay = day; sequence = 0; }
            Book.BeginDay(day);
            var visit = new Visit { Guest = guest, Number = ++sequence, Day = day };
            arrivals.Remove(guest);
            arrivals.Add(guest, visit);
            return visit;
        }

        // Reopening the picker does not call the base arrival routine. A reputation
        // caravan does, even when it reuses the same GuestCaravan on the same day.
        [HarmonyPatch(typeof(Guest), "OnLoad")]
        private static class Arrival
        {
            static void Prefix(Guest __instance, int day, bool ___m_Started)
            {
                if (!___m_Started && __instance is GuestCaravan caravan) Arrive(caravan, day);
            }
        }

        private static Visit Current(GuestCaravan guest)
        {
            int day = DaytimeManager.Instance.CurrentDay();
            return arrivals.TryGetValue(guest, out var visit) && visit.Day == day ? visit : Arrive(guest, day);
        }

        [HarmonyPatch(typeof(GuestCaravan), "OnCaravan")]
        private static class Open
        {
            static bool Prefix(GuestCaravan __instance) => Current(__instance).Available;
            static void Postfix(GuestCaravan __instance)
            {
                if (!Current(__instance).Available) return;
                var screen = OverlayManager.Instance.GetMerchant();
                if (!screens.TryGetValue(screen, out _)) return;
                if (Book.Pending)
                {
                    screen.SetHeaderText("Commission delivery");
                    screen.SetText("Choose one item from " + Book.pool + ", or keep your order for another visit.");
                    screen.SetCancel("Defer delivery");
                }
                else screen.SetCancel("Compare commissions");
            }
        }

        [HarmonyPatch(typeof(UIOverlayMerchant), nameof(UIOverlayMerchant.Init))]
        private static class Reuse
        {
            static void Prefix(UIOverlayMerchant __instance) => screens.Remove(__instance);
        }

        [HarmonyPatch(typeof(GuestCaravan), "Reroll")]
        private static class Inventory
        {
            [HarmonyAfter("sfk.dailycaravan")]
            static bool Prefix(GuestCaravan __instance, UIOverlayMerchant merchant,
                List<string> ___m_Chosen)
            {
                if (!screens.TryGetValue(merchant, out var visit))
                {
                    visit = Current(__instance);
                    screens.Add(merchant, visit);
                }
                if (!Book.Pending) return true;
                var promised = Book.items;
                ___m_Chosen.Clear();
                ___m_Chosen.AddRange(promised);
                // The native cards call our delivery callback. Daily Caravan records
                // ordinary card selections for dawn replay. A commissioned claim is
                // already captured with the item in the native save checkpoint.
                merchant.SetUseCardSelectCallback(false);
                merchant.SetCallback((index, costs) =>
                {
                    if (!visit.Available || Book.items != promised || index < 0 || index >= promised.Length) return;
                    ItemManager.Instance.SpawnDragItem(promised[index], new Vector2(0, 3));
                    Book.Finish(visit.Day, visit.Number, true);
                    OverlayManager.Instance.Confirm();
                    GameCamera.Instance.StartCutscene(Vector2.zero, 0f, GameCamera.Instance.EndCutscene,
                        reduceMusicVolume: true, forced: true);
                });
                try
                {
                    if (visit.Available)
                        for (int i = 0; i < promised.Length; i++)
                        {
                            var item = AddressablesManager.Instance.GetItem(promised[i]);
                            if (item != null) merchant.AddItem(item, id: i);
                        }
                }
                finally
                {
                    merchant.SetUseCardSelectCallback(true);
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(UIOverlayMerchant), nameof(UIOverlayMerchant.Cancel))]
        private static class Compare
        {
            static bool Prefix(UIOverlayMerchant __instance)
            {
                if (!screens.TryGetValue(__instance, out var visit)) return true;
                if (Panel != null) return false;
                if (!visit.Available || Book.Pending)
                {
                    if (visit.Available) Book.Finish(visit.Day, visit.Number, false);
                    OverlayManager.Instance.Cancel();
                    return false;
                }
                CommissionPanel.Open(__instance, visit);
                return false;
            }
        }

        [HarmonyPatch(typeof(OverlayManager), nameof(OverlayManager.Cancel))]
        private static class Back
        {
            static bool Prefix(bool force, UIOverlayBase ___m_CurrentlyOpen)
            {
                if (!force && CommissionPanel.LastBackFrame == Time.frameCount) return false;
                if (Panel != null) { Panel.Back(); return false; }
                // The game's Back action passes force=false on a locked merchant.
                if (!force && OverlayManager.Instance.IsOpen())
                {
                    var merchant = ___m_CurrentlyOpen as UIOverlayMerchant;
                    if (merchant != null && merchant.isActiveAndEnabled &&
                        screens.TryGetValue(merchant, out var visit) && visit.Available)
                    {
                        if (Book.Pending) Book.Finish(visit.Day, visit.Number, false);
                        OverlayManager.Instance.Cancel(true);
                        return false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(UICardItem), "Select")]
        private static class TakeItem
        {
            private static readonly AccessTools.FieldRef<UICard, bool> Ready =
                AccessTools.FieldRefAccess<UICard, bool>("m_CanBeClicked");

            static bool Prefix(UICardItem __instance, string ___m_ItemIdentifier, out Visit __state)
            {
                __state = null;
                var merchant = __instance.GetComponentInParent<UIOverlayMerchant>();
                if (merchant == null || !screens.TryGetValue(merchant, out var visit)) return true;
                if (!visit.Available || Panel != null || (Book.Pending && !Book.items.Contains(___m_ItemIdentifier)))
                    return false;
                if (Ready(__instance)) __state = visit;
                return true;
            }

            // Native Select spawns the draggable item before it returns.
            static void Postfix(Visit __state)
            {
                if (__state != null) Book.Finish(__state.Day, __state.Number, true);
            }
        }
    }

    internal sealed class Pool
    {
        internal string Title;
        internal string[] Ids;
        internal Item[] Items;
        private static readonly string[] Fields = { "humanItems", "demonItems", "undeadItems", "elvesItems",
            "natureItems", "mysticItems", "dwarvesItems", "barbarianItems", "reptiloidItems" };

        internal static List<Pool> Eligible(GuestCaravan guest)
        {
            var pools = new List<Pool>();
            for (int i = 0; i < Fields.Length; ++i)
            {
                var race = (Races)i;
                if (race == RaceManager.Instance.GetRace()) continue;
                var ids = (List<string>)AccessTools.Field(typeof(GuestCaravan), Fields[i]).GetValue(guest);
                if (ids == null) continue;
                var choices = ids.Where(id => !string.IsNullOrEmpty(id)).Distinct()
                    .Select(id => new { id, item = AddressablesManager.Instance.GetItem(id) })
                    .Where(choice => choice.item != null).Take(3).ToArray();
                if (choices.Length == 3)
                    pools.Add(new Pool { Title = Helper.Instance.GetKingdomTitle(race),
                        Ids = choices.Select(c => c.id).ToArray(), Items = choices.Select(c => c.item).ToArray() });
            }
            return pools;
        }
    }
}
