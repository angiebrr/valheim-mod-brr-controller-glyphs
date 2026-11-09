// ------------------------------------------------------------------------------------------------
// GroupShapeTests.cs: unit tests for how a prefix group's slots are laid out (F3).
// ------------------------------------------------------------------------------------------------

using BrrControllerGlyphs.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core.Tests;

/// <summary>
/// Tests for <see cref="GroupShapes.Of"/>.
/// </summary>
public class GroupShapeTests
{
    /// <summary>Slots side by side (the hotbar, the inventory's top row) are a row.</summary>
    [Fact]
    public void Side_by_side_slots_are_a_row()
    {
        Assert.Equal(GroupShape.Row, GroupShapes.Of(new[] { (0f, 0f), (70f, 0f), (140f, 0.2f) }));
    }

    /// <summary>Slots stacked (a column of AzuEPI's inventory quick slots) are a column.</summary>
    [Fact]
    public void Stacked_slots_are_a_column()
    {
        Assert.Equal(GroupShape.Column, GroupShapes.Of(new[] { (300f, 0f), (300f, -80f), (300.3f, -160f) }));
    }

    /// <summary>Slots over several rows and columns (a group ending mid-column) are an area.</summary>
    [Fact]
    public void Mixed_slots_are_an_area()
    {
        Assert.Equal(GroupShape.Area, GroupShapes.Of(new[] { (0f, 0f), (0f, -80f), (80f, 0f) }));
    }

    /// <summary>A lone slot (or none) is a row: its prefix sits above it like the others.</summary>
    [Fact]
    public void A_lone_slot_is_a_row()
    {
        Assert.Equal(GroupShape.Row, GroupShapes.Of(new[] { (10f, 10f) }));
        Assert.Equal(GroupShape.Row, GroupShapes.Of(Array.Empty<(float, float)>()));
    }
}
