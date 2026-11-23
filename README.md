# Caravan Commissions

Choose one of the caravan’s three item cards to take it now. For a future offer, select **Compare commissions**. The desk shows today’s items alongside the selected kingdom’s three items, including their effects. Use **Previous pool** and **Next pool** to compare kingdoms. **Today’s items** returns to the native picker.

**Review order** shows the price again before payment. **Back to pool** keeps the same pool selected. Confirming spends three units of the displayed material and uses today’s pick. The test price uses the kingdom’s wood material, with stone and boards as fallbacks where wood has no source. Available stock excludes reserved packets. The home kingdom is excluded from the pool list.

## Delivery and saves

The next distinct item caravan in the same run delivers the promised three choices. Take one item using the normal caravan cards, or choose **Defer delivery** to keep the same pool for a later visit. Reopening the ordering caravan cannot deliver the order. One order can be outstanding per run.

Both planned and reputation caravans can deliver. With Daily Caravan enabled, its next actual visit can deliver. A missed day or a schedule change does not erase an order. The commission itself never schedules visitors.

The order and spent visits travel with the game’s regular run checkpoint. Continue or restore a backup to get the order, materials and items from that checkpoint. A purchase after the last saved checkpoint rolls back with the game. A fresh run starts empty.

## Local import

1. In Thunderstore Mod Manager or r2modman, select Super Fantasy Kingdom and an isolated test profile. Install the BepInEx pack and HideManagerGameObject configuration listed in the manifest if needed.
2. In profile settings, choose **Import local mod** and select **CaravanCommissions-0.1.0.zip**. Check version 0.1.0 in the mod list.
3. When the shared game test slot is available, start the profile modded. Check the BepInEx log for `Caravan Commissions 0.1.0 loaded` and visit a caravan in a test run.

## Controller playtest

- Using only a physical pad, navigate from the three item cards to **Compare commissions**. Cycle through pools, read both sets of effects and the price, enter review and back out. Confirm focus returns to **Review order** for the same pool. Use a low-stock save to check the shortfall and return to today’s cards.
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

The local import ZIP is written to `dist/CaravanCommissions-0.1.0.zip`. The order checks exercise the transaction and checkpoint model offline. Import, native save/load, game visuals and physical controller operation require a live playtest.
