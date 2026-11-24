# Visitor Calendar Integration

Caravan Commissions 0.1.1 exposes one public, read-only query for the loaded run. Call it on the Unity game thread when refreshing the calendar. The call reads the current order and overlay ownership without opening a picker, advancing a visit, spending materials, or granting an item.

```csharp
// Assembly: CaravanCommissions.dll
// BepInEx plugin GUID: gkj.sfk.caravancommissions
CaravanCommissions.CommissionCalendar.TryGetPendingDelivery(out int visitDay)
```

| Return value | `visitDay` | Calendar treatment |
| --- | --- | --- |
| `false` | `-1` | Omit the commission. There is no outstanding order in the active run, or gameplay is unavailable. |
| `true` | `-1` | Show **Next Caravan**. The player has paid and the delivery visit has no confirmed day. |
| `true` | Current in-game day | Attach the order to today's item caravan. An open or minimized picker belongs to an eligible, unspent visit. |

The payment visit is already spent. Reopening it cannot qualify as delivery. An ordinary scheduled caravan, a reputation invitation (even on the same day), or a Daily Caravan visit can deliver. A scheduled day or a daily dispatch request alone does not identify which visit will actually serve the order. A closed gate, a busy guest slot, or an earlier invitation can change the outcome. Keep **Next Caravan** until the query supplies a day.

Deferring or exchanging an order uses that visit and keeps the order pending. Successful collection or closing an unavailable order clears it. A failed grant leaves the order pending. Checkpoint load replaces the order and clears previous picker bindings. Read the query again after loading, including when restoring a backup from before payment or after collection. Cache the method binding if desired, and refresh the result whenever the calendar opens or redraws.

## Optional Binding

The calendar can bind once after plugin startup using BepInEx and the runtime type system. This works without an assembly reference to Caravan Commissions. If either the plugin or the method is absent (including version 0.1.0), omit the commission entry.

```csharp
delegate bool ReadDelivery(out int visitDay);

static ReadDelivery BindDelivery()
{
    if (!Chainloader.PluginInfos.TryGetValue("gkj.sfk.caravancommissions", out var info) ||
        info.Instance == null) return null;
    var api = info.Instance.GetType().Assembly.GetType("CaravanCommissions.CommissionCalendar");
    var method = api?.GetMethod("TryGetPendingDelivery", BindingFlags.Public | BindingFlags.Static,
        null, new[] { typeof(int).MakeByRefType() }, null);
    return method?.ReturnType == typeof(bool)
        ? (ReadDelivery)Delegate.CreateDelegate(typeof(ReadDelivery), method) : null;
}
```
