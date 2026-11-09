# Screenshots for the mod page

The mod page (`BrrControllerGlyphs/README.md`) shows these images in tables, linked from GitHub (`blob/main/docs/images/<name>.png?raw=true`), so they appear on Thunderstore once they're on `main`. Take each pair from the same spot, with a gamepad connected, cropped to the part that changes. PNG, named exactly as below. Restart the game after switching the mod on or off (toggling it live can leave labels in the wrong state).

| File | Shot |
|---|---|
| `prompt_vanilla.png` | A crosshair prompt (e.g. a chest) with the mod's **Enabled** off, just after pressing a key your layout sends: keyboard keys. |
| `prompt_mod.png` | The same prompt with the mod on: controller glyphs. |
| `badges_vanilla.png` | Inventory with the mod off, after moving the trackpad cursor: button badges gone. |
| `badges_mod.png` | The same with the mod on: badges still showing. |
| `buttons_vanilla.png` | A prompt or hint for a key your layout sends (e.g. AzuCraftyBoxes' fill all), mod off: the key. |
| `buttons_valheim.png` | The same, **Glyphs used by mod** `Valheim`: Valheim's art plus Steam Deck art for back buttons and trackpads. |
| `buttons_steamdeck.png` | The same, **Glyphs used by mod** `SteamDeck`. |
| `hotbar_vanilla.png` | Hotbar and AzuEPI quick-slot bar, mod off: `1`–`8` and AzuEPI's key names. |
| `hotbar_neutral.png` | The same, mod on, **Hotbar group colors** `Neutral`: R4/R5 groups. |
| `hotbar_palette.png` | The same, **Hotbar group colors** `Palette`. |
| `hints_vanilla.png` | Another mod's hover hint with a key in it (e.g. `[B]` on a fire), mod off. |
| `hints_mod.png` | The same with the mod on: your button. |

## Hotbar groups (HUD and inventory, with and without quick slots)

Twelve shots for the grouping table: three mappings × HUD or inventory × with or without AzuExtendedPlayerInventory's quick slots. Use **Hotbar group colors** `Palette` so the groups stand out.

1. Back up your own mapping: copy `BepInEx/config/BrrControllerGlyphs.mapping.yaml` somewhere safe.
2. For each example in [`docs/example-mappings`](../example-mappings) (`no-groups`, `one-group`, `multiple-groups`), copy it over `BrrControllerGlyphs.mapping.yaml`. The mapping reloads when saved; check **Mapping status**, and restart if the labels don't update.
3. With quick slots: AzuEPI's **3 - Quick Slots > Number of Quick Slots** at 6. Without: set it to 0 (restart the game if the bar or cells don't go away).
4. Take the HUD shot (hotbar, plus AzuEPI's bar under it when on) and the inventory shot (top row, plus the quick-slot cells when on).
5. Put your own mapping and quick-slot count back afterwards.

| File | Mapping | Screen | Quick slots |
|---|---|---|---|
| `groups_none_hud.png` | no-groups | HUD | 0 |
| `groups_none_hud_quickslots.png` | no-groups | HUD | 6 |
| `groups_none_inventory.png` | no-groups | Inventory | 0 |
| `groups_none_inventory_quickslots.png` | no-groups | Inventory | 6 |
| `groups_one_hud.png` | one-group | HUD | 0 |
| `groups_one_hud_quickslots.png` | one-group | HUD | 6 |
| `groups_one_inventory.png` | one-group | Inventory | 0 |
| `groups_one_inventory_quickslots.png` | one-group | Inventory | 6 |
| `groups_multiple_hud.png` | multiple-groups | HUD | 0 |
| `groups_multiple_hud_quickslots.png` | multiple-groups | HUD | 6 |
| `groups_multiple_inventory.png` | multiple-groups | Inventory | 0 |
| `groups_multiple_inventory_quickslots.png` | multiple-groups | Inventory | 6 |

## Mapping check

| File | Shot |
|---|---|
| `audit_missing_keys.png` | ConfigurationManager (F1) open on **Key Glyph Mapping**, with **Keys missing from mapping** showing a few keys and their uses. Play a while first, hovering things with key hints, so hint uses show up next to keybinding settings. |
| `audit_status_error.png` | The same section after saving the mapping with a typo (e.g. a missing `:` or `]`): **Mapping status** shows the error and line, with the **Reload mapping** button. Fix the file afterwards. |
