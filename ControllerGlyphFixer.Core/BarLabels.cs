// ------------------------------------------------------------------------------------------------
// BarLabels.cs: groups a bar's slots by their shared prefix (F3).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// A run of neighbouring slots that share leading buttons (e.g. the R4 action-set hold). The
    /// prefix shows once beside the run; each slot shows only its own button(s).
    /// </summary>
    public sealed class PrefixGroup
    {
        /// <summary>
        /// Creates a group.
        /// </summary>
        /// <param name="firstSlot">Zero-based index of the group's first slot.</param>
        /// <param name="slotCount">Number of slots in the group.</param>
        /// <param name="prefix">Buttons every slot in the group starts with.</param>
        public PrefixGroup(int firstSlot, int slotCount, IReadOnlyList<string> prefix)
        {
            FirstSlot = firstSlot;
            SlotCount = slotCount;
            Prefix = prefix;
        }

        /// <summary>Zero-based index of the group's first slot.</summary>
        public int FirstSlot { get; }

        /// <summary>Number of slots in the group (at least one).</summary>
        public int SlotCount { get; }

        /// <summary>Buttons every slot in the group starts with (at least one).</summary>
        public IReadOnlyList<string> Prefix { get; }
    }

    /// <summary>
    /// A bar's labels: its groups (one prefix each) and each slot's remaining buttons. Slot labels
    /// with the full combo were too wide for a slot.
    /// </summary>
    public sealed class BarLabels
    {
        /// <summary>
        /// Creates the result; use <see cref="Split"/>.
        /// </summary>
        /// <param name="groups">Runs of slots sharing a prefix, in slot order.</param>
        /// <param name="slots">Each slot's remaining buttons; null leaves the slot alone.</param>
        private BarLabels(IReadOnlyList<PrefixGroup> groups, IReadOnlyList<IReadOnlyList<string>> slots)
        {
            Groups = groups;
            Slots = slots;
        }

        /// <summary>Runs of neighbouring slots sharing a prefix, in slot order; may be empty.</summary>
        public IReadOnlyList<PrefixGroup> Groups { get; }

        /// <summary>Each slot's buttons after its group's prefix (at least one); null for unmapped slots.</summary>
        public IReadOnlyList<IReadOnlyList<string>> Slots { get; }

        /// <summary>
        /// Groups neighbouring slots that start with the same button and moves their shared leading
        /// buttons into the group. Unmapped slots split groups; one-button slots aren't grouped.
        /// </summary>
        /// <param name="slots">Each slot's buttons (<see cref="SlotLabel.Buttons"/>); null for
        /// unmapped slots.</param>
        /// <returns>The groups and each slot's remaining buttons. Every mapped slot keeps at least
        /// one button.</returns>
        public static BarLabels Split(IReadOnlyList<IReadOnlyList<string>> slots)
        {
            var groups = new List<PrefixGroup>();
            var remainders = new IReadOnlyList<string>[slots.Count];

            int start = 0;
            while (start < slots.Count)
            {
                // Unmapped and one-button slots have nothing to share
                if (!CanGroup(slots[start]))
                {
                    remainders[start] = slots[start];
                    start++;
                    continue;
                }

                string prefixButton = KeyMap.Normalize(slots[start][0]);
                int end = start + 1;
                while (end < slots.Count && CanGroup(slots[end]) && KeyMap.Normalize(slots[end][0]) == prefixButton)
                {
                    end++;
                }

                var run = slots.Skip(start).Take(end - start).ToList();
                int shared = SharedPrefixLength(run);
                groups.Add(new PrefixGroup(start, run.Count, run[0].Take(shared).ToArray()));
                for (int slot = start; slot < end; slot++)
                {
                    remainders[slot] = slots[slot].Skip(shared).ToArray();
                }

                start = end;
            }

            return new BarLabels(groups, remainders);
        }

        /// <summary>
        /// Whether a slot can join a group: mapped, with a prefix button and a button of its own.
        /// </summary>
        /// <param name="slot">The slot's buttons, or null.</param>
        /// <returns>True for slots with two or more buttons.</returns>
        private static bool CanGroup(IReadOnlyList<string> slot)
        {
            return slot != null && slot.Count >= 2;
        }

        /// <summary>
        /// Length of the leading buttons all slots in a run share, capped so the shortest slot keeps
        /// one button of its own.
        /// </summary>
        /// <param name="run">Slots that all start with the same button.</param>
        /// <returns>At least 1 (the run's common first button).</returns>
        private static int SharedPrefixLength(IReadOnlyList<IReadOnlyList<string>> run)
        {
            int shared = run.Min(slot => slot.Count) - 1;
            IReadOnlyList<string> first = run[0];
            foreach (IReadOnlyList<string> slot in run)
            {
                int same = 0;
                while (same < shared && KeyMap.Normalize(slot[same]) == KeyMap.Normalize(first[same]))
                {
                    same++;
                }

                shared = same;
            }

            return Math.Max(shared, 1);
        }
    }
}
