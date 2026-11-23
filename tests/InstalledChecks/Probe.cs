using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using BepInEx.Configuration;
using SuperFantasyKingdom;
using SuperFantasyKingdom.UI;
using UnityEngine;
using HarmonyLib;

// Offline probe. Uses released mod methods and installed game data structures.
// The catalog entries, elapsed days and item grant recorder are fixtures.
public static class CommissionProbe
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static Type Book, Payment, Offers, Pool;
    static int quotedPrice;
    static List<string> replacementPool;
    static bool throwAfterSpawn;

    static void SpawnFixture(string itemDataName)
    {
        if (!throwAfterSpawn) return;
        var drop = Blank<DragItem>();
        typeof(DragItem).GetField("m_ItemIdentifier", All).SetValue(drop, itemDataName);
        ItemManager.Instance.GetDragItems().Add(drop);
        throw new InvalidOperationException("provider failed after registering the drop");
    }

    static void GrantFaults(Assembly asm)
    {
        var drops = new List<DragItem>();
        var existing = Blank<DragItem>();
        typeof(DragItem).GetField("m_ItemIdentifier", All).SetValue(existing, "A");
        drops.Add(existing);
        ItemManager.Instance = Blank<ItemManager>();
        typeof(ItemManager).GetField("m_DragItems", All).SetValue(ItemManager.Instance, drops);
        var cards = asm.GetType("CaravanCommissions.DeliveryCards");
        Func<string,bool> grant = key => (bool)Call(cards, null, "ObserveSpawn",
            ItemManager.Instance, key, new Action<string>(SpawnFixture));
        var book = Activator.CreateInstance(Book, true);
        Call(Book, book, "BeginDay", 16);
        Call(Book, book, "Place", 16, 1, "test", new[] {"A","B","C"}, new Func<bool>(() => true));
        Call(Book, book, "BeginDay", 17);
        var ids = (string[])Book.GetField("items").GetValue(book);
        Func<string,string> identity = id => id;
        throwAfterSpawn = false;
        Check(!(bool)Call(Book, book, "Claim", 17, 1, ids, "A", identity, grant) && drops.Count == 1,
            "no-op spawn cannot consume a paid order even with a matching existing drop");
        throwAfterSpawn = true;
        Check((bool)Call(Book, book, "Claim", 17, 1, ids, "A", identity, grant) &&
            !(bool)Call(Book, book, "Claim", 17, 1, ids, "A", identity, grant) && drops.Count == 2,
            "registered grant settles once even if the provider then throws");
    }

    static void WrappedPicker(Assembly asm)
    {
        var plugin = asm.GetType("CaravanCommissions.Plugin");
        var open = plugin.GetNestedType("Open", BindingFlags.NonPublic);
        var inventory = plugin.GetNestedType("Inventory", BindingFlags.NonPublic);
        DaytimeManager.Instance = Blank<DaytimeManager>();
        typeof(DaytimeManager).GetField("m_CurrentDay", All).SetValue(DaytimeManager.Instance, 16);
        Call(plugin, null, "Reset", (object)null);
        var guest = Blank<GuestCaravan>();
        var screen = Blank<UIOverlayMerchant>();
        var cancel = (Action<UIOverlayMerchant>)Delegate.CreateDelegate(typeof(Action<UIOverlayMerchant>),
            guest, typeof(GuestCaravan).GetMethod("Cancel", All));
        typeof(UIOverlayMerchant).GetField("m_CallbackCancel", All)
            .SetValue(screen, new Action<UIOverlayMerchant>(picker => cancel(picker)));
        object[] opening = {guest, null};
        Check((bool)open.GetMethod("Prefix", All).Invoke(null, opening), "ordinary visit opens");
        var chosen = new List<string>();
        object[] args = {guest, screen, true, true, chosen};
        replacementPool = new List<string>();
        inventory.GetMethod("Prefix", All).Invoke(null, args);
        Check(!(bool)args[3] && chosen.Count == 0, "wrapped callback keeps empty redraw bounded");
        replacementPool = new List<string> { "Shield", "Shield", "Amulet" };
        typeof(Guest).GetField("m_Random", All).SetValue(guest, new System.Random(7));
        args[3] = true;
        inventory.GetMethod("Prefix", All).Invoke(null, args);
        Check(!(bool)args[3] && chosen.Count == 2 && chosen.Distinct().Count() == 2,
            "wrapped callback with a short weighted pool draws two distinct cards");
        open.GetMethod("Finalizer", All).Invoke(null, new[] {opening[1]});
        Call(plugin.GetNestedType("Reuse", BindingFlags.NonPublic), null, "Prefix", screen);
        args[3] = true;
        inventory.GetMethod("Prefix", All).Invoke(null, args);
        Check((bool)args[3], "merchant reuse outside a caravan releases ownership");
    }
    static bool ReplacePool(ref List<string> __result)
    {
        __result = replacementPool;
        return false;
    }
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    static object Call(Type type, object target, string name, params object[] args)
    {
        return type.GetMethod(name, All).Invoke(target, args);
    }
    static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
    static T Blank<T>() { return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
    static object Reload(object book)
    {
        var hooks = Book.Assembly.GetType("CaravanCommissions.SaveHooks", true);
        var reading = hooks.GetField("reading", All);
        var context = Activator.CreateInstance(reading.FieldType, true);
        const string path = "human_game";
        reading.FieldType.GetField("Path", All).SetValue(context, path);
        var previous = reading.GetValue(null);
        try
        {
            reading.SetValue(null, context);
            var checkpoint = new Dictionary<string, object> {
                { "runID", "fixture-run" }, { "hash", "fixture-hash" },
                { "kingdomChartersV1", new { choice = 2 } }, { "caravanCommissionsV1", book }
            };
            Call(hooks.GetNestedType("ReadJson", BindingFlags.NonPublic), null, "Postfix", path, Json.Serialize(checkpoint));
            var envelope = reading.FieldType.GetField("Selected", All).GetValue(context);
            return envelope.GetType().GetField("caravanCommissionsV1", All).GetValue(envelope);
        }
        finally { reading.SetValue(null, previous); }
    }

    public static void Run(string plugin, string cheatDll, string dailyDll)
    {
        var asm = Assembly.LoadFrom(plugin);
        Book = asm.GetType("CaravanCommissions.OrderBook", true);
        Offers = asm.GetType("CaravanCommissions.OfferIds", true);
        Payment = asm.GetType("CaravanCommissions.CommissionPayment", true);
        Pool = asm.GetType("CaravanCommissions.Pool", true);
        quotedPrice = (int)asm.GetType("CaravanCommissions.CommissionPanel", true).GetField("Price", All).GetRawConstantValue();
        Check(quotedPrice == 3, "displayed price");
        var starting = new Dictionary<ResourceType, int> { { ResourceType.Stone, 4 }, { ResourceType.Wood, 9 } };
        Check((bool)Call(Payment, null, "TakeStarting", starting, ResourceType.Stone) &&
            starting[ResourceType.Stone] == 1 && starting[ResourceType.Wood] == 9, "fallback material");
        Check(!(bool)Call(Payment, null, "TakeStarting", starting, ResourceType.Stone) && starting[ResourceType.Stone] == 1,
            "starting material shortfall");
        int baseYield = 2;
        bool withCheat = !String.IsNullOrEmpty(cheatDll);
        if (withCheat)
        {
            var yield = Assembly.LoadFrom(cheatDll).GetType("CheatMenu.Cheats.ProductionYield", true);
            var cfg = new ConfigFile(Path.Combine(Path.GetDirectoryName(plugin), "offline-probe.cfg"), false);
            cfg.SaveOnConfigSet = false;
            Call(yield, null, "Bind", cfg);
            ((ConfigEntry<float>)yield.GetField("Multiplier", All).GetValue(null)).Value = 3f;
            baseYield = (int)Call(yield, null, "Scale", 2);
            Check(baseYield == 6, "CheatMenu 3x yield");
        }
        var daily = String.IsNullOrEmpty(dailyDll) ? null : Assembly.LoadFrom(dailyDll);
        var fixture = new Harmony("offline.commissions.replacedpool");
        try
        {
            fixture.Patch(typeof(GuestCaravan).GetMethod("GetItems", All),
                prefix: new HarmonyMethod(typeof(CommissionProbe).GetMethod("ReplacePool", All)));
            foreach (var removed in new[] { 0, 1, 3 })
                Scenario(baseYield, daily, removed);
            GrantFaults(asm);
            WrappedPicker(asm);
        }
        finally { fixture.UnpatchSelf(); }
        Console.WriteLine("PASS baseline production 2 -> " + baseYield + "; Daily Caravan " +
            (daily == null ? "off" : "on (real eligibility/dispatch methods)") +
            "; reorder, one removed, all removed; exact debit and rollback after throw; " +
            "suppressed and throw-after-spawn receipts; wrapped empty and short redraws; merchant reuse.");
    }

    static void Arrive(object book, int day, Assembly daily)
    {
        if (daily != null)
        {
            var visitType = daily.GetType("DailyCaravan.Visit", true);
            Check((bool)Call(visitType, null, "AddsVisit", day, 16, true, true, false), "daily eligible");
            var arrivalType = daily.GetType("DailyCaravan.Arrival", true);
            var arrival = Activator.CreateInstance(arrivalType, true);
            var visit = Activator.CreateInstance(visitType, true);
            Call(arrivalType, arrival, "Request", visit, null);
            Check(Call(arrivalType, arrival, "Take", visit, true).ToString() == "Wait", "occupied slot");
            Check(Call(arrivalType, arrival, "Take", visit, false).ToString() == "StartVisit", "first dispatch");
            Check(Call(arrivalType, arrival, "Take", visit, false).ToString() == "Wait", "repeat dispatch");
        }
        Call(Book, book, "BeginDay", day);
    }

    static void Scenario(int yield, Assembly daily, int removed)
    {
        var ledger = Blank<ResourceStorage>();
        typeof(ResourceStorage).GetField("m_Resources", All).SetValue(ledger, new List<ResourcePacket>());
        typeof(ResourceStorage).GetField("m_ResourcesAmount", All).SetValue(ledger, new int[256]);
        // The installed BuildingResource.GiveResource emits one packet per produced unit.
        // Start with three units, reserve one, then model one production cycle.
        for (int i = 0; i < 3 + yield; i++)
            ledger.AddResourcePacket(new ResourcePacket(i, ResourceType.Wood, new Vector2(), 1));
        ledger.GetAllResourcePackets()[0].Reserve();
        int initial = ledger.GetResourceQuantity(ResourceType.Wood);
        var checkout = (ResourcePacket[])Call(Payment, null, "Units", ledger.GetAllResourcePackets(), ResourceType.Wood);
        int attempts = 0;
        Check(!(bool)Call(Payment, null, "Take", checkout, ledger,
            new Func<int, ResourcePacket>(id => ++attempts == 2 ? null : ledger.TakeResourcePacket(id)),
            new Action<ResourcePacket>(p => ledger.AddResourcePacket(p))) &&
            ledger.GetResourceQuantity(ResourceType.Wood) == initial, "rollback on rejected packet");
        attempts = 0;
        Check(!(bool)Call(Payment, null, "Take", checkout, ledger,
            new Func<int, ResourcePacket>(id => {
                var packet = ledger.TakeResourcePacket(id);
                if (++attempts == 2) throw new InvalidOperationException("after take");
                return packet;
            }), new Action<ResourcePacket>(packet => {
                ledger.AddResourcePacket(packet);
                throw new InvalidOperationException("after restore");
            })) && ledger.GetResourceQuantity(ResourceType.Wood) == initial &&
            checkout.All(p => ReferenceEquals(ledger.GetResourcePacket(p.GetIdentifier()), p)),
            "throw after second take restores the original packets exactly once");
        int debits = 0;
        Func<bool> pay = delegate {
            debits++;
            var units = (ResourcePacket[])Call(Payment, null, "Units", ledger.GetAllResourcePackets(), ResourceType.Wood);
            Check(units.Length == initial - 1, "displayed spendable units");
            return (bool)Call(Payment, null, "Take", units, ledger,
                new Func<int, ResourcePacket>(id => ledger.TakeResourcePacket(id)),
                new Action<ResourcePacket>(p => ledger.AddResourcePacket(p)));
        };

        var catalog = new Dictionary<string, Item>();
        foreach (var id in new[] { "fixture.Coal", "fixture.Opal", "fixture.Ruby", "Radiance", "Amulet", "Shield" })
        {
            var item = Blank<Item>();
            typeof(Item).GetField("identifier", All).SetValue(item, id);
            catalog.Add(id, item);
        }
        var manager = Blank<AddressablesManager>();
        typeof(AddressablesManager).GetField("m_Items", All).SetValue(manager, catalog);
        catalog["fixture.OpalPrefab"] = catalog["fixture.Opal"];
        AddressablesManager.Instance = manager;
        Func<string, string> identify = (Func<string, string>)Delegate.CreateDelegate(typeof(Func<string, string>),
            Pool.GetMethod("Identify", All));
        replacementPool = new List<string> { "fixture.Coal", "fixture.OpalPrefab", "fixture.Opal", "fixture.Ruby" };
        var guest = Blank<GuestCaravan>();
        var nativeStock = (List<string>)Call(Pool, null, "Stock", guest);
        var preview = (string[])Call(Offers, null, "FromPool", nativeStock, identify);
        Check(preview.SequenceEqual(new[] { "fixture.Coal", "fixture.Opal", "fixture.Ruby" }), "canonical preview");
        var book = Activator.CreateInstance(Book, true);
        Arrive(book, 16, daily);
        Check((bool)Call(Book, book, "Place", 16, 1, "Fixture", preview, pay), "pay");
        Check(!(bool)Call(Book, book, "Place", 16, 1, "Fixture", preview, pay), "repeat pay");
        replacementPool = new List<string>(replacementPool);
        replacementPool.Reverse();
        var reordered = (List<string>)Call(Pool, null, "Stock", guest);
        Check(reordered[0] == "fixture.Ruby", "patched getter observed reordered replacement list");
        if (removed > 0) catalog.Remove("fixture.Coal");
        if (removed == 3) { catalog.Remove("fixture.Opal"); catalog.Remove("fixture.Ruby"); }
        book = Reload(book);
        int day = daily == null ? 23 : 17;
        Arrive(book, day, daily);
        string[] promised = (string[])Book.GetField("items").GetValue(book);
        Check(promised.SequenceEqual(preview), "reload after reorder");
        var granted = new List<string>();
        Func<string,bool> grant = id => { granted.Add(id); return true; };
        if (removed > 0)
            Check(!(bool)Call(Book, book, "Claim", day, 1, promised, "fixture.Coal", identify, grant), "missing ID");
        if (removed == 3)
        {
            var replacement = new[] { "Radiance", "Amulet", "Shield" };
            Check((bool)Call(Book, book, "Exchange", day, 1, promised, "Replacement", replacement, identify), "free exchange");
            Check(!(bool)Call(Book, book, "Claim", day, 1, promised, "Shield", identify, grant), "stale callback");
            book = Reload(book);
            Arrive(book, ++day, daily);
            promised = (string[])Book.GetField("items").GetValue(book);
        }
        string selected = removed == 3 ? "Shield" : "fixture.Opal";
        Check((bool)Call(Book, book, "Claim", day, 1, promised, selected, identify, grant), "claim");
        Check(!(bool)Call(Book, book, "Claim", day, 1, promised, selected, identify, grant), "repeat claim");
        Check(!(bool)Call(Book, Reload(book), "Available", day, 1), "settlement survives reload");
        Check(granted.SequenceEqual(new[] { selected }) && debits == 1 &&
            ledger.GetResourceQuantity(ResourceType.Wood) == initial - quotedPrice &&
            ledger.GetAllResourcePackets().First().IsReserved(), "stock and grants");
    }
}
