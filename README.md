# Caravan Commissions

Choose one of the caravan’s three item cards to take it now. For a future offer, select **Compare Commissions**. The desk shows today’s items alongside the selected kingdom’s three items, including their effects. Use **Previous Pool** and **Next Pool** to compare kingdoms. **Today’s Items** returns to the native picker.

**Review Order** shows the price again before payment. **Back to Pool** keeps the same pool selected. Confirming spends three units of the displayed material and uses today’s pick. The test price uses the kingdom’s wood material, with stone and boards as fallbacks where wood has no source. Available stock excludes reserved packets. The home kingdom is excluded from the pool list.

## Delivery and saves

The next distinct item caravan in the same run delivers the promised three choices. Take one item using the normal caravan cards, or choose **Defer Delivery** to keep the same pool for a later visit. Reopening the ordering caravan cannot deliver the order. One order can be outstanding per run.

Both planned and reputation caravans can deliver. With Daily Caravan enabled, its next actual visit can deliver. A missed day or a schedule change does not erase an order. The commission itself never schedules visitors.

A same-day checkpoint remembers the dawn visit separately from later invitations. Replaying the native or Daily Caravan dawn keeps that visit’s original decision. A new reputation invitation can deliver even if the loader skips the earlier visit. For a checkpoint created by an older build, the mod treats spent visit 1 as the dawn when the original visit type is unavailable.

The order and spent visits travel with the game’s regular run checkpoint. Continue or restore a backup to get the order, materials and items from that checkpoint. A purchase after the last saved checkpoint rolls back with the game. A fresh run starts empty.

## Mod compatibility

Commission pools use the visiting caravan’s runtime kingdom stock. Larger pools appear as numbered sets of three, so appended items can be ordered. A modded item needs a stable item identifier that the game can resolve again when it creates a card or drops the item. The paid order saves the three displayed IDs. Reordering or replacing a pool does not change those IDs.

If an item disappears, choose a surviving card, defer, or select **Exchange Order** to preview another complete pool. Confirming the free exchange uses this visit and delivers the new choices at a later caravan. If there are no complete pools, the desk also lets you explicitly close the order without a refund. A restored content mod can make a deferred item available again.

The game’s ordinary merchant cards keep their prices. The comparison desk displays their supplied material costs. A commissioned delivery uses prepaid native cards and does not run the shop purchase path. The commission fee remains three unreserved single units from the kingdom’s current mapped store or starting stock. Mods that replace the entire picker or grant outside the native item registry need a separate integration.

Ordinary draws retain repeated pool entries as weights. A delivery settles when a new native draggable item with the promised ID appears. If another mod suppresses that spawn, the order remains available at a later caravan. If a hook throws after the drop appears, the order settles once. Providers that equip items directly, deliver asynchronously, or replay custom invitations need a separate integration to identify that reward or visit.

## Local import

1. In Thunderstore Mod Manager or r2modman, select Super Fantasy Kingdom and an isolated test profile. Install the BepInEx pack and HideManagerGameObject configuration listed in the manifest if needed.
2. In profile settings, choose **Import local mod** and select **CaravanCommissions-0.1.0.zip**. Check version 0.1.0 in the mod list.
3. When the shared game test slot is available, start the profile modded. Check the BepInEx log for `Caravan Commissions 0.1.0 loaded` and visit a caravan in a test run.

## Controller playtest

Use the desk’s Back prompt from any focused control. Submit activates the highlighted button. You can also click its label.

- Using only a physical pad, navigate from the three item cards to **Compare Commissions**. Cycle through pools, read both sets of effects and the price, enter review and back out. Confirm focus returns to **Review Order** for the same pool. Use a low-stock save to check the shortfall and return to today’s cards.
- Confirm one affordable order. Hold submit across the transition and verify the material count falls by exactly three. Check gamepad prompts after switching to mouse and back. Repeat navigation with keyboard arrows, submit and cancel. Check remapped buttons and both UI scales.
- At the next planned or reputation caravan, compare the delivered cards with the original preview. Defer, collect at a later visit, then try reopening. Verify exactly one draggable item appears. Repeat with Daily Caravan enabled and a skipped day.
- Load checkpoints from before payment, after payment and after collection. Compare item IDs, material balances and available picks. Test minimize/reopen, restoring a backup and starting a new run.

## Build

Build on Windows against a local game installation and a BepInEx 5 profile. The SDK targets .NET Standard 2.1. Paths can be overridden with MSBuild properties.

```powershell
dotnet build CaravanCommissions.csproj -c Release "-p:GameDir=D:\SteamLibrary\steamapps\common\Super Fantasy Kingdom" "-p:ProfileDir=C:\path\to\test-profile"
dotnet run --project tests/OrderChecks -c Release
python package.py
```

The local import ZIP is written to `dist/CaravanCommissions-0.1.0.zip`. The order checks exercise the transaction and checkpoint model offline. Local testing on game 0.8.067 covered a fresh process, the tavern checkpoint, and delivery on the next Daily Caravan visit. The comparison desk was exercised at 1× and 1.5× with keyboard, mouse, and a virtual gamepad. Physical controller operation and remapped bindings still need a playtest.

## Contributing and license

Bug reports and pull requests are welcome. See the [contribution guide](CONTRIBUTING.md) for local testing and reporting instructions.

The original mod code is available under the [MIT License](LICENSE). Super Fantasy Kingdom, its game assets, and third-party dependencies retain their respective licenses and ownership.

For an offline comparison using installed managed assemblies, run `tests/InstalledChecks/run.ps1` in Windows PowerShell 5.1. Supply `-GameDir` and `-CoreDir` (the profile’s BepInEx core folder). Optionally pass `-DailyDll` and/or `-CheatDll` to use their real dispatch and production-yield methods. Run each combination in a fresh process. The probe uses a fixture item pack and grant recorder. It reads its fixtures through the mod’s checkpoint deserializer. It does not launch the game.
