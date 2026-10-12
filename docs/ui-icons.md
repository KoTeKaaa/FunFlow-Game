# HUD icon sources

HUD icons are adapted from [Game-icons.net](https://game-icons.net/) under [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/):

- Loot: [Cash](https://game-icons.net/1x1/lorc/cash.html), by Lorc.
- Weight: [Knapsack](https://game-icons.net/1x1/lorc/knapsack.html), by Lorc.
- Exit: [Exit door](https://game-icons.net/1x1/delapouite/exit-door.html), by Delapouite.
- Alarm: [Siren](https://game-icons.net/1x1/skoll/siren.html), by Skoll.

Modifications: transparent background, dark outlines and gold, mint, and red gradient fills. Source SVGs, styled SVGs, rendered PNGs and the license are in `Assets/Game/Presentation/UI/HudIcons/`. AlarmIdle is a mint variation of Siren; Alarm is its alert variation.

The HUD prefab owns these references. New levels inherit the icons without scene wiring. `HudArtDirection.ApplyStatusIcons` reapplies the layout when rebuilding HUD.

The tutorial uses the same Loot, Alarm and Exit sprites as the HUD. Mouse.svg is an original project icon. `HudArtDirection.ApplyTutorialIcons` builds its responsive rows; HudScreens shows them only before a run.
