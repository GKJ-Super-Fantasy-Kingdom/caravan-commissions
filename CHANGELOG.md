# 0.1.0

First local test build. Compare the visiting caravan’s items with the three items in another kingdom’s pool, review the price, and commission the next caravan. Includes native item selection, controller navigation and run-checkpoint persistence.

The local compatibility build preserves canonical item IDs across pool changes, offers free exchanges for missing content, and keeps merchant purchase pricing separate from prepaid deliveries. Extended pools appear in sets of three. Run checkpoints retain orders through tavern saves.

The comparison desk follows the game UI scale and uses a parchment background for item keywords. Fixed a Unity player reload issue that could discard the nested order during the tavern checkpoint rewrite.

The desk shows its panel-wide Back binding. The keyboard/mouse scheme displays the bound keyboard key. Item cards, pool controls, and payment continue to use focused Submit or a mouse click.

The offer desk and confirmation use the caravan’s card frames and banners. Both show the quoted cost and available material together. Commission headings and actions use English title case.

Prepaid deliveries now verify the new draggable item before settling. Failed material withdrawals restore missing packets, ordinary draws retain item weights, and wrapped caravan callbacks keep the native short-pool guard. Same-day checkpoints distinguish dawn replays from new invitations.
