// ------------------------------------------------------------------------------------------------
// SlotLabelTracker.cs: remembers each bar's last label state and decides what to do next (F3).
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core
{
    /// <summary>
    /// Each bar's state when its labels were last handled, keyed by bar name, with
    /// <see cref="SlotLabelRefresh.Decide"/> for the next update. A redraw (<see cref="Invalidate"/>)
    /// forces each bar's next update but keeps its last state: that state is how a bar knows our
    /// labels are on screen and must be restored when glyphs turn off.
    /// </summary>
    public sealed class SlotLabelTracker
    {
        /// <summary>
        /// Bar name to its state at the last update. Keyed by name, not object, so it stays as small
        /// as the number of bars even though bars are recreated on every world load.
        /// </summary>
        private readonly Dictionary<string, SlotBarState> m_states = new ();

        /// <summary>Bars whose next update is forced (filled by <see cref="Invalidate"/>).</summary>
        private readonly HashSet<string> m_forced = new ();

        /// <summary>
        /// Records a bar's state and decides what to do with its labels. The state is recorded
        /// before returning, so a restore that re-enters the update sees it.
        /// </summary>
        /// <param name="bar">The bar's name.</param>
        /// <param name="current">The bar's slots and glyph state now.</param>
        /// <param name="force">Valheim just rewrote the labels, so redo ours.</param>
        /// <returns>Skip, Write or Restore.</returns>
        public SlotLabelAction Next(string bar, SlotBarState current, bool force)
        {
            SlotBarState? last = m_states.TryGetValue(bar, out SlotBarState previous) ? previous : null;
            m_states[bar] = current;

            // Remove is cheap on an empty set: this runs every frame per bar
            bool forced = m_forced.Remove(bar) || force;
            return SlotLabelRefresh.Decide(last, current, forced);
        }

        /// <summary>
        /// Forces every known bar's next update (a setting that changes the labels changed), without
        /// forgetting which bars show our labels.
        /// </summary>
        public void Invalidate()
        {
            m_forced.UnionWith(m_states.Keys);
        }

        /// <summary>
        /// Whether a bar showed our labels at its last update.
        /// </summary>
        /// <param name="bar">The bar's name.</param>
        /// <returns>True if its labels need restoring when glyphs go away.</returns>
        public bool Labelled(string bar)
        {
            return m_states.TryGetValue(bar, out SlotBarState state) && state.ShowGlyphs;
        }

        /// <summary>
        /// Forgets every bar, after the plugin put Valheim's labels back itself (on unload).
        /// </summary>
        public void Clear()
        {
            m_states.Clear();
            m_forced.Clear();
        }
    }
}
