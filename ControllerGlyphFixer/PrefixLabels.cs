// ------------------------------------------------------------------------------------------------
// PrefixLabels.cs: draws a group prefix and a line beside each group of slots (F3).
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using ControllerGlyphFixer.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Which side of a group of slots its prefix goes on.
    /// </summary>
    internal enum PrefixSide
    {
        /// <summary>Above the slots (the hotbar, with free space at the top of the screen; the inventory).</summary>
        Above,

        /// <summary>Below the slots (AzuEPI's quick-slot bar, which sits right under the hotbar).</summary>
        Below,
    }

    /// <summary>
    /// One group's prefix label: its text, its colour, and the slots its line spans.
    /// </summary>
    internal readonly struct PrefixLabel
    {
        /// <summary>
        /// Creates a prefix label.
        /// </summary>
        /// <param name="text">Prefix text (the formatted prefix buttons).</param>
        /// <param name="color">Colour as #RRGGBB or #RRGGBBAA; null for the slot-label colour.</param>
        /// <param name="slots">The group's slots on screen, in order; at least one.</param>
        internal PrefixLabel(string text, string color, IReadOnlyList<RectTransform> slots)
        {
            Text = text;
            Color = color;
            Slots = slots;
        }

        /// <summary>Prefix text (the formatted prefix buttons).</summary>
        internal string Text { get; }

        /// <summary>Colour as #RRGGBB or #RRGGBBAA; null for the slot-label colour.</summary>
        internal string Color { get; }

        /// <summary>The group's slots on screen, in order.</summary>
        internal IReadOnlyList<RectTransform> Slots { get; }
    }

    /// <summary>
    /// Draws each group's prefix at the group's top-left (or bottom-left, below the slots) and a line
    /// that shows which slots it covers: across a row, down a column's left side, or an outline
    /// around anything else (<see cref="GroupShapes"/>). Everything lives under one container object
    /// per set of slots, rebuilt on every write and removed when glyphs turn off.
    /// </summary>
    // TODO(F3): make the side configurable if it collides with other HUD mods.
    internal static class PrefixLabels
    {
        /// <summary>Default name of the container object added next to the slots.</summary>
        internal const string ContainerName = "cgf_PrefixLabels";

        /// <summary>Space between the prefix and the line, and below the slots, in UI units.</summary>
        private const float Gap = 2f;

        /// <summary>
        /// Space between the slots and a prefix above them. Larger than <see cref="Gap"/>: with 2 the
        /// hotbar's prefix looked cramped against the slots (checked in game 2026-11-07).
        /// </summary>
        private const float AboveGap = 8f;

        /// <summary>Line thickness, in UI units.</summary>
        private const float LineThickness = 2f;

        /// <summary>Shortest line worth drawing; a lone slot's prefix may already cover it.</summary>
        private const float MinLineLength = 6f;

        /// <summary>Space between a prefix's text and the edge of its backing, in UI units.</summary>
        private const float BackingPadding = 2f;

        /// <summary>
        /// Translucent black behind each prefix, so it stays readable over borders and highlights
        /// (the inventory panel's gamepad highlight ran through it; checked in game 2026-11-07).
        /// </summary>
        private static readonly Color BackingColor = new (0f, 0f, 0f, 0.55f);

        /// <summary>
        /// Replaces the prefixes beside a set of slots.
        /// </summary>
        /// <param name="parent">The slots' parent; the container is added here.</param>
        /// <param name="prefixes">One prefix per group; empty removes them all.</param>
        /// <param name="style">A slot label to copy the font, size and neutral colour from.</param>
        /// <param name="side">Above or below the slots.</param>
        /// <param name="containerName">Container name; distinct per set of slots sharing a parent.</param>
        internal static void Show(Transform parent, IReadOnlyList<PrefixLabel> prefixes, TMP_Text style, PrefixSide side,
            string containerName = ContainerName)
        {
            Clear(parent, containerName);
            if (prefixes.Count == 0 || !style)
            {
                return;
            }

            var container = new GameObject(containerName, typeof(RectTransform)).GetComponent<RectTransform>();
            container.SetParent(parent, worldPositionStays: false);
            container.localPosition = Vector3.zero;

            foreach (PrefixLabel prefix in prefixes)
            {
                Rect bounds = Bounds(prefix.Slots);
                Color color = prefix.Color != null && ColorUtility.TryParseHtmlString(prefix.Color, out Color parsed)
                    ? parsed
                    : style.color;

                TextMeshProUGUI text = CreateText(container, style, prefix.Text, color);
                Vector2 size = text.GetPreferredValues(prefix.Text);
                float y = side == PrefixSide.Above ? bounds.yMax + AboveGap : bounds.yMin - Gap;
                Place(text.rectTransform, new Vector2(0f, side == PrefixSide.Above ? 0f : 1f), size,
                    new Vector2(bounds.xMin, y));

                // Backing behind the text: created after it (to know its size), then moved before it
                float textBottom = side == PrefixSide.Above ? y : y - size.y;
                RectTransform backing = CreateRect(container, BackingColor, Rect.MinMaxRect(
                    bounds.xMin - BackingPadding, textBottom - BackingPadding,
                    bounds.xMin + size.x + BackingPadding, textBottom + size.y + BackingPadding));
                backing.name = "Backing";
                backing.SetSiblingIndex(text.transform.GetSiblingIndex());

                GroupShape shape = GroupShapes.Of(prefix.Slots.Select(slot => (slot.localPosition.x, slot.localPosition.y)).ToList());
                switch (shape)
                {
                    case GroupShape.Column:
                        // Down the column's left side, from its top slot to its bottom one
                        float x = bounds.xMin - Gap - LineThickness;
                        CreateRect(container, color, Rect.MinMaxRect(x, bounds.yMin, x + LineThickness, bounds.yMax));
                        break;
                    case GroupShape.Area:
                        CreateOutline(container, color, bounds);
                        break;
                    default:
                        // Across, from the prefix to the last slot
                        float lineStart = bounds.xMin + size.x + BackingPadding + Gap * 2f;
                        if (bounds.xMax - lineStart >= MinLineLength)
                        {
                            float lineY = side == PrefixSide.Above ? y + size.y / 2f : y - size.y / 2f;
                            CreateRect(container, color, Rect.MinMaxRect(lineStart, lineY - LineThickness / 2f,
                                bounds.xMax, lineY + LineThickness / 2f));
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// Removes the prefixes beside a set of slots, if any.
        /// </summary>
        /// <param name="parent">The slots' parent.</param>
        /// <param name="containerName">The name given to <see cref="Show"/>.</param>
        internal static void Clear(Transform parent, string containerName = ContainerName)
        {
            Transform container = parent.Find(containerName);
            if (container)
            {
                // Detached first: Destroy only takes effect at the end of the frame
                container.SetParent(null, worldPositionStays: false);
                Object.Destroy(container.gameObject);
            }
        }

        /// <summary>
        /// Returns the texts of the prefixes beside a set of slots, in order.
        /// </summary>
        /// <param name="parent">The slots' parent.</param>
        /// <param name="containerName">The name given to <see cref="Show"/>.</param>
        /// <returns>The prefix texts; empty if there are none.</returns>
        internal static IReadOnlyList<string> Texts(Transform parent, string containerName = ContainerName)
        {
            Transform container = parent.Find(containerName);
            return container
                ? container.GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray()
                : new string[0];
        }

        /// <summary>
        /// The area the slots cover, in their parent's local space.
        /// </summary>
        /// <param name="slots">Slots with the same parent.</param>
        /// <returns>The bounding rectangle.</returns>
        private static Rect Bounds(IReadOnlyList<RectTransform> slots)
        {
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (RectTransform slot in slots)
            {
                Vector3 origin = slot.localPosition;
                Vector3 scale = slot.localScale;
                Rect rect = slot.rect;
                xMin = Mathf.Min(xMin, origin.x + rect.xMin * scale.x);
                xMax = Mathf.Max(xMax, origin.x + rect.xMax * scale.x);
                yMin = Mathf.Min(yMin, origin.y + rect.yMin * scale.y);
                yMax = Mathf.Max(yMax, origin.y + rect.yMax * scale.y);
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>
        /// Adds a prefix text styled like the slot labels.
        /// </summary>
        /// <param name="container">The prefixes' container.</param>
        /// <param name="style">A slot label to copy the font and size from.</param>
        /// <param name="value">The text.</param>
        /// <param name="color">The text colour.</param>
        /// <returns>The new text.</returns>
        private static TextMeshProUGUI CreateText(Transform container, TMP_Text style, string value, Color color)
        {
            var prefixObject = new GameObject("Prefix", typeof(RectTransform));
            prefixObject.transform.SetParent(container, worldPositionStays: false);

            TextMeshProUGUI text = prefixObject.AddComponent<TextMeshProUGUI>();
            text.font = style.font;
            text.fontSharedMaterial = style.fontSharedMaterial;
            text.fontSize = style.fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.BottomLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        /// <summary>
        /// Draws a thin outline just outside an area.
        /// </summary>
        /// <param name="container">The prefixes' container.</param>
        /// <param name="color">The line colour.</param>
        /// <param name="area">The area to outline, in the parent's local space.</param>
        private static void CreateOutline(Transform container, Color color, Rect area)
        {
            float xMin = area.xMin - Gap - LineThickness, xMax = area.xMax + Gap + LineThickness;
            float yMin = area.yMin - Gap - LineThickness, yMax = area.yMax + Gap + LineThickness;
            CreateRect(container, color, Rect.MinMaxRect(xMin, yMax - LineThickness, xMax, yMax));
            CreateRect(container, color, Rect.MinMaxRect(xMin, yMin, xMax, yMin + LineThickness));
            CreateRect(container, color, Rect.MinMaxRect(xMin, yMin, xMin + LineThickness, yMax));
            CreateRect(container, color, Rect.MinMaxRect(xMax - LineThickness, yMin, xMax, yMax));
        }

        /// <summary>
        /// Adds a solid rectangle (a line segment or a backing).
        /// </summary>
        /// <param name="container">The prefixes' container.</param>
        /// <param name="color">The colour.</param>
        /// <param name="area">Where it goes, in the parent's local space.</param>
        /// <returns>The rectangle's rect.</returns>
        private static RectTransform CreateRect(Transform container, Color color, Rect area)
        {
            var lineObject = new GameObject("Line", typeof(RectTransform));
            lineObject.transform.SetParent(container, worldPositionStays: false);

            Image line = lineObject.AddComponent<Image>();
            line.color = color;
            line.raycastTarget = false;
            Place(line.rectTransform, Vector2.zero, area.size, area.min);
            return line.rectTransform;
        }

        /// <summary>
        /// Sizes and positions a rect by its pivot, in the parent's local space.
        /// </summary>
        /// <param name="rect">The rect to place.</param>
        /// <param name="pivot">Which point of the rect sits at the position.</param>
        /// <param name="size">The rect's size.</param>
        /// <param name="position">Where the pivot goes.</param>
        private static void Place(RectTransform rect, Vector2 pivot, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.localPosition = position;
        }
    }
}
