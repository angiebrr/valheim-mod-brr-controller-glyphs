// ------------------------------------------------------------------------------------------------
// BarLabels.cs: splits a bar's shared hold button(s) from each slot's own button (F3).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// A bar's labels: the leading buttons every mapped slot shares (e.g. the R4 action-set hold),
    /// shown once beside the bar, and each slot's remaining buttons (e.g. D-pad up), shown in the
    /// slot. Slot labels with the full combo were too wide for a slot.
    /// </summary>
    public sealed class BarLabels
    {
        /// <summary>
        /// Creates the result; use <see cref="Split"/>.
        /// </summary>
        /// <param name="shared">Buttons shown once beside the bar.</param>
        /// <param name="slots">Each slot's remaining buttons; null leaves the slot alone.</param>
        private BarLabels(IReadOnlyList<string> shared, IReadOnlyList<IReadOnlyList<string>> slots)
        {
            Shared = shared;
            Slots = slots;
        }

        /// <summary>Leading buttons every mapped slot shares; empty if none.</summary>
        public IReadOnlyList<string> Shared { get; }

        /// <summary>Each slot's buttons after the shared ones (at least one); null for unmapped slots.</summary>
        public IReadOnlyList<IReadOnlyList<string>> Slots { get; }

        /// <summary>
        /// Splits slot buttons into the shared leading buttons and each slot's remainder.
        /// </summary>
        /// <param name="slots">Each slot's buttons (<see cref="SlotLabel.Buttons"/>); null for
        /// unmapped slots, which are ignored.</param>
        /// <returns>The split labels. Every mapped slot keeps at least one button.</returns>
        public static BarLabels Split(IReadOnlyList<IReadOnlyList<string>> slots)
        {
            var mapped = slots.Where(slot => slot != null && slot.Count > 0).ToList();
            if (mapped.Count == 0)
            {
                return new BarLabels(Array.Empty<string>(), slots.ToArray());
            }

            // Shared prefix, capped so the shortest slot keeps one button of its own
            int shared = mapped.Min(slot => slot.Count) - 1;
            IReadOnlyList<string> first = mapped[0];
            foreach (IReadOnlyList<string> slot in mapped)
            {
                int same = 0;
                while (same < shared && KeyMap.Normalize(slot[same]) == KeyMap.Normalize(first[same]))
                {
                    same++;
                }

                shared = same;
            }

            IReadOnlyList<IReadOnlyList<string>> remainders = slots
                .Select(slot => slot == null ? null : (IReadOnlyList<string>)slot.Skip(shared).ToArray())
                .ToArray();
            return new BarLabels(first.Take(shared).ToArray(), remainders);
        }
    }
}
