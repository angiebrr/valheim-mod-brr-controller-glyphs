// ------------------------------------------------------------------------------------------------
// SlotLabelRefreshTests.cs: unit tests for when slot labels are rewritten or restored (F3).
// ------------------------------------------------------------------------------------------------

using ControllerGlyphFixer.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace ControllerGlyphFixer.Core.Tests;

/// <summary>
/// Tests for <see cref="SlotLabelRefresh.Decide"/>.
/// </summary>
public class SlotLabelRefreshTests
{
    /// <summary>Eight slots, first slot object 100, glyphs on.</summary>
    private static readonly SlotBarState GlyphsOn = new (slotCount: 8, firstSlotId: 100, showGlyphs: true);

    /// <summary>The same slots with glyphs off.</summary>
    private static readonly SlotBarState GlyphsOff = new (slotCount: 8, firstSlotId: 100, showGlyphs: false);

    /// <summary>A bar seen for the first time gets labels when glyphs are on.</summary>
    [Fact]
    public void First_sight_with_glyphs_on_writes()
    {
        Assert.Equal(SlotLabelAction.Write, SlotLabelRefresh.Decide(last: null, GlyphsOn, force: false));
    }

    /// <summary>A bar seen for the first time with glyphs off keeps Valheim's labels.</summary>
    [Fact]
    public void First_sight_with_glyphs_off_skips()
    {
        Assert.Equal(SlotLabelAction.Skip, SlotLabelRefresh.Decide(last: null, GlyphsOff, force: false));
    }

    /// <summary>Nothing changed since the last write: skip (UpdateIcons runs every frame).</summary>
    [Fact]
    public void Unchanged_bar_skips()
    {
        Assert.Equal(SlotLabelAction.Skip, SlotLabelRefresh.Decide(GlyphsOn, GlyphsOn, force: false));
    }

    /// <summary>Valheim just reset the labels (forced): write again even though nothing changed.</summary>
    [Fact]
    public void Forced_with_glyphs_on_writes_even_if_unchanged()
    {
        Assert.Equal(SlotLabelAction.Write, SlotLabelRefresh.Decide(GlyphsOn, GlyphsOn, force: true));
    }

    /// <summary>
    /// Slots rebuilt with the same count (AzuEPI does this after Settings closes): the new slot
    /// objects have new IDs, so the labels are written again.
    /// </summary>
    [Fact]
    public void Slots_rebuilt_with_the_same_count_writes()
    {
        var rebuilt = new SlotBarState(slotCount: 8, firstSlotId: 200, showGlyphs: true);

        Assert.Equal(SlotLabelAction.Write, SlotLabelRefresh.Decide(GlyphsOn, rebuilt, force: false));
    }

    /// <summary>Slot count changed (an item bound to a later slot): write.</summary>
    [Fact]
    public void Slot_count_changed_writes()
    {
        var moreSlots = new SlotBarState(slotCount: 9, firstSlotId: 100, showGlyphs: true);

        Assert.Equal(SlotLabelAction.Write, SlotLabelRefresh.Decide(GlyphsOn, moreSlots, force: false));
    }

    /// <summary>Glyphs just turned on: write.</summary>
    [Fact]
    public void Glyphs_turned_on_writes()
    {
        Assert.Equal(SlotLabelAction.Write, SlotLabelRefresh.Decide(GlyphsOff, GlyphsOn, force: false));
    }

    /// <summary>Glyphs just turned off: let Valheim restore its own labels.</summary>
    [Fact]
    public void Glyphs_turned_off_restores()
    {
        Assert.Equal(SlotLabelAction.Restore, SlotLabelRefresh.Decide(GlyphsOn, GlyphsOff, force: false));
    }

    /// <summary>
    /// Glyphs already off: never restore again, even when forced (Valheim just wrote its own
    /// labels, and restoring calls back into the forced path).
    /// </summary>
    /// <param name="force">Whether Valheim just reset the labels.</param>
    [Theory]
    [InlineData(false)] // UpdateIcons, nothing changed
    [InlineData(true)]  // ToggleBindingHint / OnEnable
    public void Glyphs_already_off_skips(bool force)
    {
        Assert.Equal(SlotLabelAction.Skip, SlotLabelRefresh.Decide(GlyphsOff, GlyphsOff, force));
    }

    /// <summary>States with the same values are equal; any different value makes them differ.</summary>
    [Fact]
    public void States_compare_by_value()
    {
        Assert.Equal(GlyphsOn, new SlotBarState(8, 100, true));
        Assert.True(GlyphsOn == new SlotBarState(8, 100, true));
        Assert.True(GlyphsOn != GlyphsOff);
        Assert.NotEqual(GlyphsOn, new SlotBarState(8, 101, true));
        Assert.NotEqual(GlyphsOn, new SlotBarState(7, 100, true));
    }
}
