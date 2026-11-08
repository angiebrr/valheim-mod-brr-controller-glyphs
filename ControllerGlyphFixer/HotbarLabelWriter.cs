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
    /// are on: each slot shows its own button, and each group of slots sharing a hold button
    /// (R4/R5) gets one badge and line (<see cref="HoldBadges"/>). When and what to write is
    /// decided in Core (<see cref="SlotLabelRefresh"/>, <see cref="SlotLabel"/>,
    /// <see cref="BarLabels"/>, <see cref="BadgeColors"/>); this class reads game state and sets text.
    /// </summary>
    internal sealed class HotbarLabelWriter
    {
        /// <summary>Name of Valheim's own hotbar object.</summary>
        private const string VanillaBarName = "HotKeyBar";

        /// <summary>Name of AzuExtendedPlayerInventory's quick-slot bar (a second HotkeyBar).</summary>
        private const string QuickSlotBarName = "AzuEPI_QuickAccessBar";

        /// <summary>Slots per bar whose shortcuts decide the groups (Valheim and AzuEPI have 8).</summary>
        private const int MaxSlots = 8;

        /// <summary>Glyph mode, mapping lookups and glyph formatting.</summary>
        private readonly GlyphService m_glyphs;

        /// <summary>AzuEPI's quick-slot shortcuts.</summary>
        private readonly QuickSlotKeys m_quickSlots;

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
        internal HotbarLabelWriter(GlyphService glyphs, QuickSlotKeys quickSlots)
        {
            m_glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
            m_quickSlots = quickSlots ?? throw new ArgumentNullException(nameof(quickSlots));
        }

        /// <summary>
        /// Whether this writer labels a bar (Valheim's hotbar or AzuEPI's quick-slot bar).
        /// </summary>
        /// <param name="barName">The HotkeyBar's object name.</param>
        /// <returns>True for the two bars this writer handles.</returns>
        internal static bool Handles(string barName)
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

            GameObject firstSlot = bar.m_elements.Count > 0 ? bar.m_elements[0].m_go : null;
            var current = new SlotBarState(bar.m_elements.Count, firstSlot ? firstSlot.GetInstanceID() : 0,
                m_glyphs.ShowHotbarLabels);
            SlotBarState? last = m_lastStates.TryGetValue(barName, out SlotBarState previous) ? previous : null;

            // Recorded first: Restore calls ToggleBindingHint, which re-enters this method
            m_lastStates[barName] = current;

            switch (SlotLabelRefresh.Decide(last, current, force))
            {
                case SlotLabelAction.Write:
                    WriteLabels(bar, barName);
                    break;
                case SlotLabelAction.Restore:
                    HoldBadges.Clear(bar.transform);
                    bar.ToggleBindingHint(!ZInput.IsGamepadActive());
                    break;
            }
        }

        /// <summary>
        /// Builds a bar's labels from the shortcuts of every slot the bar can show (not only the
        /// ones on screen now, so the shared button doesn't change as items are bound).
        /// </summary>
        /// <param name="barName">Name of Valheim's hotbar or AzuEPI's quick-slot bar.</param>
        /// <returns>The shared buttons and each slot's own buttons.</returns>
        internal BarLabels LabelsFor(string barName)
        {
            // AzuEPI keeps shortcuts for 8 quick slots; unused ones (default Alt + 7, ...) would stop sharing
            int slotCount = barName == VanillaBarName
                ? MaxSlots
                : Math.Min(QuickSlotKeys.Count ?? MaxSlots, MaxSlots);
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
        /// Renders the badge texts of the groups with slots on screen, in order.
        /// </summary>
        /// <param name="labels">The bar's labels from <see cref="LabelsFor"/>.</param>
        /// <param name="slotsOnScreen">Number of slots the bar shows.</param>
        /// <returns>One text per group that starts on screen.</returns>
        internal IReadOnlyList<string> BadgeTexts(BarLabels labels, int slotsOnScreen)
        {
            return labels.Groups
                .Where(group => group.FirstSlot < slotsOnScreen)
                .Select(group => m_glyphs.FormatButtons(group.Prefix))
                .ToArray();
        }

        /// <summary>
        /// Forgets what was written, so every bar is rewritten on its next update (e.g. after the
        /// badge colour setting changes).
        /// </summary>
        internal void Invalidate()
        {
            m_lastStates.Clear();
        }

        /// <summary>
        /// Finds a slot's label text.
        /// </summary>
        /// <param name="slot">The slot's GameObject.</param>
        /// <returns>The "binding" label, or null if the slot has none.</returns>
        internal static TMP_Text BindingText(GameObject slot)
        {
            Transform binding = slot ? slot.transform.Find("binding") : null;
            return binding ? binding.GetComponent<TMP_Text>() : null;
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
        /// badge and line for each group.
        /// </summary>
        /// <param name="bar">The hotbar.</param>
        /// <param name="barName">The bar's name, already read.</param>
        private void WriteLabels(HotkeyBar bar, string barName)
        {
            BarLabels labels = LabelsFor(barName);
            TMP_Text style = null;
            for (int i = 0; i < bar.m_elements.Count; i++)
            {
                TMP_Text text = BindingText(bar.m_elements[i].m_go);
                style = style ? style : text;
                string label = SlotText(labels, i);
                if (text && label != null && text.text != label)
                {
                    text.text = label;
                }
            }

            // Below for AzuEPI's bar: it sits right under the hotbar
            BadgeSide side = barName == QuickSlotBarName ? BadgeSide.Below : BadgeSide.Above;
            HoldBadges.Show(bar.transform, BadgesFor(bar, labels), style, side);
        }

        /// <summary>
        /// Builds the badges for the groups that have slots on screen.
        /// </summary>
        /// <param name="bar">The hotbar.</param>
        /// <param name="labels">The bar's labels.</param>
        /// <returns>One badge per group with at least one slot on screen.</returns>
        private List<HoldBadge> BadgesFor(HotkeyBar bar, BarLabels labels)
        {
            var badges = new List<HoldBadge>();
            foreach (BarGroup group in labels.Groups)
            {
                // Skip slots another mod destroyed without removing them from the list yet
                var slots = bar.m_elements
                    .Skip(group.FirstSlot)
                    .Take(group.SlotCount)
                    .Where(element => element.m_go)
                    .Select(element => (RectTransform)element.m_go.transform)
                    .ToList();
                if (slots.Count > 0)
                {
                    badges.Add(new HoldBadge(m_glyphs.FormatButtons(group.Prefix), m_glyphs.BadgeColor(group.Prefix), slots));
                }
            }

            return badges;
        }
    }
}
