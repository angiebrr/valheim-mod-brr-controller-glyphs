// ------------------------------------------------------------------------------------------------
// PrefixLabels.cs: draws a group prefix and a line beside each group of slots (F3).
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Which side of a group of slots its prefix and line go on.
    /// </summary>
    internal enum PrefixSide
    {
        /// <summary>Above the slots (Valheim's hotbar, with free space at the top of the screen).</summary>
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
    /// Draws each group's prefix at the group's left end, above or below its slots, and a line from
    /// the prefix to the group's last slot. Everything lives under one container object, which is
    /// rebuilt on every write and removed when glyphs turn off.
    /// </summary>
    // TODO(F3): check sizes and spacing in game; make the side configurable if it collides with other HUD mods.
    internal static class PrefixLabels
    {
        /// <summary>Name of the container object added next to the slots.</summary>
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

        /// <summary>
        /// Replaces the prefixes beside a set of slots.
        /// </summary>
        /// <param name="parent">The slots' parent (the bar); the container is added here.</param>
        /// <param name="prefixes">One prefix per group; empty removes them all.</param>
        /// <param name="style">A slot label to copy the font, size and neutral colour from.</param>
        /// <param name="side">Above or below the slots.</param>
        internal static void Show(Transform parent, IReadOnlyList<PrefixLabel> prefixes, TMP_Text style, PrefixSide side)
        {
            Clear(parent);
            if (prefixes.Count == 0 || !style)
            {
                return;
            }

            var container = new GameObject(ContainerName, typeof(RectTransform)).GetComponent<RectTransform>();
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

                float lineStart = bounds.xMin + size.x + Gap * 2f;
                if (bounds.xMax - lineStart >= MinLineLength)
                {
                    float lineY = side == PrefixSide.Above ? y + size.y / 2f : y - size.y / 2f;
                    CreateLine(container, color, lineStart, bounds.xMax, lineY);
                }
            }
        }

        /// <summary>
        /// Removes the prefixes beside a set of slots, if any.
        /// </summary>
        /// <param name="parent">The slots' parent.</param>
        internal static void Clear(Transform parent)
        {
            Transform container = parent.Find(ContainerName);
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
        /// <returns>The prefix texts; empty if there are none.</returns>
        internal static IReadOnlyList<string> Texts(Transform parent)
        {
            Transform container = parent.Find(ContainerName);
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
        /// Adds a horizontal line.
        /// </summary>
        /// <param name="container">The prefixes' container.</param>
        /// <param name="color">The line colour.</param>
        /// <param name="xStart">Left end, in the parent's local space.</param>
        /// <param name="xEnd">Right end.</param>
        /// <param name="y">Vertical centre.</param>
        private static void CreateLine(Transform container, Color color, float xStart, float xEnd, float y)
        {
            var lineObject = new GameObject("Line", typeof(RectTransform));
            lineObject.transform.SetParent(container, worldPositionStays: false);

            Image line = lineObject.AddComponent<Image>();
            line.color = color;
            line.raycastTarget = false;
            Place(line.rectTransform, new Vector2(0f, 0.5f), new Vector2(xEnd - xStart, LineThickness), new Vector2(xStart, y));
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
