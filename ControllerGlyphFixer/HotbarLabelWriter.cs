// ------------------------------------------------------------------------------------------------
// HotbarLabelWriter.cs: writes controller-button labels on hotbar and quick-slot slots (F3).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using ControllerGlyphFixer.Core;
using ControllerGlyphFixer.Patches;
using TMPro;
using UnityEngine;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Writes slot labels on Valheim's hotbar and AzuEPI's quick-slot bar while controller glyphs
    /// are on. When and what to write is decided in Core (<see cref="SlotLabelRefresh"/>,
    /// <see cref="SlotLabel"/>); this class only reads game state and sets text.
    /// </summary>
    internal sealed class HotbarLabelWriter
    {
        /// <summary>Name of Valheim's own hotbar object.</summary>
        private const string VanillaBarName = "HotKeyBar";

        /// <summary>Name of AzuExtendedPlayerInventory's quick-slot bar (a second HotkeyBar).</summary>
        private const string QuickSlotBarName = "AzuEPI_QuickAccessBar";

        /// <summary>Glyph mode and key translation.</summary>
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
        /// <param name="glyphs">Glyph mode and key translation.</param>
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
                    bar.ToggleBindingHint(!ZInput.IsGamepadActive());
                    break;
            }
        }

        /// <summary>
        /// Builds the label for one slot from its keyboard shortcut.
        /// </summary>
        /// <param name="barName">Name of Valheim's hotbar or AzuEPI's quick-slot bar.</param>
        /// <param name="slot">Zero-based slot index.</param>
        /// <returns>The label, or null to leave the slot's text alone.</returns>
        internal string LabelFor(string barName, int slot)
        {
            if (barName == VanillaBarName)
            {
                return HotbarSlotLabel(slot);
            }

            return m_quickSlots.TryGet(slot, out string mainKey, out IReadOnlyList<string> modifiers)
                ? SlotLabel.For(mainKey, modifiers, showGlyphs: true, m_glyphs.TranslateKey)
                : null;
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
        /// Builds the label for one of Valheim's hotbar slots (also used for the inventory's top row).
        /// </summary>
        /// <param name="slot">Zero-based slot index (0-7).</param>
        /// <returns>The label, or null if the slot's key isn't mapped.</returns>
        internal string HotbarSlotLabel(int slot)
        {
            // Raw keyboard key (respects rebinding), not our translated text
            string key = OriginalBoundKeyString.Call(ZInput.instance, "Hotbar" + (slot + 1), emptyStringOnMissing: true);
            return SlotLabel.For(key, Array.Empty<string>(), showGlyphs: true, m_glyphs.TranslateKey);
        }

        /// <summary>
        /// Sets every slot's label to its mapped buttons, leaving unmapped slots alone.
        /// </summary>
        /// <param name="bar">The hotbar.</param>
        /// <param name="barName">The bar's name, already read.</param>
        private void WriteLabels(HotkeyBar bar, string barName)
        {
            for (int i = 0; i < bar.m_elements.Count; i++)
            {
                string label = LabelFor(barName, i);
                TMP_Text text = label == null ? null : BindingText(bar.m_elements[i].m_go);
                if (text != null && text.text != label)
                {
                    text.text = label;
                }
            }
        }
    }
}
