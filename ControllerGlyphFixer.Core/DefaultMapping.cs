namespace ControllerGlyphFixer.Core
{
    /// <summary>
    /// The mapping file written to the config folder on first run.
    /// </summary>
    public static class DefaultMapping
    {
        public const string Yaml = @"# ControllerGlyphFixer key mapping
#
# keys:    keyboard key -> the controller button(s) your Steam Input layout presses to send it.
#          One button (Q: L5) or several, pressed together (""1"": [R4, DPadUp]).
# labels:  text shown for buttons Valheim has no art for (default: the button name).
# aliases: extra spellings of a key name (e.g. what a mod writes in its hover text).
#
# Buttons with Valheim art: A B X Y LB RB LT RT LS RS Back Start DPadUp DPadRight DPadDown DPadLeft
# Any other button name (R4, L5, LPadUp, ...) is shown as a text badge like [R4].
# Key names ignore case, spaces, '_' and '-'. Alpha1/Digit1 match ""1""; Num 1/Numpad1 match Keypad1.

labels:
  LPadUp: LPad Up
  LPadDown: LPad Down

aliases: {}

keys:
  # Hotbar: hold R4
  ""1"": [R4, DPadUp]
  ""2"": [R4, DPadRight]
  ""3"": [R4, DPadDown]
  ""4"": [R4, DPadLeft]
  ""5"": [R4, Y]
  ""6"": [R4, B]
  ""7"": [R4, A]
  ""8"": [R4, X]

  # Quick slots: hold R5
  Keypad1: [R5, DPadUp]
  Keypad2: [R5, DPadRight]
  Keypad3: [R5, DPadDown]
  Keypad4: [R5, DPadLeft]
  Keypad5: [R5, Y]
  Keypad6: [R5, B]

  I: [R5, A]      # backpack
  N: [R5, X]      # ImpactfulSkills AOE toggle
  Q: L5           # pickup
  Z: L4           # AzuAutoStore favorite
  B: LPadUp       # AutomaticFuel torch mode (left trackpad click up)
  LeftShift: LPadDown  # AzuCraftyBoxes fill all (left trackpad click down)
";
    }
}
