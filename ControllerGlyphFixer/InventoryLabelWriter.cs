// ------------------------------------------------------------------------------------------------
// InventoryLabelWriter.cs: labels the inventory's top row and AzuEPI's quick-slot cells (F3).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using ControllerGlyphFixer.Core;
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
        // TODO(F3): make it a setting if people disagree on the size (70% looked right, 2026-11-07).
        private const string LabelSize = "70%";

        /// <summary>
        /// Space between the inventory's slots and their prefixes, enough to clear the panel's
        /// border and gamepad highlight (at the HUD's 2 units they sat on it; 2026-11-07).
        /// </summary>
        // TODO(F3): check in game; the border's height depends on the panel layout.
        private const float PanelGap = 24f;

        /// <summary>Glyph mode, mapping lookups and glyph formatting.</summary>
        private readonly GlyphService m_glyphs;

        /// <summary>Builds the hotbar and quick-slot labels the inventory shares with the HUD.</summary>
        private readonly HotbarLabelWriter m_hotbar;

        /// <summary>AzuEPI's quick-slot settings (count, cell names).</summary>
        private readonly QuickSlotKeys m_quickSlots;

        /// <summary>Draws the group prefixes above the top row and the quick-slot cells.</summary>
        private readonly PrefixLabels m_prefixLabels;

        /// <summary>
        /// Each quick slot's cell, by quick-slot index; null until found. Found by AzuEPI's text,
        /// which is gone once replaced, so remembered until the cells are destroyed.
        /// </summary>
        private readonly InventoryElement[] m_cellBySlot = new InventoryElement[QuickSlotKeys.MaxSlots];

        /// <summary>State when the grid's labels were last handled; null before the first time.</summary>
        private SlotBarState? m_lastState;

        /// <summary>
        /// Creates the writer.
        /// </summary>
        /// <param name="glyphs">Glyph mode, mapping lookups and glyph formatting.</param>
        /// <param name="hotbar">Builds the hotbar and quick-slot labels.</param>
        /// <param name="quickSlots">AzuEPI's quick-slot settings.</param>
        /// <param name="prefixLabels">Draws the group prefixes.</param>
        internal InventoryLabelWriter(GlyphService glyphs, HotbarLabelWriter hotbar, QuickSlotKeys quickSlots,
            PrefixLabels prefixLabels)
        {
            m_glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
            m_hotbar = hotbar ?? throw new ArgumentNullException(nameof(hotbar));
            m_quickSlots = quickSlots ?? throw new ArgumentNullException(nameof(quickSlots));
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
            SlotBarState current = BindingLabels.State(cells.Count, cells.Count > 0 ? cells[0] : null, m_glyphs.ShowHotbarLabels);
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
        /// Forgets what was written, so the labels are rewritten on the next update (e.g. after a
        /// prefix setting changes).
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
        /// <returns>The cell, or null if it wasn't found (yet) or was destroyed.</returns>
        internal InventoryElement QuickSlotCell(int slot)
        {
            InventoryElement cell = slot >= 0 && slot < m_cellBySlot.Length ? m_cellBySlot[slot] : null;
            return cell ? cell : null;
        }

        /// <summary>
        /// Finds AzuEPI's quick-slot cells by the names it writes on them; cells already found are
        /// kept until they're destroyed.
        /// </summary>
        /// <param name="cells">The player grid's cells.</param>
        private void FindQuickSlotCells(List<InventoryElement> cells)
        {
            var known = new HashSet<InventoryElement>(m_cellBySlot.Where(cell => cell));
            int count = m_quickSlots.SlotCount;
            var names = new string[count];
            for (int slot = 0; slot < count; slot++)
            {
                names[slot] = !QuickSlotCell(slot) && m_quickSlots.TryGetCellName(slot, out string name) ? name : null;
            }

            string[] texts = cells.Select(cell => cell && !known.Contains(cell) ? BindingLabels.Text(cell.gameObject) : null).ToArray();
            int[] found = QuickSlotCells.Find(texts, names);
            for (int slot = 0; slot < found.Length; slot++)
            {
                if (found[slot] >= 0)
                {
                    m_cellBySlot[slot] = cells[found[slot]];
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
            InventoryElement first = topRow.FirstOrDefault(cell => cell);
            if (!first)
            {
                return;
            }

            BarLabels labels = m_hotbar.HotbarLabels();
            for (int slot = 0; slot < topRow.Count; slot++)
            {
                SetLabel(topRow[slot], Scaled(m_hotbar.SlotText(labels, slot)));
            }

            List<PrefixLabel> prefixes = m_hotbar.PrefixLabelsFor(labels,
                slot => slot < topRow.Count && topRow[slot] ? (RectTransform)topRow[slot].transform : null);
            m_prefixLabels.Show(first.transform.parent, prefixes, BindingLabels.Find(first.gameObject), PrefixSide.Above,
                TopRowContainer, PanelGap);
        }

        /// <summary>
        /// Labels AzuEPI's quick-slot cells and draws their prefixes above each group.
        /// </summary>
        private void WriteQuickSlots()
        {
            InventoryElement first = Enumerable.Range(0, m_cellBySlot.Length).Select(QuickSlotCell).FirstOrDefault(cell => cell);
            if (!first)
            {
                return;
            }

            BarLabels labels = m_hotbar.QuickSlotLabels();
            for (int slot = 0; slot < m_cellBySlot.Length; slot++)
            {
                SetLabel(QuickSlotCell(slot), Scaled(m_hotbar.SlotText(labels, slot)));
            }

            Transform parent = first.transform.parent;
            List<PrefixLabel> prefixes = m_hotbar.PrefixLabelsFor(labels, slot =>
            {
                InventoryElement cell = QuickSlotCell(slot);
                return cell && cell.transform.parent == parent ? (RectTransform)cell.transform : null;
            });
            m_prefixLabels.Show(parent, prefixes, BindingLabels.Find(first.gameObject), PrefixSide.Above, QuickSlotContainer,
                PanelGap);
        }

        /// <summary>
        /// Puts vanilla's "1".."8" back on the top row and removes its prefixes.
        /// </summary>
        /// <param name="grid">The player grid.</param>
        private void RestoreTopRow(InventoryGrid grid)
        {
            List<InventoryElement> topRow = TopRow(grid);
            for (int slot = 0; slot < topRow.Count; slot++)
            {
                // FIXME: copied from vanilla InventoryGrid.UpdateGui; if Valheim changes it, update here.
                SetLabel(topRow[slot], (slot + 1).ToString());
            }

            InventoryElement first = topRow.FirstOrDefault(cell => cell);
            if (first)
            {
                m_prefixLabels.Clear(first.transform.parent, TopRowContainer);
            }
        }

        /// <summary>
        /// Puts AzuEPI's names back on its quick-slot cells and removes their prefixes.
        /// </summary>
        private void RestoreQuickSlots()
        {
            for (int slot = 0; slot < m_cellBySlot.Length; slot++)
            {
                InventoryElement cell = QuickSlotCell(slot);
                if (!cell)
                {
                    continue;
                }

                if (m_quickSlots.TryGetCellName(slot, out string name))
                {
                    SetLabel(cell, name);
                }

                m_prefixLabels.Clear(cell.transform.parent, QuickSlotContainer);
            }
        }

        /// <summary>
        /// The top row's cells: the first row of the player grid, in column order. Destroyed cells
        /// stay in place (as destroyed objects) so the others keep their column numbers.
        /// </summary>
        /// <param name="grid">The player grid.</param>
        /// <returns>At most the grid's width of cells.</returns>
        private List<InventoryElement> TopRow(InventoryGrid grid)
        {
            return grid.m_elements.Take(grid.m_width).ToList();
        }

        /// <summary>
        /// Sets a cell's label if the cell is alive, has a label, and the text differs.
        /// </summary>
        /// <param name="cell">The cell; may be null or destroyed.</param>
        /// <param name="label">The new text; null leaves the cell alone.</param>
        private void SetLabel(InventoryElement cell, string label)
        {
            if (cell)
            {
                BindingLabels.Set(cell.gameObject, label);
            }
        }

        /// <summary>
        /// Shrinks a label for the inventory's cells.
        /// </summary>
        /// <param name="label">The label, or null.</param>
        /// <returns>The label wrapped in a size tag, or null.</returns>
        private string Scaled(string label)
        {
            return label == null ? null : $"<size={LabelSize}>{label}</size>";
        }
    }
}
