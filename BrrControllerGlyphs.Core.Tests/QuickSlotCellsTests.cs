// ------------------------------------------------------------------------------------------------
// QuickSlotCellsTests.cs: unit tests for finding AzuEPI's quick-slot cells in the inventory (F3).
// ------------------------------------------------------------------------------------------------

using BrrControllerGlyphs.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core.Tests;

/// <summary>
/// Tests for <see cref="QuickSlotCells.Find"/>.
/// </summary>
public class QuickSlotCellsTests
{
    /// <summary>Each quick slot's name is found in the cell showing it.</summary>
    [Fact]
    public void Names_are_matched_to_their_cells()
    {
        string[] cells = { "1", "2", "Helmet", "R5 Y", "R5 Up", null };

        int[] found = QuickSlotCells.Find(cells, new[] { "R5 Up", "R5 Y" });

        Assert.Equal(new[] { 4, 3 }, found);
    }

    /// <summary>A name no cell shows gives -1.</summary>
    [Fact]
    public void Missing_names_give_minus_one()
    {
        Assert.Equal(new[] { 0, -1 }, QuickSlotCells.Find(new[] { "Keypad1" }, new[] { "Keypad1", "Keypad2" }));
    }

    /// <summary>Duplicate names take different cells, in order.</summary>
    [Fact]
    public void Duplicate_names_take_different_cells()
    {
        Assert.Equal(new[] { 1, 3 }, QuickSlotCells.Find(new[] { "a", "x", "b", "x" }, new[] { "x", "x" }));
    }

    /// <summary>Empty or null names never match (empty cells are common).</summary>
    /// <param name="name">An empty or null name.</param>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Empty_names_never_match(string name)
    {
        Assert.Equal(new[] { -1 }, QuickSlotCells.Find(new[] { "", null }, new[] { name }));
    }
}
