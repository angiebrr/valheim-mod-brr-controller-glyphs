// ------------------------------------------------------------------------------------------------
// SlotLabelRefresh.cs: decides when a hotbar's slot labels are rewritten or restored (F3).
// ------------------------------------------------------------------------------------------------

using System;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// What to do with a bar's slot labels this time.
    /// </summary>
    public enum SlotLabelAction
    {
        /// <summary>Leave the labels as they are.</summary>
        Skip,

        /// <summary>Write the mapped-button labels.</summary>
        Write,

        /// <summary>Glyphs just turned off: let Valheim write its own labels again.</summary>
        Restore,
    }

    /// <summary>
    /// A hotbar's slots and glyph state when its labels were last handled.
    /// </summary>
    public readonly struct SlotBarState : IEquatable<SlotBarState>
    {
        /// <summary>
        /// Creates a state.
        /// </summary>
        /// <param name="slotCount">Number of slots on the bar.</param>
        /// <param name="firstSlotId">Unity instance ID of the first slot's object (0 if none).</param>
        /// <param name="showGlyphs">Whether slot labels should show mapped buttons.</param>
        public SlotBarState(int slotCount, int firstSlotId, bool showGlyphs)
        {
            SlotCount = slotCount;
            FirstSlotId = firstSlotId;
            ShowGlyphs = showGlyphs;
        }

        /// <summary>Number of slots on the bar.</summary>
        public int SlotCount { get; }

        /// <summary>
        /// Unity instance ID of the first slot's object. Changes when the slots are rebuilt, even
        /// with the same count (AzuEPI rebuilds its quick slots after Settings closes).
        /// </summary>
        public int FirstSlotId { get; }

        /// <summary>Whether slot labels should show mapped buttons.</summary>
        public bool ShowGlyphs { get; }

        /// <summary>Compares two states by value.</summary>
        /// <param name="left">First state.</param>
        /// <param name="right">Second state.</param>
        /// <returns>True if every value matches.</returns>
        public static bool operator ==(SlotBarState left, SlotBarState right) => left.Equals(right);

        /// <summary>Compares two states by value.</summary>
        /// <param name="left">First state.</param>
        /// <param name="right">Second state.</param>
        /// <returns>True if any value differs.</returns>
        public static bool operator !=(SlotBarState left, SlotBarState right) => !left.Equals(right);

        /// <summary>Compares with another state by value.</summary>
        /// <param name="other">The other state.</param>
        /// <returns>True if every value matches.</returns>
        public bool Equals(SlotBarState other)
        {
            return SlotCount == other.SlotCount && FirstSlotId == other.FirstSlotId && ShowGlyphs == other.ShowGlyphs;
        }

        /// <summary>Compares with any object by value.</summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True if it's a state with the same values.</returns>
        public override bool Equals(object obj) => obj is SlotBarState other && Equals(other);

        /// <summary>Hash of all values.</summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (SlotCount * 397 ^ FirstSlotId) * 2 + (ShowGlyphs ? 1 : 0);
            }
        }
    }

    /// <summary>
    /// Decides when a hotbar's slot labels need writing. UpdateIcons runs every frame, so labels
    /// are only touched when the slots, the glyph state, or Valheim's own labels changed.
    /// </summary>
    public static class SlotLabelRefresh
    {
        /// <summary>
        /// Decides what to do with a bar's labels.
        /// </summary>
        /// <param name="last">State when the bar was last handled; null the first time.</param>
        /// <param name="current">State now.</param>
        /// <param name="force">Valheim just rewrote the labels (ToggleBindingHint, OnEnable).</param>
        /// <returns>Skip, Write or Restore.</returns>
        public static SlotLabelAction Decide(SlotBarState? last, SlotBarState current, bool force)
        {
            bool unchanged = last.HasValue && last.Value == current;
            if (unchanged && !force)
            {
                return SlotLabelAction.Skip;
            }

            if (current.ShowGlyphs)
            {
                return SlotLabelAction.Write;
            }

            // Restore once, on the change; Valheim's labels stay put after that
            bool glyphsJustTurnedOff = last.HasValue && last.Value.ShowGlyphs;
            return glyphsJustTurnedOff ? SlotLabelAction.Restore : SlotLabelAction.Skip;
        }
    }
}
