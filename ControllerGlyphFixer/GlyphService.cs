// ------------------------------------------------------------------------------------------------
// GlyphService.cs: reads game state for the Core glyph decisions (the thin shell around Core).
// ------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ControllerGlyphFixer.Core;
using UnityEngine.InputSystem;
using KeyCode = UnityEngine.KeyCode;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer
{
    /// <summary>
    /// Reads game state for the Core decisions. All logic lives in Core; keep this thin.
    /// </summary>
    internal sealed class GlyphService
    {
        /// <summary>The loaded key mapping.</summary>
        private readonly KeyMap m_keyMap;

        /// <summary>Config General.Enabled: master switch.</summary>
        private readonly ConfigEntry<bool> m_enabled;

        /// <summary>Config General.KeyPrompts: controller glyphs in $KEY_ prompts (F1 + F2).</summary>
        private readonly ConfigEntry<bool> m_keyPrompts;

        /// <summary>Config General.HotbarLabels: mapped buttons on hotbar and quick slots (F3).</summary>
        private readonly ConfigEntry<bool> m_hotbarLabels;

        /// <summary>Config General.PrefixColors: how group prefixes and lines are coloured (F3).</summary>
        private readonly ConfigEntry<PrefixColorMode> m_prefixColors;

        /// <summary>Config General.HoverText: mapped buttons for key names in hover hints (F4).</summary>
        private readonly ConfigEntry<bool> m_hoverText;

        /// <summary>Config General.ButtonGlyphs: Valheim's art or the mod's tried first (F5).</summary>
        private readonly ConfigEntry<ButtonGlyphOrder> m_buttonGlyphs;

        /// <summary>The mod's glyph tag for a normalized button (<see cref="ModGlyphs.Tag"/>); null without the sprite asset.</summary>
        private readonly Func<string, string> m_modGlyph;

        /// <summary>Most answers <see cref="m_keyNames"/> keeps before starting over.</summary>
        private const int MaxCachedKeyNames = 256;

        /// <summary>Answers of <see cref="IsKeyName"/> so far; hover text repeats the same few words.</summary>
        private readonly Dictionary<string, bool> m_keyNames = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates the service.
        /// </summary>
        /// <param name="keyMap">The loaded key mapping (<see cref="KeyMap.Empty"/> if none).</param>
        /// <param name="enabled">Config master switch; read live so changes apply immediately.</param>
        /// <param name="keyPrompts">Config switch for F1 + F2; read live.</param>
        /// <param name="hotbarLabels">Config switch for F3; read live.</param>
        /// <param name="prefixColors">Config colour mode for group prefixes; read live.</param>
        /// <param name="hoverText">Config switch for F4; read live.</param>
        /// <param name="buttonGlyphs">Config art order for mapped buttons (F5); read live.</param>
        /// <param name="modGlyph">The mod's glyph lookup, or null if the sprite asset couldn't be built.</param>
        internal GlyphService(KeyMap keyMap, ConfigEntry<bool> enabled, ConfigEntry<bool> keyPrompts,
            ConfigEntry<bool> hotbarLabels, ConfigEntry<PrefixColorMode> prefixColors, ConfigEntry<bool> hoverText,
            ConfigEntry<ButtonGlyphOrder> buttonGlyphs, Func<string, string> modGlyph)
        {
            m_buttonGlyphs = buttonGlyphs ?? throw new ArgumentNullException(nameof(buttonGlyphs));
            m_modGlyph = modGlyph;
            m_keyMap = keyMap ?? throw new ArgumentNullException(nameof(keyMap));
            m_enabled = enabled ?? throw new ArgumentNullException(nameof(enabled));
            m_keyPrompts = keyPrompts ?? throw new ArgumentNullException(nameof(keyPrompts));
            m_hotbarLabels = hotbarLabels ?? throw new ArgumentNullException(nameof(hotbarLabels));
            m_prefixColors = prefixColors ?? throw new ArgumentNullException(nameof(prefixColors));
            m_hoverText = hoverText ?? throw new ArgumentNullException(nameof(hoverText));

            // Built once: the $KEY_ and hover postfixes pass these on every call, often every frame
            KeyTranslator = TranslateKey;
            JoyLookup = LookupJoy;
            KeyNameCheck = IsKeyName;
        }

        /// <summary>Number of keys in the loaded mapping.</summary>
        internal int MappedKeyCount => m_keyMap.Count;

        /// <summary><see cref="TranslateKey"/> as a delegate, created once.</summary>
        internal Func<string, string> KeyTranslator { get; }

        /// <summary><see cref="LookupJoy"/> as a delegate, created once.</summary>
        internal Func<string, string> JoyLookup { get; }

        /// <summary><see cref="IsKeyName"/> as a delegate, created once.</summary>
        internal Func<string, bool> KeyNameCheck { get; }

        /// <summary>
        /// Whether hover-text hints should show mapped buttons right now (F4): needs both the $KEY_
        /// glyphs (KeyPrompts) and the HoverText switch, since it finishes what F1 starts there.
        /// Computed on each read, like <see cref="ShowKeyPrompts"/>.
        /// </summary>
        internal bool ShowHoverText => ShowGlyphs(m_keyPrompts.Value && m_hoverText.Value);

        /// <summary>
        /// Whether a word is a keyboard key name, as mods write them (Unity KeyCode names such as
        /// "E", "LeftShift", "Keypad1"; any case). Plain numbers aren't: "[5]" is more often a count.
        /// </summary>
        /// <param name="word">A word from a hover hint.</param>
        /// <returns>True for KeyCode names.</returns>
        internal bool IsKeyName(string word)
        {
            if (string.IsNullOrEmpty(word) || char.IsDigit(word[0]))
            {
                return false;
            }

            if (!m_keyNames.TryGetValue(word, out bool isKey))
            {
                // Bounded: bracketed words in hover text are few, but nothing guarantees that
                if (m_keyNames.Count >= MaxCachedKeyNames)
                {
                    m_keyNames.Clear();
                }

                isKey = Enum.TryParse(word, ignoreCase: true, out KeyCode key) && Enum.IsDefined(typeof(KeyCode), key);
                m_keyNames[word] = isKey;
            }

            return isKey;
        }

        /// <summary>
        /// Returns the display string of a controller binding (e.g. "JoyUse").
        /// </summary>
        /// <param name="joyName">Controller binding name.</param>
        /// <returns>Valheim's glyph for it, or "" if it isn't bound (or ZInput isn't ready).</returns>
        internal string LookupJoy(string joyName)
        {
            ZInput input = ZInput.instance;
            return input != null ? input.GetBoundKeyString(joyName, emptyStringOnMissing: true) : "";
        }

        /// <summary>
        /// Whether $KEY_ prompts should show controller glyphs right now (F1 + F2). Computed on each
        /// read, not set at construction, because its inputs change while the game runs (controller
        /// plugged in, Settings opened, config toggled).
        /// </summary>
        // Steam Input exposes a virtual XInput controller ("Xbox Controller"), but it connects after
        // plugins load, so Gamepad.current is null at startup and becomes set later (checked 2026-11-02).
        internal bool ShowKeyPrompts => ShowGlyphs(m_keyPrompts.Value);

        /// <summary>
        /// Whether hotbar and quick-slot labels should show mapped controller buttons right now (F3).
        /// Computed on each read, like <see cref="ShowKeyPrompts"/>.
        /// </summary>
        internal bool ShowHotbarLabels => ShowGlyphs(m_hotbarLabels.Value);

        /// <summary>
        /// Shared live check for every feature: master switch, the feature's switch, and game state.
        /// </summary>
        /// <param name="featureEnabled">The feature's own config switch.</param>
        /// <returns>Result of <see cref="GlyphMode.Decide"/>.</returns>
        private bool ShowGlyphs(bool featureEnabled)
        {
            return GlyphMode.Decide(
                enabled: m_enabled.Value && featureEnabled,
                gamepadEnabled: ZInput.IsGamepadEnabled(),
                gamepadPresent: Gamepad.current != null,
                settingsOpen: Settings.instance != null);
        }

        /// <summary>
        /// Translates a keyboard key through the mapping, with Valheim's or the mod's glyphs per ButtonGlyphs.
        /// </summary>
        /// <param name="key">Key name as Valheim returned it (e.g. "Q", "$button_lshift").</param>
        /// <returns>The mapped display string, or null if the key isn't mapped.</returns>
        internal string TranslateKey(string key)
        {
            return m_keyMap.Translate(key, ValheimGlyph, m_modGlyph, m_buttonGlyphs.Value);
        }

        /// <summary>The current ButtonGlyphs setting, for the self-test.</summary>
        internal ButtonGlyphOrder ButtonGlyphs => m_buttonGlyphs.Value;

        /// <summary>
        /// Returns the buttons pressed for a keyboard key, as written in the mapping file.
        /// </summary>
        /// <param name="key">Key name in any supported spelling.</param>
        /// <returns>The button names, or null if the key isn't mapped.</returns>
        internal IReadOnlyList<string> ButtonsFor(string key)
        {
            return m_keyMap.Buttons(key);
        }

        /// <summary>
        /// Renders buttons with glyphs like <see cref="TranslateKey"/>.
        /// </summary>
        /// <param name="buttons">Button names as written in the mapping file.</param>
        /// <returns>The display string, or null for a null or empty list.</returns>
        internal string FormatButtons(IReadOnlyList<string> buttons)
        {
            return m_keyMap.Format(buttons, ValheimGlyph, m_modGlyph, m_buttonGlyphs.Value);
        }

        /// <summary>
        /// Returns the colour of a group's prefix and line, per the current colour mode.
        /// </summary>
        /// <param name="prefix">The group's prefix buttons.</param>
        /// <returns>A colour (#RRGGBB or #RRGGBBAA), or null for the slot-label colour.</returns>
        internal string PrefixColor(IReadOnlyList<string> prefix)
        {
            return PrefixColors.For(m_prefixColors.Value, prefix, m_keyMap);
        }

        /// <summary>
        /// Returns Valheim's sprite tag for a control name, in the same style the game uses.
        /// </summary>
        /// <param name="control">Valheim control name (e.g. "buttonSouth").</param>
        /// <returns>A sprite tag, or null if Valheim has no sprite for the control.</returns>
        private string ValheimGlyph(string control)
        {
            string style = SpriteStyle.For(ZInput.CurrentGlyph.ToString(), ZInput.ConnectedGamepadType.ToString());
            if (ZInput.s_gamepadSpriteMap.TryGetValue(control, out Dictionary<string, string> styles) &&
                styles.TryGetValue(style, out string spriteName))
            {
                return SpriteStyle.Tag(style, spriteName);
            }

            return null;
        }
    }
}
