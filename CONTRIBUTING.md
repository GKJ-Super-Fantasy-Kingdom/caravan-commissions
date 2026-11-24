# Contributing to Caravan Commissions

## Report a bug

Open a [GitHub issue](https://github.com/GKJ-Super-Fantasy-Kingdom/caravan-commissions/issues) with the mod, game, and BepInEx versions, your kingdom and day, and the steps to reproduce the problem. Include the relevant log excerpt. For interface or input issues, include the UI scale and whether you used a controller or keyboard and mouse.

## Submit a change

Fork the repository, create a branch, and follow the build and packaging instructions in the build section below. Use the assemblies from your own game installation and BepInEx profile as build references. Keep game binaries, save files, and local build output out of commits.

Describe the player-visible change in your pull request. List the checks you ran and, where relevant, which controller actions and UI scales you tested.

## Commit messages

Use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) for each commit. Start the subject with a type, an optional scope, and a short description. Use an exclamation mark before the colon for a breaking change.

Examples include `feat(ui): show controller prompts`, `fix(save): restore the current day`, and `chore: update build tooling`.

Accepted types are `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`. Use the same format for pull request titles so GitHub can use the title as the merge or squash commit subject.

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

For an offline comparison using installed managed assemblies, run `tests/InstalledChecks/run.ps1` in Windows PowerShell 5.1. Supply `-GameDir` and `-CoreDir` (the profile’s BepInEx core folder). Optionally pass `-DailyDll` and/or `-CheatDll` to use their real dispatch and production-yield methods. Run each combination in a fresh process. The probe uses a fixture item pack and grant recorder. It reads its fixtures through the mod’s checkpoint deserializer. It does not launch the game.
