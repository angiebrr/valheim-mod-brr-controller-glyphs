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
    /// are on. Labels come from <see cref="SlotLabel.For"/>; this class only reads keys and sets text.
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

        /// <summary>Per bar (Unity instance ID): slot count and glyph state when labels were last written.</summary>
        private readonly Dictionary<int, (int SlotCount, bool ShowGlyphs)> m_lastWritten = new ();

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
        /// Writes a bar's labels if anything relevant changed since the last write (or if forced).
        /// Cheap when nothing changed, since UpdateIcons calls this every frame.
        /// </summary>
        /// <param name="bar">The hotbar.</param>
        /// <param name="force">Write even if nothing changed (Valheim just rewrote the labels).</param>
        internal void Update(HotkeyBar bar, bool force)
        {
            if (bar.name != VanillaBarName && bar.name != QuickSlotBarName)
            {
                return;
            }

            bool showGlyphs = m_glyphs.ShowHotbarLabels;
            int key = bar.GetInstanceID();
            bool known = m_lastWritten.TryGetValue(key, out var last);
            if (!force && known && last.SlotCount == bar.m_elements.Count && last.ShowGlyphs == showGlyphs)
            {
                return;
            }

            m_lastWritten[key] = (bar.m_elements.Count, showGlyphs);

            if (!showGlyphs)
            {
                // Glyphs just turned off: let Valheim restore its own labels
                if (known && last.ShowGlyphs)
                {
                    bar.ToggleBindingHint(!ZInput.IsGamepadActive());
                }

                return;
            }

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

        /// <summary>
        /// Builds the label for one slot from its keyboard shortcut.
        /// </summary>
        /// <param name="bar">Valheim's hotbar or AzuEPI's quick-slot bar.</param>
        /// <param name="slot">Zero-based slot index.</param>
        /// <returns>The label, or null to leave the slot's text alone.</returns>
        private string LabelFor(HotkeyBar bar, int slot)
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
        private static TMP_Text BindingText(GameObject slot)
        {
            Transform binding = slot ? slot.transform.Find("binding") : null;
            return binding ? binding.GetComponent<TMP_Text>() : null;
        }
    }
}
