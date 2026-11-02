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
        /// Whether this writer labels the bar (Valheim's hotbar or AzuEPI's quick-slot bar).
        /// </summary>
        /// <param name="bar">Any HotkeyBar.</param>
        /// <returns>True for the two bars this writer handles.</returns>
        internal static bool Handles(HotkeyBar bar)
        {
            return bar.name == VanillaBarName || bar.name == QuickSlotBarName;
        }

        /// <summary>
        /// Writes or restores a bar's labels if anything relevant changed since last time (or if
        /// forced). Cheap when nothing changed, since UpdateIcons calls this every frame.
        /// </summary>
        /// <param name="bar">The hotbar.</param>
        /// <param name="force">Valheim just rewrote the labels.</param>
        internal void Update(HotkeyBar bar, bool force)
        {
            if (!Handles(bar))
            {
                return;
            }

            GameObject firstSlot = bar.m_elements.Count > 0 ? bar.m_elements[0].m_go : null;
            var current = new SlotBarState(bar.m_elements.Count, firstSlot ? firstSlot.GetInstanceID() : 0,
                m_glyphs.ShowHotbarLabels);
            SlotBarState? last = m_lastStates.TryGetValue(bar.name, out SlotBarState previous) ? previous : null;

            // Recorded first: Restore calls ToggleBindingHint, which re-enters this method
            m_lastStates[bar.name] = current;

            switch (SlotLabelRefresh.Decide(last, current, force))
            {
                case SlotLabelAction.Write:
                    WriteLabels(bar);
                    break;
                case SlotLabelAction.Restore:
                    bar.ToggleBindingHint(!ZInput.IsGamepadActive());
                    break;
            }
        }

        /// <summary>
        /// Builds the label for one slot from its keyboard shortcut.
        /// </summary>
        /// <param name="bar">Valheim's hotbar or AzuEPI's quick-slot bar.</param>
        /// <param name="slot">Zero-based slot index.</param>
        /// <returns>The label, or null to leave the slot's text alone.</returns>
        internal string LabelFor(HotkeyBar bar, int slot)
        {
            if (bar.name == VanillaBarName)
            {
                // Raw keyboard key (respects rebinding), not our translated text
                string key = OriginalBoundKeyString.Call(ZInput.instance, "Hotbar" + (slot + 1), emptyStringOnMissing: true);
                return SlotLabel.For(key, Array.Empty<string>(), showGlyphs: true, m_glyphs.TranslateKey);
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
        /// Sets every slot's label to its mapped buttons, leaving unmapped slots alone.
        /// </summary>
        /// <param name="bar">The hotbar.</param>
        private void WriteLabels(HotkeyBar bar)
        {
            for (int i = 0; i < bar.m_elements.Count; i++)
            {
                string label = LabelFor(bar, i);
                TMP_Text text = label == null ? null : BindingText(bar.m_elements[i].m_go);
                if (text != null && text.text != label)
                {
                    text.text = label;
                }
            }
        }
    }
}
