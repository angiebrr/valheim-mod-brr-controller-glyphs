// ------------------------------------------------------------------------------------------------
// BarLabelsTests.cs: unit tests for splitting a bar's shared hold button from its slot buttons (F3).
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="BarLabels.Split"/>.
/// </summary>
public class BarLabelsTests
{
    /// <summary>
    /// Builds a slot's button list from a comma-separated string; null stays null (unmapped slot).
    /// </summary>
    /// <param name="buttons">Comma-separated buttons, or null.</param>
    /// <returns>The button list, or null.</returns>
    private static IReadOnlyList<string> Slot(string buttons) => buttons?.Split(',');

    /// <summary>My hotbar: every slot holds R4, so R4 moves to the bar and slots keep one button.</summary>
    [Fact]
    public void Shared_hold_button_moves_to_the_bar()
    {
        var labels = BarLabels.Split(new[] { Slot("R4,DPadUp"), Slot("R4,DPadRight"), Slot("R4,Y") });

        Assert.Equal(new[] { "R4" }, labels.Shared);
        Assert.Equal(new[] { "DPadUp" }, labels.Slots[0]);
        Assert.Equal(new[] { "DPadRight" }, labels.Slots[1]);
        Assert.Equal(new[] { "Y" }, labels.Slots[2]);
    }

    /// <summary>Unmapped slots stay null and don't stop the others sharing.</summary>
    [Fact]
    public void Unmapped_slots_are_ignored()
    {
        var labels = BarLabels.Split(new[] { Slot("R5,DPadUp"), null, Slot("R5,B") });

        Assert.Equal(new[] { "R5" }, labels.Shared);
        Assert.Null(labels.Slots[1]);
        Assert.Equal(new[] { "B" }, labels.Slots[2]);
    }

    /// <summary>Slots with nothing in common share nothing and keep all their buttons.</summary>
    [Fact]
    public void Nothing_in_common_shares_nothing()
    {
        var labels = BarLabels.Split(new[] { Slot("R4,DPadUp"), Slot("R5,DPadUp") });

        Assert.Empty(labels.Shared);
        Assert.Equal(new[] { "R4", "DPadUp" }, labels.Slots[0]);
        Assert.Equal(new[] { "R5", "DPadUp" }, labels.Slots[1]);
    }

    /// <summary>Every slot keeps at least one button, even when one slot is a prefix of another.</summary>
    [Fact]
    public void Every_slot_keeps_a_button()
    {
        var labels = BarLabels.Split(new[] { Slot("R4"), Slot("R4,Y") });

        Assert.Empty(labels.Shared);
        Assert.Equal(new[] { "R4" }, labels.Slots[0]);
    }

    /// <summary>A bar with one mapped slot still moves the hold button, so it looks like the others.</summary>
    [Fact]
    public void Single_slot_still_moves_the_hold_button()
    {
        var labels = BarLabels.Split(new[] { Slot("R4,DPadUp") });

        Assert.Equal(new[] { "R4" }, labels.Shared);
        Assert.Equal(new[] { "DPadUp" }, labels.Slots[0]);
    }

    /// <summary>Longer shared prefixes (e.g. two held buttons) move together.</summary>
    [Fact]
    public void Several_shared_buttons_move_together()
    {
        var labels = BarLabels.Split(new[] { Slot("L4,R4,A"), Slot("L4,R4,B") });

        Assert.Equal(new[] { "L4", "R4" }, labels.Shared);
        Assert.Equal(new[] { "A" }, labels.Slots[0]);
    }

    /// <summary>Button names match the way the mapping file does (case, spaces, '_' and '-').</summary>
    [Fact]
    public void Shared_buttons_match_like_the_mapping_file()
    {
        var labels = BarLabels.Split(new[] { Slot("R4,A"), Slot("r4,B") });

        Assert.Equal(new[] { "R4" }, labels.Shared);
    }

    /// <summary>No slots, or no mapped slots: nothing shared, slots stay as given.</summary>
    [Fact]
    public void No_mapped_slots_shares_nothing()
    {
        Assert.Empty(BarLabels.Split(Array.Empty<IReadOnlyList<string>>()).Shared);

        var labels = BarLabels.Split(new IReadOnlyList<string>[] { null, null });
        Assert.Empty(labels.Shared);
        Assert.All(labels.Slots, Assert.Null);
    }
}
