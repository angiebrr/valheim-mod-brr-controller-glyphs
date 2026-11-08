// ------------------------------------------------------------------------------------------------
// InventoryLabelWriter.cs: labels the inventory's top row and AzuEPI's quick-slot cells (F3).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using ControllerGlyphFixer.Core;
using TMPro;
using UnityEngine;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// While controller glyphs are on, labels the player inventory's top row (the hotbar row,
    /// "1".."8" in vanilla) and AzuEPI's quick-slot cells (its "Display Text") with each slot's own
    /// button at a smaller size, and draws each prefix group's prefix above its slots. Puts the
    /// game's and AzuEPI's text back when glyphs turn off.
    /// </summary>
    internal sealed class InventoryLabelWriter
    {
        /// <summary>Prefix container for the top row (its parent may also hold the quick-slot cells).</summary>
        internal const string TopRowContainer = "cgf_PrefixLabels_TopRow";

        /// <summary>Prefix container for AzuEPI's quick-slot cells.</summary>
        internal const string QuickSlotContainer = "cgf_PrefixLabels_QuickSlots";

        /// <summary>Glyph size in inventory cells, relative to the cell's label font.</summary>
        // TODO(F3): check in game; make it a setting if people disagree on the size.
        private const string LabelSize = "70%";

        /// <summary>Glyph mode, mapping lookups and glyph formatting.</summary>
        private readonly GlyphService m_glyphs;

        /// <summary>Builds the hotbar and quick-slot labels the inventory shares with the HUD.</summary>
        private readonly HotbarLabelWriter m_hotbar;

        /// <summary>Draws the group prefixes above the top row and the quick-slot cells.</summary>
        private readonly PrefixLabels m_prefixLabels;

        /// <summary>Quick-slot cells found so far, to their quick-slot index. Found by AzuEPI's text,
        /// which is gone once replaced, so remembered until the cells are rebuilt.</summary>
        private readonly Dictionary<InventoryElement, int> m_quickSlotCells = new ();

        /// <summary>State when the grid's labels were last handled; null before the first time.</summary>
        private SlotBarState? m_lastState;

        /// <summary>
        /// Creates the writer.
        /// </summary>
        /// <param name="glyphs">Glyph mode, mapping lookups and glyph formatting.</param>
        /// <param name="hotbar">Builds the hotbar and quick-slot labels.</param>
        /// <param name="prefixLabels">Draws the group prefixes.</param>
        internal InventoryLabelWriter(GlyphService glyphs, HotbarLabelWriter hotbar, PrefixLabels prefixLabels)
        {
            m_glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
            m_hotbar = hotbar ?? throw new ArgumentNullException(nameof(hotbar));
            m_prefixLabels = prefixLabels ?? throw new ArgumentNullException(nameof(prefixLabels));
        }

        /// <summary>
        /// Writes or restores the labels if the grid was rebuilt or glyphs toggled. Cheap when
        /// nothing changed, since the inventory updates every frame while it's open.
        /// </summary>
        /// <param name="gui">The inventory screen.</param>
        internal void Update(InventoryGui gui)
        {
            InventoryGrid grid = gui.m_playerGrid;
            if (!grid)
            {
                return;
            }

            List<InventoryElement> cells = grid.m_elements;
            InventoryElement first = cells.Count > 0 ? cells[0] : null;
            var current = new SlotBarState(cells.Count, first ? first.gameObject.GetInstanceID() : 0, m_glyphs.ShowHotbarLabels);
            SlotLabelAction action = SlotLabelRefresh.Decide(m_lastState, current, force: false);
            m_lastState = current;
            if (action == SlotLabelAction.Skip)
            {
                return;
            }

            FindQuickSlotCells(cells);
            if (action == SlotLabelAction.Write)
            {
                WriteTopRow(grid);
                WriteQuickSlots();
            }
            else
            {
                RestoreTopRow(grid);
                RestoreQuickSlots();
            }
        }

        /// <summary>
        /// Forgets what was written, so the labels are rewritten on the next update (e.g. after the
        /// prefix colour setting changes).
        /// </summary>
        internal void Invalidate()
        {
            m_lastState = null;
        }

        /// <summary>
        /// The label a top-row cell should show.
        /// </summary>
        /// <param name="slot">Zero-based column.</param>
        /// <returns>The scaled label, or null if the slot's key isn't mapped.</returns>
        internal string TopRowLabel(int slot)
        {
            return Scaled(m_hotbar.SlotText(m_hotbar.HotbarLabels(), slot));
        }

        /// <summary>
        /// The label a quick-slot cell should show.
        /// </summary>
        /// <param name="slot">Zero-based quick-slot index.</param>
        /// <returns>The scaled label, or null if the slot's key isn't mapped.</returns>
        internal string QuickSlotLabel(int slot)
        {
            return Scaled(m_hotbar.SlotText(m_hotbar.QuickSlotLabels(), slot));
        }

        /// <summary>
        /// The cell found for a quick slot.
        /// </summary>
        /// <param name="slot">Zero-based quick-slot index.</param>
        /// <returns>The cell, or null if it wasn't found (yet).</returns>
        internal InventoryElement QuickSlotCell(int slot)
        {
            return m_quickSlotCells.FirstOrDefault(pair => pair.Key && pair.Value == slot).Key;
        }

        /// <summary>
        /// Finds AzuEPI's quick-slot cells by the names it writes on them, and forgets cells that
        /// were destroyed.
        /// </summary>
        /// <param name="cells">The player grid's cells.</param>
        private void FindQuickSlotCells(List<InventoryElement> cells)
        {
            foreach (InventoryElement gone in m_quickSlotCells.Keys.Where(cell => !cell).ToList())
            {
                m_quickSlotCells.Remove(gone);
            }

            int count = Math.Min(QuickSlotKeys.Count ?? 0, 8);
            var names = new string[count];
            for (int slot = 0; slot < count; slot++)
            {
                names[slot] = QuickSlotKeys.TryGetCellName(slot, out string name) && !m_quickSlotCells.ContainsValue(slot) ? name : null;
            }

            string[] texts = cells.Select(cell => cell && !m_quickSlotCells.ContainsKey(cell) ? BindingText(cell)?.text : null).ToArray();
            int[] found = QuickSlotCells.Find(texts, names);
            for (int slot = 0; slot < found.Length; slot++)
            {
                if (found[slot] >= 0)
                {
                    m_quickSlotCells[cells[found[slot]]] = slot;
                }
            }
        }

        /// <summary>
        /// Labels the top row and draws its prefixes above it.
        /// </summary>
        /// <param name="grid">The player grid.</param>
        private void WriteTopRow(InventoryGrid grid)
        {
            List<InventoryElement> topRow = TopRow(grid);
            if (topRow.Count == 0)
            {
                return;
            }

            BarLabels labels = m_hotbar.HotbarLabels();
            for (int slot = 0; slot < topRow.Count; slot++)
            {
                SetLabel(topRow[slot], Scaled(m_hotbar.SlotText(labels, slot)));
            }

            List<PrefixLabel> prefixes = m_hotbar.PrefixLabelsFor(labels,
                slot => slot < topRow.Count ? (RectTransform)topRow[slot].transform : null);
            m_prefixLabels.Show(topRow[0].transform.parent, prefixes, BindingText(topRow[0]), PrefixSide.Above, TopRowContainer);
        }

        /// <summary>
        /// Labels AzuEPI's quick-slot cells and draws their prefixes above each group.
        /// </summary>
        private void WriteQuickSlots()
        {
            InventoryElement first = QuickSlotCell(0) ?? m_quickSlotCells.Keys.FirstOrDefault(cell => cell);
            if (!first)
            {
                return;
            }

            BarLabels labels = m_hotbar.QuickSlotLabels();
            foreach (KeyValuePair<InventoryElement, int> cell in m_quickSlotCells.Where(pair => pair.Key))
            {
                SetLabel(cell.Key, Scaled(m_hotbar.SlotText(labels, cell.Value)));
            }

            Transform parent = first.transform.parent;
            List<PrefixLabel> prefixes = m_hotbar.PrefixLabelsFor(labels, slot =>
            {
                InventoryElement cell = QuickSlotCell(slot);
                return cell && cell.transform.parent == parent ? (RectTransform)cell.transform : null;
            });
            m_prefixLabels.Show(parent, prefixes, BindingText(first), PrefixSide.Above, QuickSlotContainer);
        }

        /// <summary>
        /// Puts vanilla's "1".."8" back on the top row and removes its prefixes.
        /// </summary>
        /// <param name="grid">The player grid.</param>
        private static void RestoreTopRow(InventoryGrid grid)
        {
            List<InventoryElement> topRow = TopRow(grid);
            for (int slot = 0; slot < topRow.Count; slot++)
            {
                // FIXME: copied from vanilla InventoryGrid.UpdateGui; if Valheim changes it, update here.
                SetLabel(topRow[slot], (slot + 1).ToString());
            }

            if (topRow.Count > 0)
            {
                PrefixLabels.Clear(topRow[0].transform.parent, TopRowContainer);
            }
        }

        /// <summary>
        /// Puts AzuEPI's names back on its quick-slot cells and removes their prefixes.
        /// </summary>
        private void RestoreQuickSlots()
        {
            foreach (KeyValuePair<InventoryElement, int> cell in m_quickSlotCells.Where(pair => pair.Key))
            {
                if (QuickSlotKeys.TryGetCellName(cell.Value, out string name))
                {
                    SetLabel(cell.Key, name);
                }

                PrefixLabels.Clear(cell.Key.transform.parent, QuickSlotContainer);
            }
        }

        /// <summary>
        /// The top row's cells: the first row of the player grid, minus any AzuEPI moved there.
        /// </summary>
        /// <param name="grid">The player grid.</param>
        /// <returns>The cells in column order; at most the grid's width.</returns>
        private static List<InventoryElement> TopRow(InventoryGrid grid)
        {
            return grid.m_elements.Take(grid.m_width).Where(cell => cell).ToList();
        }

        /// <summary>
        /// Sets a cell's label if it has one and the text differs.
        /// </summary>
        /// <param name="cell">The cell.</param>
        /// <param name="label">The new text; null leaves the cell alone.</param>
        private static void SetLabel(InventoryElement cell, string label)
        {
            TMP_Text text = BindingText(cell);
            if (text && label != null && text.text != label)
            {
                text.text = label;
            }
        }

        /// <summary>
        /// Finds a cell's label text.
        /// </summary>
        /// <param name="cell">The cell.</param>
        /// <returns>The "binding" label, or null if the cell has none.</returns>
        private static TMP_Text BindingText(InventoryElement cell)
        {
            return cell ? HotbarLabelWriter.BindingText(cell.gameObject) : null;
        }

        /// <summary>
        /// Shrinks a label for the inventory's cells.
        /// </summary>
        /// <param name="label">The label, or null.</param>
        /// <returns>The label wrapped in a size tag, or null.</returns>
        private static string Scaled(string label)
        {
            return label == null ? null : $"<size={LabelSize}>{label}</size>";
        }
    }
}
