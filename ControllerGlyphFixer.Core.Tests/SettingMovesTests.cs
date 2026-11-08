// ------------------------------------------------------------------------------------------------
// SettingMovesTests.cs: unit tests for carrying settings over to their new sections.
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="SettingMoves"/>.
/// </summary>
public class SettingMovesTests
{
    /// <summary>The hotbar-label settings move from General to Hotbar labels; nothing else moves.</summary>
    [Fact]
    public void Hotbar_label_settings_move_out_of_general()
    {
        Assert.Equal(
            new[] { "HotbarLabels", "PrefixColors", "PrefixBackground" },
            SettingMoves.All.Select(move => move.Key));
        Assert.All(SettingMoves.All, move => Assert.Equal(("General", "Hotbar labels"), (move.From, move.To)));
    }

    /// <summary>An old .cfg's entries left in General (BepInEx's orphans) are the moves to carry over.</summary>
    [Fact]
    public void Pending_moves_are_the_ones_left_in_the_old_section()
    {
        var orphans = new[] { ("General", "PrefixColors"), ("General", "HotbarLabels"), ("General", "SomethingOld") };

        Assert.Equal(new[] { "HotbarLabels", "PrefixColors" }, SettingMoves.Pending(orphans).Select(move => move.Key));
    }

    /// <summary>A .cfg written after the move (or a fresh install) has nothing to carry over.</summary>
    [Fact]
    public void Nothing_pending_without_old_entries()
    {
        Assert.Empty(SettingMoves.Pending(new[] { ("Hotbar labels", "PrefixColors") }));
    }
}
