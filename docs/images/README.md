# Screenshots for the mod page

The mod page (`BrrControllerGlyphs/README.md`) shows these images in tables, linked from GitHub (`blob/main/docs/images/<name>.jpg?raw=true`), so they appear on Thunderstore once they're on `main`. Save each as JPEG in this folder, named exactly as below, cropped to the part that changes. Take each pair from the same spot with the same crop, with the gamepad connected.

The list is in shooting order: four rounds, three restarts. Tick each box as you go.

## Before you start

- [x] Back up your mapping: copy `BepInEx/config/brr.angiebrr.BrrControllerGlyphs.mapping.yaml` somewhere safe. Rounds 1 and 2 copy the example mappings over it.
- [x] Settings in Mod Config (F1): **Enabled** on, **Glyphs used by mod** `Valheim`.
- [x] AzuEPI: **3 - Quick Slots > Number of Quick Slots** `6`, **5 - UI Features > Inventory Layout** `Legacy` (other layouts are a known issue).
- [x] Stand near a chest, with an item or two on hotbar slots 1–4 and in some quick slots.

Other UI mods (e.g. MyLittleUI) can stay on: just keep the same mods for both shots of a pair, since the tables compare with and without this mod only.

Switching settings live can leave labels stale (known issue). If a shot looks wrong after a change, reopen the inventory, and if that doesn't fix it, restart.

## Round 1: mod on, your mapping, quick slots 6 (no restart)

Hotbar colors and art (HUD):

- [x] **Hotbar group colors** `Neutral` → `hotbar_neutral.jpg`: the hotbar and AzuEPI's bar under it, with the R4 and R5 groups.
- [x] **Hotbar group colors** `Palette` → `hotbar_palette.jpg`: the same view. Leave `Palette` on from here.
- [x] `buttons_valheim.jpg`: close-up of hotbar slots 1–4 and the R4 prefix (R4 in Steam Deck art, D-pad in Valheim's art).
- [x] **Glyphs used by mod** `SteamDeck` → `buttons_steamdeck.jpg`: the same close-up (all Steam Deck art). Set it back to `Valheim`.

Prompts and badges:

- [x] `prompt_mod.jpg`: look at the chest, press a key your controller sends (e.g. pickup on L5), and shoot the chest prompt: still glyphs. Crop to Valheim's own lines (`Open`), not AzuCraftyBoxes' line.
- [x] `badges_mod.jpg`: open the inventory, move the trackpad cursor, and shoot the button badges (Y, LSt, RT...) still showing.
- [x] `hints_mod.jpg`: a mod hint written the same way in both input modes, e.g. AutomaticFuel's `[B]` on a fire or AzuCraftyBoxes' fill-all `[LeftShift + Use]` on a fire or smelter. Press a gamepad button first. (Not the Crafting Chest hint: AzuCraftyBoxes writes it differently per input mode.)

Mapping check (Mod Config, **Key Glyph Mapping**):

- [x] `audit_missing_keys.jpg`: **Keys missing from mapping** with a few keys and their uses. Hover a few things with mod hints first so hint uses show up.
- [x] `audit_status_error.jpg`: break the mapping file (delete a `:` or a `]`), save, and shoot **Mapping status** with the error, line and **Reload mapping** button. Then fix the file and save.

Groups with quick slots (`groups_one_*_quickslots` are done):

- [x] Copy `docs/example-mappings/no-groups.yaml` over `brr.angiebrr.BrrControllerGlyphs.mapping.yaml` and save; **Mapping status** should say it loaded.
- [x] `groups_none_hud_quickslots.jpg`: the hotbar and AzuEPI's bar.
- [x] `groups_none_inventory_quickslots.jpg`: the inventory's top row and AzuEPI's quick-slot cells.
- [x] Copy `multiple-groups.yaml` over the mapping and save.
- [x] `groups_multiple_hud_quickslots.jpg`
- [x] `groups_multiple_inventory_quickslots.jpg`
- [x] Put your own mapping back.

## Round 2: no quick slots (restart 1)

- [x] Set AzuEPI's **Number of Quick Slots** to `0`, quit and restart.
- [x] `groups_one_hud.jpg`: your mapping, the hotbar alone.
- [x] `groups_one_inventory.jpg`: the inventory's top row.
- [x] Copy `no-groups.yaml` over the mapping and save.
- [x] `groups_none_hud.jpg`
- [x] `groups_none_inventory.jpg`
- [x] Copy `multiple-groups.yaml` over the mapping and save.
- [x] `groups_multiple_hud.jpg`
- [x] `groups_multiple_inventory.jpg`
- [x] Put your own mapping back.

## Round 3: mod off (restart 2)

- [x] Set AzuEPI's **Number of Quick Slots** back to `6` and the mod's **Enabled** off, quit and restart.
- [x] `hotbar_vanilla.jpg`: the hotbar and AzuEPI's bar, same view as `hotbar_neutral`.
- [x] `buttons_vanilla.jpg`: close-up of hotbar slots 1–4, same crop as `buttons_valheim`.
- [x] `prompt_vanilla.jpg`: the chest prompt after pressing the same key as for `prompt_mod`: keyboard keys. Same crop.
- [x] `badges_vanilla.jpg`: the inventory after moving the trackpad cursor: badges gone. Same crop as `badges_mod`.
- [x] `hints_vanilla.jpg`: the same hint as `hints_mod`, same crop, after pressing a gamepad button.

## Round 4: back to normal (restart 3)

- [x] Turn **Enabled** back on, quit and restart.
- [x] Check your mapping is your own and **Mapping status** says it loaded.

## Notes on the shots taken (2026-11-09)

- Taken with Steam's screenshot key at 2560 × 1600 and cropped at full size; saved as JPEG (the screenshots are JPEG already, and PNG made the folder 15 MB instead of 2 MB).
- `hotbar_neutral` and `hotbar_palette` are the same outdoor spot, from 2000 × 1250 copies scaled up to the other HUD crops' size. `groups_one_inventory_quickslots` is from the earlier batch (also `Palette`).
- `badges_mod` uses the no-groups mapping, the only inventory shot with the mod on and 6 quick slots in the batch. Retake with your own mapping if the different labels distract.

## What each table shows

| Table on the mod page | Files |
|---|---|
| Prompts stay controller glyphs | `prompt_vanilla`, `prompt_mod`, `badges_vanilla`, `badges_mod` |
| Your buttons instead of keyboard keys | `buttons_vanilla`, `buttons_valheim`, `buttons_steamdeck` |
| Hotbar group colors | `hotbar_vanilla`, `hotbar_neutral`, `hotbar_palette` |
| Hotbar groups (mapping × HUD/inventory × quick slots) | `groups_{none,one,multiple}_{hud,inventory}[_quickslots]` |
| Other mods' hover hints | `hints_vanilla`, `hints_mod` |
| Checking your mapping | `audit_missing_keys`, `audit_status_error` |
