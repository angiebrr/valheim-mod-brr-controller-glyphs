// ------------------------------------------------------------------------------------------------
// BarLabelsTests.cs: unit tests for grouping a bar's slots by their shared prefix (F3).
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

    /// <summary>
    /// Asserts a group's position and prefix.
    /// </summary>
    /// <param name="group">The group to check.</param>
    /// <param name="firstSlot">Expected first slot.</param>
    /// <param name="slotCount">Expected number of slots.</param>
    /// <param name="prefix">Expected prefix buttons, comma-separated.</param>
    private static void AssertGroup(PrefixGroup group, int firstSlot, int slotCount, string prefix)
    {
        Assert.Equal(firstSlot, group.FirstSlot);
        Assert.Equal(slotCount, group.SlotCount);
        Assert.Equal(prefix.Split(','), group.Prefix);
    }

    /// <summary>My hotbar: every slot holds R4, so one group; slots keep one button each.</summary>
    [Fact]
    public void One_prefix_button_makes_one_group()
    {
        var labels = BarLabels.Split(new[] { Slot("R4,DPadUp"), Slot("R4,DPadRight"), Slot("R4,Y") });

        AssertGroup(Assert.Single(labels.Groups), firstSlot: 0, slotCount: 3, prefix: "R4");
        Assert.Equal(new[] { "DPadUp" }, labels.Slots[0]);
        Assert.Equal(new[] { "DPadRight" }, labels.Slots[1]);
        Assert.Equal(new[] { "Y" }, labels.Slots[2]);
    }

    /// <summary>Mixed prefix buttons: one group per run of neighbouring slots.</summary>
    [Fact]
    public void Mixed_prefix_buttons_make_a_group_per_run()
    {
        var labels = BarLabels.Split(new[] { Slot("R4,DPadUp"), Slot("R4,DPadDown"), Slot("L4,A"), Slot("L4,B") });

        Assert.Equal(2, labels.Groups.Count);
        AssertGroup(labels.Groups[0], firstSlot: 0, slotCount: 2, prefix: "R4");
        AssertGroup(labels.Groups[1], firstSlot: 2, slotCount: 2, prefix: "L4");
        Assert.Equal(new[] { "A" }, labels.Slots[2]);
    }

    /// <summary>The same prefix button after a different one starts a new group.</summary>
    [Fact]
    public void A_returning_prefix_button_starts_a_new_group()
    {
        var labels = BarLabels.Split(new[] { Slot("R4,A"), Slot("L4,B"), Slot("R4,X") });

        Assert.Equal(3, labels.Groups.Count);
        AssertGroup(labels.Groups[2], firstSlot: 2, slotCount: 1, prefix: "R4");
    }

    /// <summary>A lone slot still moves its prefix button to a prefix, so it looks like the others.</summary>
    [Fact]
    public void Single_slot_still_moves_the_prefix_button()
    {
        var labels = BarLabels.Split(new[] { Slot("R4,DPadUp") });

        AssertGroup(Assert.Single(labels.Groups), firstSlot: 0, slotCount: 1, prefix: "R4");
        Assert.Equal(new[] { "DPadUp" }, labels.Slots[0]);
    }

    /// <summary>Unmapped slots stay null and split the groups around them.</summary>
    [Fact]
    public void Unmapped_slots_split_groups()
    {
        var labels = BarLabels.Split(new[] { Slot("R5,DPadUp"), null, Slot("R5,B") });

        Assert.Equal(2, labels.Groups.Count);
        AssertGroup(labels.Groups[1], firstSlot: 2, slotCount: 1, prefix: "R5");
        Assert.Null(labels.Slots[1]);
    }

    /// <summary>A one-button slot has nothing to share: no group, it keeps its button.</summary>
    [Fact]
    public void One_button_slots_are_not_grouped()
    {
        var labels = BarLabels.Split(new[] { Slot("R4"), Slot("R4,Y") });

        AssertGroup(Assert.Single(labels.Groups), firstSlot: 1, slotCount: 1, prefix: "R4");
        Assert.Equal(new[] { "R4" }, labels.Slots[0]);
        Assert.Equal(new[] { "Y" }, labels.Slots[1]);
    }

    /// <summary>Longer shared prefixes (two held buttons) move together; each slot keeps a button.</summary>
    [Fact]
    public void Several_shared_buttons_move_together()
    {
        var labels = BarLabels.Split(new[] { Slot("L4,R4,A"), Slot("L4,R4,B"), Slot("L4,X") });

        AssertGroup(Assert.Single(labels.Groups), firstSlot: 0, slotCount: 3, prefix: "L4");

        labels = BarLabels.Split(new[] { Slot("L4,R4,A"), Slot("L4,R4,B") });
        AssertGroup(Assert.Single(labels.Groups), firstSlot: 0, slotCount: 2, prefix: "L4,R4");
        Assert.Equal(new[] { "A" }, labels.Slots[0]);
    }

    /// <summary>Button names match the way the mapping file does (case, spaces, '_' and '-').</summary>
    [Fact]
    public void Prefix_buttons_match_like_the_mapping_file()
    {
        var labels = BarLabels.Split(new[] { Slot("R4,A"), Slot("r4,B") });

        AssertGroup(Assert.Single(labels.Groups), firstSlot: 0, slotCount: 2, prefix: "R4");
    }

    /// <summary>No slots, or no mapped slots: no groups, slots stay as given.</summary>
    [Fact]
    public void No_mapped_slots_make_no_groups()
    {
        Assert.Empty(BarLabels.Split(Array.Empty<IReadOnlyList<string>>()).Groups);

        var labels = BarLabels.Split(new IReadOnlyList<string>[] { null, null });
        Assert.Empty(labels.Groups);
        Assert.All(labels.Slots, Assert.Null);
    }
}
