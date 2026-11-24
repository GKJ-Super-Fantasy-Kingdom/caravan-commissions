using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using UnityEngine;

namespace CaravanCommissions
{
    [BepInPlugin("gkj.sfk.caravancommissions", "Caravan Commissions", "0.1.3")]
    [BepInDependency("sfk.dailycaravan", BepInDependency.DependencyFlags.SoftDependency)]
    internal sealed class Plugin : BaseUnityPlugin
    {
        internal static OrderBook Book = new OrderBook();
        private static GameDataManager bookScene;
        internal static bool IsCurrentRun => bookScene && ReferenceEquals(bookScene, GameDataManager.Instance);
        internal static CommissionPanel Panel;
        private static ConditionalWeakTable<UIOverlayMerchant, Visit> screens = new ConditionalWeakTable<UIOverlayMerchant, Visit>();
        private static ConditionalWeakTable<GuestCaravan, Visit> arrivals = new ConditionalWeakTable<GuestCaravan, Visit>();
        private static Visit opening;
        private static GuestCaravan scheduled;
        private static Func<bool> dailyDispatch;
        internal static ManualLogSource Log;
        private Harmony harmony;
        private static readonly AccessTools.FieldRef<UIOverlayMerchant, Action<int, ResourceAmount[]>> Callback =
            AccessTools.FieldRefAccess<UIOverlayMerchant, Action<int, ResourceAmount[]>>("m_Callback");
        private static readonly AccessTools.FieldRef<UIOverlayMerchant, Action<UIOverlayMerchant>> CancelCallback =
            AccessTools.FieldRefAccess<UIOverlayMerchant, Action<UIOverlayMerchant>>("m_CallbackCancel");
        internal static bool Owns(UIOverlayMerchant screen, Visit visit) =>
            screens.TryGetValue(screen, out var bound) && ReferenceEquals(bound, visit);

        private static readonly AccessTools.FieldRef<OverlayManager, UIOverlayBase> CurrentOverlay =
            AccessTools.FieldRefAccess<OverlayManager, UIOverlayBase>("m_CurrentlyOpen");
        private static readonly AccessTools.FieldRef<OverlayManager, UIOverlayBase> BackupOverlay =
            AccessTools.FieldRefAccess<OverlayManager, UIOverlayBase>("m_Backup");

        internal static int ConfirmedDeliveryDay()
        {
            var overlays = OverlayManager.Instance;
            var clock = DaytimeManager.Instance;
            if (!overlays || !clock) return -1;
            int today = clock.CurrentDay();
            bool Eligible(UIOverlayBase overlay) => overlay is UIOverlayMerchant merchant &&
                screens.TryGetValue(merchant, out var visit) && visit.Day == today && Book.Available(today, visit.Number);
            return (overlays.IsOpen() && Eligible(CurrentOverlay(overlays))) ||
                (overlays.HasMinimizedOverlay() && Eligible(BackupOverlay(overlays))) ? today : -1;
        }
        internal static bool IsCurrent(UIOverlayMerchant screen) => OverlayManager.Instance != null &&
            screen != null && screen.isActiveAndEnabled && ReferenceEquals(CurrentOverlay(OverlayManager.Instance), screen);

        internal static bool Missing => Book.Pending && Book.items.Any(id => !OfferIds.Available(id, Pool.Identify));

