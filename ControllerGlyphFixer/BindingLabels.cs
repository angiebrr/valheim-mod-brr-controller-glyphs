// ------------------------------------------------------------------------------------------------
// BindingLabels.cs: reads and writes a slot's "binding" label, shared by the label writers (F3).
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;
using TMPro;
using UnityEngine;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Every hotbar slot and inventory cell has a "binding" child with the text label Valheim (or
    /// AzuEPI) writes the key on. These helpers find, read and set it, and build the refresh state
    /// the writers compare each frame.
    /// </summary>
    internal static class BindingLabels
    {
        /// <summary>Name of the label child in Valheim's slot and cell prefabs.</summary>
        private const string LabelName = "binding";

        /// <summary>
        /// Finds a slot's label.
        /// </summary>
        /// <param name="slot">The slot's GameObject; may be null or destroyed.</param>
        /// <returns>The label, or null if the slot is gone or has none.</returns>
        internal static TMP_Text Find(GameObject slot)
        {
            Transform label = slot ? slot.transform.Find(LabelName) : null;
            return label ? label.GetComponent<TMP_Text>() : null;
        }

        /// <summary>
        /// Reads a slot's label text.
        /// </summary>
        /// <param name="slot">The slot's GameObject; may be null or destroyed.</param>
        /// <returns>The text, or null if the slot is gone or has no label.</returns>
        internal static string Text(GameObject slot)
        {
            TMP_Text label = Find(slot);
            return label ? label.text : null;
        }

        /// <summary>
        /// Sets a slot's label text, only when it differs (setting text rebuilds the mesh).
        /// </summary>
        /// <param name="slot">The slot's GameObject; may be null or destroyed.</param>
        /// <param name="text">The new text; null leaves the label alone.</param>
        internal static void Set(GameObject slot, string text)
        {
            TMP_Text label = text == null ? null : Find(slot);
            if (label && label.text != text)
            {
                label.text = text;
            }
        }

        /// <summary>
        /// Builds the state a writer compares with last time (<see cref="SlotLabelRefresh"/>).
        /// </summary>
        /// <param name="slotCount">Number of slots or cells.</param>
        /// <param name="firstSlot">The first one, whose ID changes when they're rebuilt; may be null.</param>
        /// <param name="showGlyphs">Whether labels should show mapped buttons.</param>
        /// <returns>The state.</returns>
        internal static SlotBarState State(int slotCount, Object firstSlot, bool showGlyphs)
        {
            return new SlotBarState(slotCount, firstSlot ? firstSlot.GetInstanceID() : 0, showGlyphs);
        }
    }
}
