// ------------------------------------------------------------------------------------------------
// InventoryLabelWriter.cs: writes controller-button labels on the inventory's top row (F3).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using ControllerGlyphFixer.Core;
using TMPro;
using UnityEngine;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Writes the player inventory's top-row labels (the hotbar row, which Valheim always labels
    /// "1".."8") while controller glyphs are on, using the same labels as the hotbar.
    /// </summary>
    internal sealed class InventoryLabelWriter
    {
        /// <summary>Glyph mode.</summary>
        private readonly GlyphService m_glyphs;

        /// <summary>Builds the hotbar slot labels the top row shares.</summary>
        private readonly HotbarLabelWriter m_hotbar;

        /// <summary>State when the grid's labels were last handled; null before the first time.</summary>
        private SlotBarState? m_lastState;

        /// <summary>
        /// Creates the writer.
        /// </summary>
        /// <param name="glyphs">Glyph mode.</param>
        /// <param name="hotbar">Builds the hotbar slot labels.</param>
        internal InventoryLabelWriter(GlyphService glyphs, HotbarLabelWriter hotbar)
        {
            m_glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
            m_hotbar = hotbar ?? throw new ArgumentNullException(nameof(hotbar));
        }

        /// <summary>
        /// Writes or restores the top-row labels if the grid was rebuilt or glyphs toggled. Cheap
        /// when nothing changed, since UpdateGui runs every frame while the inventory is open.
        /// </summary>
        /// <param name="grid">An inventory grid; only the player's own grid is labelled.</param>
        internal void Update(InventoryGrid grid)
        {
            InventoryGui gui = InventoryGui.instance;
            if (!gui || grid != gui.m_playerGrid)
            {
                return;
            }

            List<InventoryElement> elements = grid.m_elements;
            InventoryElement first = elements.Count > 0 ? elements[0] : null;
            var current = new SlotBarState(elements.Count, first ? first.gameObject.GetInstanceID() : 0,
                m_glyphs.ShowHotbarLabels);
            SlotLabelAction action = SlotLabelRefresh.Decide(m_lastState, current, force: false);
            m_lastState = current;

            if (action == SlotLabelAction.Skip)
            {
                return;
            }

            // Same per-slot buttons as the hotbar; no hold badge here yet (no free space found beside the grid)
            BarLabels labels = action == SlotLabelAction.Write ? m_hotbar.LabelsFor(HotbarLabelWriter.VanillaBarName) : null;
            int topRow = Math.Min(grid.m_width, elements.Count);
            for (int slot = 0; slot < topRow; slot++)
            {
                TMP_Text text = HotbarLabelWriter.BindingText(elements[slot].gameObject);
                string label = labels != null ? m_hotbar.SlotText(labels, slot) : VanillaLabel(slot);
                if (text != null && label != null && text.text != label)
                {
                    text.text = label;
                }
            }
        }

        /// <summary>
        /// Valheim's own top-row label for a slot.
        /// </summary>
        /// <param name="slot">Zero-based column.</param>
        /// <returns>"1" for column 0, and so on.</returns>
        // FIXME: copied from vanilla InventoryGrid.UpdateGui; if Valheim changes it, update here.
        private static string VanillaLabel(int slot)
        {
            return (slot + 1).ToString();
        }
    }
}
