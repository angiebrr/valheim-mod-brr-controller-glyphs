# BrrControllerGlyphs

Keeps Valheim's controller glyphs on when your Steam Input layout also sends keyboard keys, and
shows those keys as the controller buttons that send them.

## The problem it fixes

Many Steam Controller and Steam Deck layouts send real gamepad buttons for the core actions and
keyboard keys for the extras: hotbar slots on an action-set hold, back grips, trackpad clicks,
other mods' hotkeys. Every one of those key presses flips Valheim into keyboard mode, so:

- prompts switch from controller glyphs to keyboard keys after any extra-button press;
- hotbar labels, quick slots and mod hover hints show `1`, `Z` or `LeftShift` instead of the
  button you actually press.

With a gamepad connected, this mod keeps the controller glyphs on, whatever the last input was.
Keys your layout sends show as your buttons, for example `R4 + D-pad Up` for hotbar slot 1.
Valheim's input itself isn't changed: the trackpad cursor, run toggle and menu navigation behave
as before.

## Features

- **Valheim's hints** (crosshair, hint bars, inventory and crafting badges) stay controller
  glyphs after keyboard or mouse input.
- **Keyboard-only keys** show as the buttons from your mapping file, with Steam Deck-style art for
  buttons Valheim has none for (back buttons L4/L5/R4/R5, trackpads).
- **Hotbar and quick-slot labels** show your buttons, grouped by the held button: one `R4` beside
  a run of slots, then each slot's own button. Works on AzuExtendedPlayerInventory's quick-slot bar
  and in the inventory too.
- **Other mods' hover hints** such as `[B] AutoFuel` show your button instead of the key.
- **Help setting up:** the settings list the keys that hints and other mods' keybindings use but
  your mapping lacks, and the mapping reloads as soon as you save it.

Turn off **Enabled** (or unplug the gamepad) and everything goes back to vanilla.

## Requirements

- BepInExPack Valheim
- Jotunn
- YamlDotNet (ValheimModding)

Client-side only: servers and other players don't need it.

## Setup

1. Install the mod (a mod manager such as Gale or r2modman handles the dependencies).
2. Start the game once. The mod writes a mapping template to
   `BepInEx/config/BrrControllerGlyphs.mapping.yaml`. It maps nothing yet, so controller glyphs
   already work and keyboard keys still show as keys.
3. Open the mapping file and list the keyboard keys your Steam Input layout sends, with the
   button(s) that send each one:

   ```yaml
   keys:
     "1": [R4, DPadUp]      # hotbar slot 1: hold R4 + D-pad up
     "2": [R4, DPadRight]
     Keypad1: [R5, DPadUp]  # AzuEPI quick slot 1: hold R5 + D-pad up
     Q: L5                  # a single back button
     LeftShift: LPadDown    # left trackpad click down
   ```

   The template lists every button name with art, the optional `labels`, `colors` and `aliases`
   sections, and the key spellings that work.
4. Save. The mapping reloads while you play; **Mapping status** in the settings says whether it
   loaded or which line is wrong.
5. Play a while, then check **Keys missing from mapping** for keys other mods use that your
   mapping lacks. Add only keys your layout actually sends.

## Settings

Change them in game with ConfigurationManager (F1, or the button in the pause menu); changes
apply live. The `.cfg` file is `BepInEx/config/angiebrr.BrrControllerGlyphs.cfg`.

| Section | Setting | What it does |
|---|---|---|
| General | Enabled | Turns the whole mod on or off. |
| General | Glyphs used by mod | Art for the glyphs this mod draws: `Valheim` (Valheim's, Steam Deck art for extra buttons) or `SteamDeck` (Steam Deck art for all). |
| Hint Glyphs | Force controller glyphs in Valheim hints | Keeps Valheim's hints as controller glyphs after keyboard or mouse input. |
| Hint Glyphs | Force controller glyphs in mod hints | Shows keys in other mods' hover hints as your buttons. |
| Hotbar Glyphs | Force hotbar controller glyphs | Labels the hotbar and quick slots with your buttons, grouped by held button. |
| Hotbar Glyphs | Hotbar group colors | `Neutral`, `Palette`, or `Custom` (the mapping file's `colors`). |
| Hotbar Glyphs | Hotbar group background | Backing behind each group's held button; alpha 0 hides it. |
| Key Glyph Mapping | Mapping file, Mapping status, Keys missing from mapping | Read-only: where the file is, whether it loaded, and what it's missing. |

## Console commands (F5)

- `brrcg_reloadmapping`: reads the mapping file again.
- `brrcg_selftest`: checks the patches, gamepad detection and prompts, and writes the results to
  `LogOutput.log` (lines start with `[brrcg_selftest]`).

## Compatibility

- **AzuExtendedPlayerInventory:** quick-slot bar and inventory quick slots are labelled.
- **MyLittleUI:** hover text and chest hints work; the mod runs after it.
- **ConfigurationManager (shudnal):** settings ordered, with a reload button and the missing-keys
  list.
- Mods that write key names in hover text (AzuAutoStore, AzuCraftyBoxes, AutomaticFuel,
  ImpactfulSkills, ...) are translated through your mapping.

## Known limitations

- Hints drawn by Jotunn's KeyHintManager aren't changed yet.
- Stick directions and the PlayStation touchpad have no mapping names yet.
- If a Valheim update changes something the mod patches, only that feature turns off, with a
  warning in the log.

## Changelog

- **0.0.1:** first version.

## Credits

Controller glyphs made from [Input Prompts](https://kenney.nl/assets/input-prompts) by Kenney
(CC0), recolored for this mod by `scripts/make-glyphs.ps1`. Built with
[Jötunn](https://github.com/Valheim-Modding/Jotunn).
