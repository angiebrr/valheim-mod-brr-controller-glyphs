# Changelog

## 0.0.1

### Added

- Controller glyphs stay on in Valheim's hints (crosshair, hint bars, inventory and crafting badges) while a gamepad is connected, even after keyboard or mouse input.
- Keyboard keys your controller sends show as the buttons from your mapping file, with Steam Deck-style glyphs for buttons Valheim has no art for (back buttons, trackpads).
- Hotbar and AzuExtendedPlayerInventory quick-slot labels show your buttons, grouped by the held button (e.g. one R4 beside a run of slots), in the HUD and the inventory, with neutral, palette or custom group colors.
- Other mods' hover hints such as `[B] AutoFuel` show your button instead of the key.
- Keys missing from mapping: lists keys other mods' keybindings and hover hints use that your mapping lacks.
- The mapping file reloads when you save it; Mapping status shows whether it loaded or which line is wrong.
- Console commands `brrcg_reloadmapping` and `brrcg_selftest`.
