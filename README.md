# Caravan Commissions

Take an item from today’s caravan, or spend three materials to reserve a choice of three items from another kingdom. You can see exactly which items the next item caravan will bring before you pay.

## Features

- Compare today’s cards and their effects with a future offer, side by side.
- See the fee and your available stock together. The caravan asks for three wood, with stone or boards as fallbacks if your kingdom has no wood source.
- Keep one paid order per run. It travels with your run checkpoint through tavern saves and backup restores.

## How to Use

1. At an item caravan, select **Compare Commissions**. Use the arrows beside the future offer’s heading to browse other kingdoms, or **Today's Items** to return and take a card now.

   On the desk, move focus with the arrow keys, WASD, or the D-pad. Press **Enter** or the bottom face button (**A** on Xbox) to select. Press **Esc** or the right face button (**B** on Xbox) to go back. You can also click buttons with the mouse.

2. Select **Review Order** to check the three promised items and the price. **Back** returns to the same pool. **Pay and Commission** takes the displayed materials once and uses today’s pick.
3. At the next distinct item caravan, choose one of the three delivery cards to receive it as a draggable item. Choose **Defer Delivery** to keep all three choices for a later visit at no extra cost.

## Screenshots

Captured in Super Fantasy Kingdom 0.8.068 with Caravan Commissions 0.1.3 and a virtual gamepad.

![Offer desk with today's items on the left, arrows beside the Demon pool on the right, and Review Order focused](https://raw.githubusercontent.com/GKJ-Super-Fantasy-Kingdom/caravan-commissions/main/docs/screenshots/0.1.3/offers.png)

*At 1×, use the arrows beside Demon to change the future offer. Today's Items returns you to the caravan's first card.*

![Confirmation showing Burn, Hammer and Rage above a 3-wood fee, 7 wood in stock and the focused payment button](https://raw.githubusercontent.com/GKJ-Super-Fantasy-Kingdom/caravan-commissions/main/docs/screenshots/0.1.3/confirm.png)

*At 1.5×, check the three promised choices and your stock before paying. Back returns to the same pool.*

![Delivery picker showing Burn, Hammer and Rage with Hammer focused and Defer Delivery below](https://raw.githubusercontent.com/GKJ-Super-Fantasy-Kingdom/caravan-commissions/main/docs/screenshots/0.1.3/delivery.png)

*At 1.5×, choose one prepaid item to collect it, or defer to keep all three choices for a later visit.*

## Compatibility

- On the vanilla schedule, the next item caravan can be a scheduled visit or a reputation visit. With GKJ’s [Daily Caravan](https://thunderstore.io/c/super-fantasy-kingdom/p/GKJ/DailyCaravan/), your delivery is ready at the next actual item visit. Reopening the same visit keeps your original decision.
- With GKJ’s [Kingdom Charters](https://thunderstore.io/c/super-fantasy-kingdom/p/GKJ/KingdomCharters/), the Merchant Charter charges its extra coin for an immediate pick. Collecting a prepaid commission is free of that fee.
- With ILM’s [CheatMenu](https://thunderstore.io/c/super-fantasy-kingdom/p/ILM/CheatMenu/) 1.0.3, an offline check at 3× production yield confirmed that placing an order deducts the displayed three materials once.
- With GKJ’s [Visitor Calendar](https://thunderstore.io/c/super-fantasy-kingdom/p/GKJ/VisitorCalendar/) 0.1.1 or later, **Next Caravan** marks an undated order. An open or minimized eligible picker shows **Commission Delivery** on that day. Collecting clears the entry.
- Reloading a checkpoint restores both your order and your material stock to that point in the run.
- Modded items need stable IDs that the game can look up after a reload. If a promised item disappears, take a remaining card, defer delivery, or use **Exchange Order** to reserve another complete pool for free. Mods that replace the item picker or equip rewards directly need a separate integration.

## Installation

1. In Thunderstore Mod Manager or [r2modman](https://thunderstore.io/c/super-fantasy-kingdom/p/ebkr/r2modman/), select **Super Fantasy Kingdom** and your profile.
2. Install **CaravanCommissions** by **GKJ** along with its listed dependencies, [BepInExPack](https://thunderstore.io/c/super-fantasy-kingdom/p/BepInEx/BepInExPack/) and [HideManagerGameObject_Config](https://thunderstore.io/c/super-fantasy-kingdom/p/BepInEx/HideManagerGameObject_Config/). For a local ZIP, install the dependencies and use **Import local mod**.
3. Start the game modded. **Compare Commissions** appears when an item caravan opens.
