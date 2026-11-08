// ------------------------------------------------------------------------------------------------
// HotbarLabelWriter.cs: writes controller-button labels on hotbar and quick-slot slots (F3).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using ControllerGlyphFixer.Core;
using ControllerGlyphFixer.Patches;
using TMPro;
using UnityEngine;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Writes slot labels on Valheim's hotbar and AzuEPI's quick-slot bar while controller glyphs
    /// are on: each slot shows its own button, and each group of slots sharing a prefix button
    /// (R4/R5) gets one prefix and line (<see cref="PrefixLabels"/>). When and what to write is
    /// decided in Core (<see cref="SlotLabelRefresh"/>, <see cref="SlotLabel"/>,
    /// <see cref="BarLabels"/>, <see cref="PrefixColors"/>); this class reads game state and sets text.
    /// </summary>
    internal sealed class HotbarLabelWriter
    {
        /// <summary>Name of Valheim's own hotbar object.</summary>
        private const string VanillaBarName = "HotKeyBar";

        /// <summary>Name of AzuExtendedPlayerInventory's quick-slot bar (a second HotkeyBar).</summary>
        private const string QuickSlotBarName = "AzuEPI_QuickAccessBar";

        /// <summary>Valheim's hotbar slots ("Hotbar1".."Hotbar8"); all of them decide its groups.</summary>
        private const int HotbarSlots = 8;

        /// <summary>Glyph mode, mapping lookups and glyph formatting.</summary>
        private readonly GlyphService m_glyphs;

        /// <summary>AzuEPI's quick-slot shortcuts.</summary>
        private readonly QuickSlotKeys m_quickSlots;

        /// <summary>Draws the group prefixes beside the bars.</summary>
        private readonly PrefixLabels m_prefixLabels;

        /// <summary>
        /// Per bar name: state when its labels were last handled. Keyed by name, not instance, so it
        /// holds at most two entries even though bars are recreated on every world load.
        /// </summary>
        private readonly Dictionary<string, SlotBarState> m_lastStates = new ();

        /// <summary>
        /// Creates the writer.
        /// </summary>
        /// <param name="glyphs">Glyph mode, mapping lookups and glyph formatting.</param>
        /// <param name="quickSlots">AzuEPI's quick-slot shortcuts.</param>
        /// <param name="prefixLabels">Draws the group prefixes beside the bars.</param>
        internal HotbarLabelWriter(GlyphService glyphs, QuickSlotKeys quickSlots, PrefixLabels prefixLabels)
        {
            m_glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
            m_quickSlots = quickSlots ?? throw new ArgumentNullException(nameof(quickSlots));
            m_prefixLabels = prefixLabels ?? throw new ArgumentNullException(nameof(prefixLabels));
        }

        /// <summary>
        /// Whether this writer labels a bar (Valheim's hotbar or AzuEPI's quick-slot bar).
        /// </summary>
        /// <param name="barName">The HotkeyBar's object name.</param>
        /// <returns>True for the two bars this writer handles.</returns>
        internal bool Handles(string barName)
        {
            return barName == VanillaBarName || barName == QuickSlotBarName;
        }

        /// <summary>
        /// Writes or restores a bar's labels if anything relevant changed since last time (or if
        /// forced). Cheap when nothing changed, since UpdateIcons calls this every frame.
        /// </summary>
        /// <param name="bar">The hotbar.</param>
        /// <param name="force">Valheim just rewrote the labels.</param>
        internal void Update(HotkeyBar bar, bool force)
        {
            // Read once: Unity allocates a new string on every read of name, and this runs every frame
            string barName = bar.name;
            if (!Handles(barName))
            {
                return;
            }

            SlotBarState current = BindingLabels.State(bar.m_elements.Count,
                bar.m_elements.Count > 0 ? bar.m_elements[0].m_go : null, m_glyphs.ShowHotbarLabels);
            SlotBarState? last = m_lastStates.TryGetValue(barName, out SlotBarState previous) ? previous : null;

            // Recorded first: Restore calls ToggleBindingHint, which re-enters this method
            m_lastStates[barName] = current;

            switch (SlotLabelRefresh.Decide(last, current, force))
            {
                case SlotLabelAction.Write:
                    WriteLabels(bar, barName);
                    break;
                case SlotLabelAction.Restore:
                    m_prefixLabels.Clear(bar.transform);
                    bar.ToggleBindingHint(!ZInput.IsGamepadActive());
                    break;
            }
        }

        /// <summary>
        /// Builds the labels of Valheim's hotbar slots (also used for the inventory's top row).
        /// </summary>
        /// <returns>The groups and each slot's own buttons.</returns>
        internal BarLabels HotbarLabels() => LabelsFor(VanillaBarName);

        /// <summary>
        /// Builds the labels of AzuEPI's quick slots (as on its quick-slot bar).
        /// </summary>
        /// <returns>The groups and each quick slot's own buttons.</returns>
        internal BarLabels QuickSlotLabels() => LabelsFor(QuickSlotBarName);

        /// <summary>
        /// Builds a bar's labels from the shortcuts of every slot the bar can show (not only the
        /// ones on screen now, so the groups don't change as items are bound).
        /// </summary>
        /// <param name="barName">Name of Valheim's hotbar or AzuEPI's quick-slot bar.</param>
        /// <returns>The groups and each slot's own buttons.</returns>
        internal BarLabels LabelsFor(string barName)
        {
            int slotCount = barName == VanillaBarName ? HotbarSlots : m_quickSlots.SlotCount;
            var slots = new IReadOnlyList<string>[slotCount];
            for (int slot = 0; slot < slotCount; slot++)
            {
                slots[slot] = barName == VanillaBarName ? HotbarSlotButtons(slot) : QuickSlotButtons(slot);
            }

            return BarLabels.Split(slots);
        }

        /// <summary>
        /// Renders one slot's own buttons.
        /// </summary>
        /// <param name="labels">The bar's labels from <see cref="LabelsFor"/>.</param>
        /// <param name="slot">Zero-based slot index.</param>
        /// <returns>The slot's label, or null to leave the slot's text alone.</returns>
        internal string SlotText(BarLabels labels, int slot)
        {
            return slot < labels.Slots.Count ? m_glyphs.FormatButtons(labels.Slots[slot]) : null;
        }

        /// <summary>
        /// Renders the prefix texts of the groups with slots on screen, in order.
        /// </summary>
        /// <param name="labels">The bar's labels from <see cref="LabelsFor"/>.</param>
        /// <param name="slotsOnScreen">Number of slots the bar shows.</param>
        /// <returns>One text per group that starts on screen.</returns>
        internal IReadOnlyList<string> PrefixTexts(BarLabels labels, int slotsOnScreen)
        {
            return labels.Groups
                .Where(group => group.FirstSlot < slotsOnScreen)
                .Select(group => m_glyphs.FormatButtons(group.Prefix))
                .ToArray();
        }

        /// <summary>
        /// Forgets what was written, so every bar is rewritten on its next update (e.g. after the
        /// prefix colour setting changes).
        /// </summary>
        internal void Invalidate()
        {
            m_lastStates.Clear();
        }

        /// <summary>
        /// Buttons for one of Valheim's hotbar slots, from the raw key bound to "HotbarN".
        /// </summary>
        /// <param name="slot">Zero-based slot index.</param>
        /// <returns>The buttons, or null if the slot's key isn't mapped.</returns>
        private IReadOnlyList<string> HotbarSlotButtons(int slot)
        {
            // Raw keyboard key (respects rebinding), not our translated text
            string key = OriginalBoundKeyString.Call(ZInput.instance, "Hotbar" + (slot + 1), emptyStringOnMissing: true);
            return SlotLabel.Buttons(key, null, m_glyphs.ButtonsFor);
        }

        /// <summary>
        /// Buttons for one of AzuEPI's quick slots, from its "Hotkey N" shortcut.
        /// </summary>
        /// <param name="slot">Zero-based slot index.</param>
        /// <returns>The buttons, or null if AzuEPI has no shortcut or it isn't mapped.</returns>
        private IReadOnlyList<string> QuickSlotButtons(int slot)
        {
            return m_quickSlots.TryGet(slot, out string mainKey, out IReadOnlyList<string> modifiers)
                ? SlotLabel.Buttons(mainKey, modifiers, m_glyphs.ButtonsFor)
                : null;
        }

        /// <summary>
        /// Sets every slot's label to its own buttons (leaving unmapped slots alone) and draws a
        /// prefix and line for each group.
        /// </summary>
        /// <param name="bar">The hotbar.</param>
        /// <param name="barName">The bar's name, already read.</param>
        private void WriteLabels(HotkeyBar bar, string barName)
        {
            BarLabels labels = LabelsFor(barName);
            TMP_Text style = null;
            for (int i = 0; i < bar.m_elements.Count; i++)
            {
                GameObject slot = bar.m_elements[i].m_go;
                style = style ? style : BindingLabels.Find(slot);
                BindingLabels.Set(slot, SlotText(labels, i));
            }

            // Below for AzuEPI's bar: it sits right under the hotbar
            PrefixSide side = barName == QuickSlotBarName ? PrefixSide.Below : PrefixSide.Above;
            m_prefixLabels.Show(bar.transform, PrefixLabelsFor(bar, labels), style, side,
                aboveClearance: PrefixLabels.SelectionClearance);
        }

        /// <summary>
        /// Builds the prefixes for a bar's groups that have slots on screen.
        /// </summary>
        /// <param name="bar">The hotbar.</param>
        /// <param name="labels">The bar's labels.</param>
        /// <returns>One prefix per group with at least one slot on screen.</returns>
        private List<PrefixLabel> PrefixLabelsFor(HotkeyBar bar, BarLabels labels)
        {
            // Skips slots another mod destroyed without removing them from the list yet
            return PrefixLabelsFor(labels, slot => slot < bar.m_elements.Count && bar.m_elements[slot].m_go
                ? (RectTransform)bar.m_elements[slot].m_go.transform
                : null);
        }

        /// <summary>
        /// Builds the prefixes for the groups that have slots on screen, wherever those slots are
        /// (a HUD bar, the inventory's top row, AzuEPI's inventory quick slots).
        /// </summary>
        /// <param name="labels">The labels from <see cref="LabelsFor"/>.</param>
        /// <param name="slotRect">Returns a slot's rect on screen, or null if it isn't shown.</param>
        /// <returns>One prefix per group with at least one slot on screen.</returns>
        internal List<PrefixLabel> PrefixLabelsFor(BarLabels labels, Func<int, RectTransform> slotRect)
        {
            var prefixes = new List<PrefixLabel>();
            foreach (PrefixGroup group in labels.Groups)
            {
                var slots = Enumerable.Range(group.FirstSlot, group.SlotCount)
                    .Select(slotRect)
                    .Where(rect => rect)
                    .ToList();
                if (slots.Count > 0)
                {
                    prefixes.Add(new PrefixLabel(m_glyphs.FormatButtons(group.Prefix), m_glyphs.PrefixColor(group.Prefix), slots));
                }
            }

            return prefixes;
        }
    }
}