        private void Awake()
        {
            Log = Logger;
            if (Chainloader.PluginInfos.TryGetValue("sfk.dailycaravan", out var daily) && daily.Instance != null)
            {
                var getter = AccessTools.PropertyGetter(daily.Instance.GetType(), "Launching");
                if (getter?.ReturnType == typeof(bool))
                    dailyDispatch = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), daily.Instance, getter);
            }
            harmony = new Harmony("gkj.sfk.caravancommissions");
            harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo("Caravan Commissions 0.1.3 loaded");
        }

        private void OnDestroy()
        {
            if (Panel != null) Panel.Dismiss();
            harmony?.UnpatchSelf();
            bookScene = null;
            dailyDispatch = null;
            Log = null;
        }

        internal static void Reset(OrderBook saved)
        {
            if (Panel != null) Panel.Dismiss();
            Book = saved ?? new OrderBook();
            bookScene = GameDataManager.Instance;
            arrivals = new ConditionalWeakTable<GuestCaravan, Visit>();
            screens = new ConditionalWeakTable<UIOverlayMerchant, Visit>();
            opening = null;
            scheduled = null;
        }

        internal sealed class Visit
        {
            internal GuestCaravan Guest;
            internal int Day, Number;
            internal bool Available => Book.Available(Day, Number) && DaytimeManager.Instance.CurrentDay() == Day;
        }

        private static Visit Arrive(GuestCaravan guest, int day)
        {
            bool dawn = ReferenceEquals(scheduled, guest) || dailyDispatch?.Invoke() == true;
            var visit = new Visit { Guest = guest, Number = Book.StartVisit(day, dawn), Day = day };
            arrivals.Remove(guest);
            arrivals.Add(guest, visit);
            return visit;
        }

        [HarmonyPatch(typeof(GuestCaravan), "OnLoad")]
        private static class ScheduledArrival
        {
            static void Prefix(GuestCaravan __instance, out GuestCaravan __state)
            {
                __state = scheduled;
                scheduled = __instance;
            }
            static void Finalizer(GuestCaravan __state) => scheduled = __state;
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
            static bool Prefix(GuestCaravan __instance, out Visit __state)
            {
                __state = opening;
                opening = Current(__instance);
                return opening.Available;
            }
            static void Finalizer(Visit __state) => opening = __state;
            [HarmonyPriority(Priority.Last)]
            static void Postfix(GuestCaravan __instance, bool __runOriginal)
            {
                if (!__runOriginal || !Current(__instance).Available) return;
                var screen = OverlayManager.Instance.GetMerchant();
                if (screen == null || !Owns(screen, Current(__instance))) return;
                if (Book.Pending)
                {
                    screen.SetHeaderText("Commission Delivery");
                    screen.SetText(Missing ? "Some promised items are unavailable. Choose a remaining card, exchange the order for free, or go back to defer." :
                        "Choose one item from " + Book.pool + ", or keep your order for another visit.");
                    screen.SetCancel(Missing ? "Exchange Order" : "Defer Delivery", CancelCallback(screen));
                }
                else screen.SetCancel("Compare Commissions", CancelCallback(screen));
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
            private static readonly Func<Guest, int, int> RandomIndex = AccessTools.MethodDelegate<Func<Guest, int, int>>(
                AccessTools.DeclaredMethod(typeof(Guest), "GetRandomValue"));

            [HarmonyPriority(Priority.Last)]
            [HarmonyAfter("sfk.dailycaravan")]
            static bool Prefix(GuestCaravan __instance, UIOverlayMerchant merchant, bool __runOriginal,
                ref bool doChangeInventory, List<string> ___m_Chosen)
            {
                if (!__runOriginal) return true;
                if (!screens.TryGetValue(merchant, out var visit))
                {
                    visit = opening;
                    if (visit == null)
                    {
                        // A provider can invoke the guest's reroll directly on a native picker.
                        if (!ReferenceEquals(Callback(merchant)?.Target, __instance) ||
                            !ReferenceEquals(CancelCallback(merchant)?.Target, __instance)) return true;
                        visit = Current(__instance);
                    }
                    if (!ReferenceEquals(visit.Guest, __instance)) return true;
                    screens.Add(merchant, visit);
                }
                if (!ReferenceEquals(visit.Guest, __instance)) return true;
                if (Book.Pending)
                {
                    ___m_Chosen.Clear();
                    if (visit.Available) DeliveryCards.Show(merchant, visit);
                    // Keep Daily Caravan's ordinary offer cache separate from prepaid cards.
                    return false;
                }

                var raw = doChangeInventory ? Pool.Stock(__instance) : ___m_Chosen;
                var usable = OfferIds.Weighted(raw, Pool.Identify);
                // Query the patched getter once. Native Reroll(false) renders exactly
                // this snapshot, including Daily Caravan's restored selection.
                var chosen = doChangeInventory ? OfferIds.Draw(usable, n => RandomIndex(__instance, n)) : usable.Distinct();
                ___m_Chosen.Clear();
                ___m_Chosen.AddRange(chosen.Take(3));
                doChangeInventory = false;
                return true;
            }
        }

        internal static readonly ConditionalWeakTable<UICardItem, ResourceAmount[]> CardPrices =
            new ConditionalWeakTable<UICardItem, ResourceAmount[]>();

        [HarmonyPatch(typeof(UIOverlayMerchant), nameof(UIOverlayMerchant.AddItem))]
        private static class ObservePrice
        {
            static void Postfix(UIOverlayMerchant __instance, GameObject __result, ResourceAmount[] costs)
            {
                if (!screens.TryGetValue(__instance, out _) || __result == null || costs == null || costs.Length == 0) return;
                var card = __result.GetComponent<UICardItem>();
                if (card == null) return;
                CardPrices.Remove(card);
                CardPrices.Add(card, costs.ToArray());
            }
        }

        [HarmonyPatch(typeof(UIOverlayMerchant), nameof(UIOverlayMerchant.Cancel))]
        private static class Compare
        {
            static bool Prefix(UIOverlayMerchant __instance)
            {
                if (!screens.TryGetValue(__instance, out var visit) || !Owns(__instance, visit)) return true;
                if (Panel != null) return false;
                if (!visit.Available || (Book.Pending && !Missing))
                {
                    if (visit.Available) Book.Finish(visit.Day, visit.Number, false);
                    OverlayManager.Instance.Cancel();
                    return false;
                }
                CommissionPanel.Open(__instance, visit);
                return false;
            }
        }

        internal static void Leave(UIOverlayMerchant screen, Visit visit)
        {
            if (!Book.Pending && Owns(screen, visit))
            {
                // Preserve GuestCaravan.Cancel and any decline observer, including
                // Daily Caravan's same-dawn replay record for an ordinary visit.
                var cancel = CancelCallback(screen);
                if (cancel == null) OverlayManager.Instance.Cancel(true);
                else cancel(screen);
                return;
            }
            if (Book.Pending) Book.Finish(visit.Day, visit.Number, false);
            OverlayManager.Instance.Cancel(true);
        }

        [HarmonyPatch(typeof(OverlayManager), nameof(OverlayManager.Cancel))]
        private static class Back
        {
            static bool Prefix(bool force, UIOverlayBase ___m_CurrentlyOpen)
            {
                if (!force && CommissionPanel.LastBackFrame == Time.frameCount) return false;
                if (Panel != null)
                {
                    if (Panel.OwnsCurrentScreen) { Panel.Back(); return false; }
                    Panel.Dismiss();
                }
                // The game's Back action passes force=false on a locked merchant.
                if (!force && OverlayManager.Instance.IsOpen())
                {
                    var merchant = ___m_CurrentlyOpen as UIOverlayMerchant;
                    if (merchant != null && merchant.isActiveAndEnabled &&
                        screens.TryGetValue(merchant, out var visit) && Owns(merchant, visit) && visit.Available)
                    {
                        Leave(merchant, visit);
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
                if (merchant == null || !screens.TryGetValue(merchant, out var visit) || !Owns(merchant, visit)) return true;
                if (!visit.Available || Panel != null || Book.Pending)
                    return false;
                if (Ready(__instance)) __state = visit;
                return true;
            }

            // Native Select spawns the draggable item before it returns.
            static void Postfix(Visit __state, bool __runOriginal)
            {
                if (__runOriginal && __state != null) Book.Finish(__state.Day, __state.Number, true);
            }
        }
    }

}
