// ------------------------------------------------------------------------------------------------
// PrefixLabels.cs: draws a group prefix and a line beside each group of slots (F3).
// ------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
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
    /// around anything else (<see cref="GroupShapes"/>). Each prefix sits on a rounded, translucent
    /// backing (config General.PrefixBackground); for a row it spans the prefix and its line.
    /// Everything lives under one container object per set of slots, rebuilt on every write and
    /// removed when glyphs turn off.
    /// </summary>
    // TODO(F3): make the side configurable if it collides with other HUD mods.
    internal sealed class PrefixLabels
    {
        /// <summary>Default name of the container object added next to the slots.</summary>
        internal const string ContainerName = "cgf_PrefixLabels";

        /// <summary>Space between the prefix and the line, and below the slots, in UI units.</summary>
        private const float Gap = 2f;

        /// <summary>
        /// Space between the slots and a prefix above them: the same as below, so the backing meets
        /// the slots and reads as part of the bar. (8 was tried before backings existed, when 2 looked
        /// cramped; with backings the attached look was preferred, checked in game 2026-11-07.)
        /// </summary>
        private const float AboveGap = Gap;

        /// <summary>Line thickness, in UI units.</summary>
        private const float LineThickness = 2f;

        /// <summary>Shortest line worth drawing; a lone slot's prefix may already cover it.</summary>
        private const float MinLineLength = 6f;

        /// <summary>Space between a backing's edge and what it holds, in UI units.</summary>
        private const float BackingPadding = 2f;

        /// <summary>Rounded-square sprite stretched behind each prefix (<see cref="RoundedSprite"/>).</summary>
        private readonly Sprite m_backingSprite;

        /// <summary>
        /// Config General.PrefixBackground: the backing's colour and transparency. Read on each draw;
        /// the plugin redraws the bars when it changes.
        /// </summary>
        private readonly ConfigEntry<Color> m_backingColor;

        /// <summary>
        /// Creates the drawer.
        /// </summary>
        /// <param name="backingSprite">The rounded backing sprite, made once at startup.</param>
        /// <param name="backingColor">Config backing colour; read live.</param>
        internal PrefixLabels(Sprite backingSprite, ConfigEntry<Color> backingColor)
        {
            m_backingSprite = backingSprite ? backingSprite : throw new System.ArgumentNullException(nameof(backingSprite));
            m_backingColor = backingColor ?? throw new System.ArgumentNullException(nameof(backingColor));
        }

        /// <summary>Whether the rounded backing sprite is still loaded (checked by the self-test).</summary>
        internal bool BackingSpriteLoaded => m_backingSprite && m_backingSprite.texture;

        /// <summary>
        /// Replaces the prefixes beside a set of slots.
        /// </summary>
        /// <param name="parent">The slots' parent; the container is added here.</param>
        /// <param name="prefixes">One prefix per group; empty removes them all.</param>
        /// <param name="style">A slot label to copy the font, size and neutral colour from.</param>
        /// <param name="side">Above or below the slots.</param>
        /// <param name="containerName">Container name; distinct per set of slots sharing a parent.</param>
        internal void Show(Transform parent, IReadOnlyList<PrefixLabel> prefixes, TMP_Text style, PrefixSide side,
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
                Rect bounds = Bounds(parent, prefix.Slots);
                Color color = prefix.Color != null && ColorUtility.TryParseHtmlString(prefix.Color, out Color parsed)
                    ? parsed
                    : style.color;

                // Backing first, so it's drawn behind; sized once the text is measured
                Image backing = CreateImage(container, "Backing", m_backingColor.Value);
                backing.sprite = m_backingSprite;
                backing.type = Image.Type.Sliced;

                // The backing's left edge is flush with the first slot; padding goes inside, around the
                // text and line (padding outside showed on one side when edges differed by a unit)
                TextMeshProUGUI text = CreateText(container, style, prefix.Text, color);
                Vector2 size = text.GetPreferredValues(prefix.Text);
                float textLeft = bounds.xMin + BackingPadding;
                float y = side == PrefixSide.Above ? bounds.yMax + AboveGap : bounds.yMin - Gap;
                Place(text.rectTransform, new Vector2(0f, side == PrefixSide.Above ? 0f : 1f), size,
                    new Vector2(textLeft, y));
                float textBottom = side == PrefixSide.Above ? y : y - size.y;
                float backingRight = textLeft + size.x + BackingPadding;

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
                        // Across, from the prefix to the last slot, on one backing flush with both end slots
                        float lineStart = textLeft + size.x + Gap * 2f;
                        float lineEnd = bounds.xMax - BackingPadding;
                        if (lineEnd - lineStart >= MinLineLength)
                        {
                            float lineY = textBottom + size.y / 2f;
                            CreateRect(container, color, Rect.MinMaxRect(lineStart, lineY - LineThickness / 2f,
                                lineEnd, lineY + LineThickness / 2f));
                            backingRight = bounds.xMax;
                        }

                        break;
                }

                Place(backing.rectTransform, Vector2.zero,
                    new Vector2(backingRight - bounds.xMin, size.y + BackingPadding * 2f),
                    new Vector2(bounds.xMin, textBottom - BackingPadding));
            }
        }

        /// <summary>
        /// Removes the prefixes beside a set of slots, if any.
        /// </summary>
        /// <param name="parent">The slots' parent.</param>
        /// <param name="containerName">The name given to <see cref="Show"/>.</param>
        internal void Clear(Transform parent, string containerName = ContainerName)
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
        internal IReadOnlyList<string> Texts(Transform parent, string containerName = ContainerName)
        {
            Transform container = parent.Find(containerName);
            return container
                ? container.GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray()
                : new string[0];
        }

        /// <summary>
        /// The area the slots' visible backgrounds cover, in their parent's local space.
        /// </summary>
        /// <param name="parent">The slots' parent.</param>
        /// <param name="slots">Slots with that parent.</param>
        /// <returns>The bounding rectangle.</returns>
        private Rect Bounds(Transform parent, IReadOnlyList<RectTransform> slots)
        {
            var corners = new Vector3[4];
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (RectTransform slot in slots)
            {
                VisibleBackground(slot).GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = parent.InverseTransformPoint(corner);
                    xMin = Mathf.Min(xMin, local.x);
                    xMax = Mathf.Max(xMax, local.x);
                    yMin = Mathf.Min(yMin, local.y);
                    yMax = Mathf.Max(yMax, local.y);
                }
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>
        /// The rect of a slot's visible square: the largest active image on the slot or its direct
        /// children, ignoring state overlays. The slot's own layout box can differ from what's drawn
        /// (the backings started a few units right of the first slot in game, 2026-11-07).
        /// </summary>
        /// <param name="slot">A hotbar slot or inventory cell.</param>
        /// <returns>The background's rect, or the slot's own if it has no image.</returns>
        private RectTransform VisibleBackground(RectTransform slot)
        {
            RectTransform best = slot;
            float bestArea = Area(slot, slot.GetComponent<Image>());
            foreach (Transform child in slot)
            {
                if (!child.gameObject.activeSelf || IsOverlay(child.name))
                {
                    continue;
                }

                var rect = (RectTransform)child;
                float area = Area(rect, child.GetComponent<Image>());
                if (area > bestArea)
                {
                    best = rect;
                    bestArea = area;
                }
            }

            return best;
        }

        /// <summary>
        /// Whether a slot child is a state overlay (selection, equipped, queued frames) rather than
        /// the slot's background; those come and go and may stick out.
        /// </summary>
        /// <param name="name">The child's name.</param>
        /// <returns>True for Valheim's overlay children.</returns>
        private bool IsOverlay(string name)
        {
            return name == "selected" || name == "equiped" || name == "queued";
        }

        /// <summary>
        /// On-screen area of an image, for picking the largest.
        /// </summary>
        /// <param name="rect">The image's rect.</param>
        /// <param name="image">The image; may be null or disabled.</param>
        /// <returns>Its area in world units, or -1 when there's no enabled image.</returns>
        private float Area(RectTransform rect, Image image)
        {
            if (!image || !image.enabled)
            {
                return -1f;
            }

            Vector3 scale = rect.lossyScale;
            return Mathf.Abs(rect.rect.width * scale.x * rect.rect.height * scale.y);
        }

        /// <summary>
        /// Adds a prefix text styled like the slot labels.
        /// </summary>
        /// <param name="container">The prefixes' container.</param>
        /// <param name="style">A slot label to copy the font and size from.</param>
        /// <param name="value">The text.</param>
        /// <param name="color">The text colour.</param>
        /// <returns>The new text.</returns>
        private TextMeshProUGUI CreateText(Transform container, TMP_Text style, string value, Color color)
        {
            // Built inactive: TMP initialises on activation, and would otherwise look for its default
            // font (LiberationSans, not in Valheim) and log a warning before ours is assigned
            var prefixObject = new GameObject("Prefix", typeof(RectTransform));
            prefixObject.SetActive(false);
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

            prefixObject.SetActive(true);
            return text;
        }

        /// <summary>
        /// Draws a thin outline just outside an area.
        /// </summary>
        /// <param name="container">The prefixes' container.</param>
        /// <param name="color">The line colour.</param>
        /// <param name="area">The area to outline, in the parent's local space.</param>
        private void CreateOutline(Transform container, Color color, Rect area)
        {
            float xMin = area.xMin - Gap - LineThickness, xMax = area.xMax + Gap + LineThickness;
            float yMin = area.yMin - Gap - LineThickness, yMax = area.yMax + Gap + LineThickness;
            CreateRect(container, color, Rect.MinMaxRect(xMin, yMax - LineThickness, xMax, yMax));
            CreateRect(container, color, Rect.MinMaxRect(xMin, yMin, xMax, yMin + LineThickness));
            CreateRect(container, color, Rect.MinMaxRect(xMin, yMin, xMin + LineThickness, yMax));
            CreateRect(container, color, Rect.MinMaxRect(xMax - LineThickness, yMin, xMax, yMax));
        }

        /// <summary>
        /// Adds a solid rectangle (a line segment).
        /// </summary>
        /// <param name="container">The prefixes' container.</param>
        /// <param name="color">The colour.</param>
        /// <param name="area">Where it goes, in the parent's local space.</param>
        private void CreateRect(Transform container, Color color, Rect area)
        {
            Image line = CreateImage(container, "Line", color);
            Place(line.rectTransform, Vector2.zero, area.size, area.min);
        }

        /// <summary>
        /// Adds an unplaced image that ignores clicks.
        /// </summary>
        /// <param name="container">The prefixes' container.</param>
        /// <param name="name">Object name, for UnityExplorer.</param>
        /// <param name="color">The image colour (tints its sprite, if any).</param>
        /// <returns>The new image.</returns>
        private Image CreateImage(Transform container, string name, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform));
            imageObject.transform.SetParent(container, worldPositionStays: false);

            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// Sizes and positions a rect by its pivot, in the parent's local space.
        /// </summary>
        /// <param name="rect">The rect to place.</param>
        /// <param name="pivot">Which point of the rect sits at the position.</param>
        /// <param name="size">The rect's size.</param>
        /// <param name="position">Where the pivot goes.</param>
        private void Place(RectTransform rect, Vector2 pivot, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.localPosition = position;
        }
    }
}
