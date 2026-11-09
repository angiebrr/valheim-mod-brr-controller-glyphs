// ------------------------------------------------------------------------------------------------
// KeyMapTemplate.cs: the example mapping file written when none exists.
// ------------------------------------------------------------------------------------------------

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core
{
    /// <summary>
    /// Written to the config folder when no mapping file exists. Maps nothing: the examples are
    /// commented out, so controller glyphs work and keyboard-only keys stay as keys until edited.
    /// </summary>
    public static class KeyMapTemplate
    {
        /// <summary>
        /// Template contents. Example lines are "#  entry" so that deleting the '#' enables them
        /// (covered by a test); header lines must not use that pattern.
        /// </summary>
        public const string Yaml = @"# BrrControllerGlyphs key mapping
#
# keys:    keyboard key -> the controller button(s) your Steam Input layout presses to send it.
#          One button (Q: L5) or several, pressed together (""1"": [R4, DPadUp]).
# labels:  text for buttons shown as a text badge (default: the button name).
# colors:  colors of each group's held button, used when ""Hotbar group colors"" is Custom
#          (""#RRGGBB"" or ""#RRGGBBAA"", in quotes). Unlisted buttons use the slot-label color.
# aliases: extra spellings of a key name (e.g. what a mod writes in its hover text).
#
# Buttons with art. ""Glyphs used by mod"" picks Valheim's art or the Steam Deck art; a button only
# one of them has uses that one. Other spellings that work are in brackets.
# Face buttons:  A B X Y
# Bumpers:       LB (L1)   RB (R1)
# Triggers:      LT (L2)   RT (R2)
# Sticks:        LS   RS                                   Valheim art only
# Back buttons:  L4 L5 R4 R5                               Steam Deck art only
# Menu:          Back (View, Select)   Start (Options, Menu)
# D-pad:         DPadUp DPadDown DPadLeft DPadRight
# Trackpads:     LPad (LPadClick, LTrackpad) LPadUp LPadDown LPadLeft LPadRight
#                RPad (RPadClick, RTrackpad) RPadUp RPadDown RPadLeft RPadRight   Steam Deck art only
# Any other button name (M1, Gyro, ...) is shown as a text badge like [M1].
# Key names ignore case, spaces, '_' and '-'. Alpha1/Digit1 match ""1""; Num 1/Numpad1 match Keypad1;
# Unity key names other mods use match too: LeftControl matches LeftCtrl, Return matches Enter.
# Quote keys that are numbers (""1"") or YAML words like yes/no/on/off.
#
# Remove the '#' in front of a line to use it.

labels:
#  LPadUp: LPad Up

colors:
#  R4: ""#9B8CFF""

aliases:
#  Pickup: Q

keys:
#  ""1"": [R4, DPadUp]       # hotbar slot 1: hold R4 + D-pad up
#  Keypad1: [R5, DPadUp]   # quick slot 1: hold R5 + D-pad up
#  Q: L5                   # a single back button
#  LeftShift: LPadUp       # a trackpad click
";
    }
}
