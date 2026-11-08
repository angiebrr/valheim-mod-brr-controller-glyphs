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
using Object = UnityEngine.Object;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Writes slot labels on Valheim's hotbar and AzuEPI's quick-slot bar while controller glyphs
    /// are on: each slot shows its own button, and the button every slot shares (the R4/R5 hold)
    /// shows once in a badge beside the bar. When and what to write is decided in Core
    /// (<see cref="SlotLabelRefresh"/>, <see cref="SlotLabel"/>, <see cref="BarLabels"/>); this
    /// class only reads game state and sets text.
    /// </summary>
    internal sealed class HotbarLabelWriter
    {
        /// <summary>Name of Valheim's own hotbar object.</summary>
        private const string VanillaBarName = "HotKeyBar";

        /// <summary>Name of AzuExtendedPlayerInventory's quick-slot bar (a second HotkeyBar).</summary>
        private const string QuickSlotBarName = "AzuEPI_QuickAccessBar";

        /// <summary>Name of the badge object this writer adds to a bar.</summary>
        internal const string BadgeName = "cgf_HoldBadge";

        /// <summary>Slots per bar whose shortcuts decide the shared button (Valheim and AzuEPI have 8).</summary>
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
                    SetBadge(bar, null);
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
        /// Renders the buttons shown once beside the bar.
        /// </summary>
        /// <param name="labels">The bar's labels from <see cref="LabelsFor"/>.</param>
        /// <returns>The badge text, or null if the slots share nothing.</returns>
        internal string BadgeText(BarLabels labels)
        {
            return m_glyphs.FormatButtons(labels.Shared);
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
        /// Finds the badge this writer added to a bar.
        /// </summary>
        /// <param name="bar">The hotbar.</param>
        /// <returns>The badge text, or null if the bar has none.</returns>
        internal static TMP_Text Badge(HotkeyBar bar)
        {
            Transform badge = bar.transform.Find(BadgeName);
            return badge ? badge.GetComponent<TMP_Text>() : null;
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
        /// Sets every slot's label to its own buttons (leaving unmapped slots alone) and the badge
        /// to the shared buttons.
        /// </summary>
        /// <param name="bar">The hotbar.</param>
        /// <param name="barName">The bar's name, already read.</param>
        private void WriteLabels(HotkeyBar bar, string barName)
        {
            BarLabels labels = LabelsFor(barName);
            for (int i = 0; i < bar.m_elements.Count; i++)
            {
                string label = SlotText(labels, i);
                TMP_Text text = label == null ? null : BindingText(bar.m_elements[i].m_go);
                if (text != null && text.text != label)
                {
                    text.text = label;
                }
            }

            SetBadge(bar, BadgeText(labels));
        }

        /// <summary>
        /// Shows the badge with the given text, creating it if needed, or removes it.
        /// </summary>
        /// <param name="bar">The hotbar.</param>
        /// <param name="text">Badge text; null removes the badge.</param>
        private static void SetBadge(HotkeyBar bar, string text)
        {
            TMP_Text badge = Badge(bar);
            if (text == null || bar.m_elements.Count == 0)
            {
                if (badge)
                {
                    Object.Destroy(badge.gameObject);
                }

                return;
            }

            badge = badge ? badge : CreateBadge(bar);
            if (!badge)
            {
                return;
            }

            PlaceBadge(bar, badge.rectTransform);
            if (badge.text != text)
            {
                badge.text = text;
            }
        }

        /// <summary>
        /// Adds a badge to a bar, styled like the bar's slot labels.
        /// </summary>
        /// <param name="bar">The hotbar; must have at least one slot.</param>
        /// <returns>The new badge, or null if the first slot has no label to copy the style from.</returns>
        private static TMP_Text CreateBadge(HotkeyBar bar)
        {
            TMP_Text style = BindingText(bar.m_elements[0].m_go);
            if (!style)
            {
                return null;
            }

            var badgeObject = new GameObject(BadgeName, typeof(RectTransform));
            badgeObject.transform.SetParent(bar.transform, worldPositionStays: false);

            TextMeshProUGUI badge = badgeObject.AddComponent<TextMeshProUGUI>();
            badge.font = style.font;
            badge.fontSharedMaterial = style.fontSharedMaterial;
            badge.fontSize = style.fontSize;
            badge.color = style.color;
            badge.alignment = TextAlignmentOptions.MidlineLeft;
            badge.textWrappingMode = TextWrappingModes.NoWrap;
            badge.raycastTarget = false;
            return badge;
        }

        /// <summary>
        /// Puts the badge where one more slot would go, right of the bar's rightmost slot on its
        /// first row. The bars sit at the left edge of the screen, so there's no room on their left.
        /// </summary>
        /// <param name="bar">The hotbar; must have at least one slot.</param>
        /// <param name="badge">The badge's rect.</param>
        // TODO(F3): check the position in game; make it configurable if it collides with other HUD mods.
        private static void PlaceBadge(HotkeyBar bar, RectTransform badge)
        {
            // Skip slots another mod destroyed without removing them from the list yet
            var slots = bar.m_elements.Where(element => element.m_go).Select(element => (RectTransform)element.m_go.transform).ToList();
            if (slots.Count == 0)
            {
                return;
            }

            RectTransform first = slots[0];
            float rightmost = slots.Max(slot => slot.localPosition.x);

            badge.anchorMin = first.anchorMin;
            badge.anchorMax = first.anchorMax;
            badge.pivot = first.pivot;
            badge.sizeDelta = first.sizeDelta;
            badge.localPosition = new Vector3(rightmost + bar.m_elementSpace, first.localPosition.y, 0f);
        }
    }
}
