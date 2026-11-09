// ------------------------------------------------------------------------------------------------
// QuickSlotCells.cs: finds AzuEPI's quick-slot cells among the inventory's cells by their text (F3).
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core
{
    /// <summary>
    /// AzuEPI shows its quick slots as inventory-grid cells labelled with each slot's name
    /// (<see cref="AzuEpiQuickSlots.CellName"/>). Matching that text finds the cells without
    /// reading AzuEPI's internal types.
    /// </summary>
    public static class QuickSlotCells
    {
        /// <summary>
        /// Finds the cell showing each quick slot's name.
        /// </summary>
        /// <param name="cellTexts">Every cell's label text, in grid order; null for cells without one.</param>
        /// <param name="names">Each quick slot's name, in slot order.</param>
        /// <returns>For each quick slot, the index of its cell, or -1 if no cell shows its name.
        /// Each cell matches at most one slot.</returns>
        public static int[] Find(IReadOnlyList<string> cellTexts, IReadOnlyList<string> names)
        {
            var found = new int[names.Count];
            var taken = new bool[cellTexts.Count];
            for (int slot = 0; slot < names.Count; slot++)
            {
                found[slot] = -1;
                if (string.IsNullOrEmpty(names[slot]))
                {
                    continue;
                }

                for (int cell = 0; cell < cellTexts.Count; cell++)
                {
                    if (!taken[cell] && cellTexts[cell] == names[slot])
                    {
                        found[slot] = cell;
                        taken[cell] = true;
                        break;
                    }
                }
            }

            return found;
        }
    }
}
