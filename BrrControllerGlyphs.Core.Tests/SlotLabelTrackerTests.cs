// ------------------------------------------------------------------------------------------------
// SlotLabelTrackerTests.cs: unit tests for tracking what each bar's labels show across redraws (F3).
// ------------------------------------------------------------------------------------------------

using BrrControllerGlyphs.Core;

///////////////////////////////////////////////////////////////////////////////////////////////////

namespace BrrControllerGlyphs.Core.Tests;

/// <summary>
/// Tests for <see cref="SlotLabelTracker"/>: a setting change forces a redraw without forgetting that
/// our labels are on screen, so switching glyphs off still puts Valheim's labels back.
/// </summary>
public class SlotLabelTrackerTests
{
    /// <summary>Valheim's hotbar, as the writer names it.</summary>
    private const string Hotbar = "HotKeyBar";

    /// <summary>AzuEPI's quick-slot bar, as the writer names it.</summary>
    private const string QuickSlotBar = "AzuEPI_QuickAccessBar";

    /// <summary>
    /// A bar's state with 8 slots, first slot object 100.
    /// </summary>
    /// <param name="showGlyphs">Whether glyphs should show.</param>
    /// <returns>The state.</returns>
    private static SlotBarState State(bool showGlyphs) => new (8, 100, showGlyphs);

    /// <summary>A bar seen for the first time with glyphs on gets our labels.</summary>
    [Fact]
    public void First_update_with_glyphs_on_writes()
    {
        var tracker = new SlotLabelTracker();

        Assert.Equal(SlotLabelAction.Write, tracker.Next(Hotbar, State(showGlyphs: true), force: false));
    }

    /// <summary>Nothing changed since the last frame: the labels are left alone.</summary>
    [Fact]
    public void Unchanged_bar_is_skipped()
    {
        var tracker = new SlotLabelTracker();
        tracker.Next(Hotbar, State(showGlyphs: true), force: false);

        Assert.Equal(SlotLabelAction.Skip, tracker.Next(Hotbar, State(showGlyphs: true), force: false));
    }

    /// <summary>
    /// The bug found in game 2026-11-09: turning Enabled off redraws (invalidates) first, and the
    /// next update must still restore Valheim's labels and remove the prefixes.
    /// </summary>
    [Fact]
    public void Glyphs_turned_off_by_a_setting_restore_the_labels()
    {
        var tracker = new SlotLabelTracker();
        tracker.Next(Hotbar, State(showGlyphs: true), force: false);

        tracker.Invalidate();

        Assert.Equal(SlotLabelAction.Restore, tracker.Next(Hotbar, State(showGlyphs: false), force: false));
    }

    /// <summary>A redraw with glyphs still on (e.g. the colors changed) writes the labels again.</summary>
    [Fact]
    public void Invalidated_bar_with_glyphs_on_is_rewritten()
    {
        var tracker = new SlotLabelTracker();
        tracker.Next(Hotbar, State(showGlyphs: true), force: false);

        tracker.Invalidate();

        Assert.Equal(SlotLabelAction.Write, tracker.Next(Hotbar, State(showGlyphs: true), force: false));
    }

    /// <summary>A redraw is done once per bar: the frame after, an unchanged bar is skipped again.</summary>
    [Fact]
    public void Invalidation_is_used_once_per_bar()
    {
        var tracker = new SlotLabelTracker();
        tracker.Next(Hotbar, State(showGlyphs: true), force: false);
        tracker.Invalidate();
        tracker.Next(Hotbar, State(showGlyphs: true), force: false);

        Assert.Equal(SlotLabelAction.Skip, tracker.Next(Hotbar, State(showGlyphs: true), force: false));
    }

    /// <summary>Each bar is redrawn after an invalidation, not just the first one updated.</summary>
    [Fact]
    public void Invalidation_covers_every_bar()
    {
        var tracker = new SlotLabelTracker();
        tracker.Next(Hotbar, State(showGlyphs: true), force: false);
        tracker.Next(QuickSlotBar, State(showGlyphs: true), force: false);

        tracker.Invalidate();
        tracker.Next(Hotbar, State(showGlyphs: false), force: false);

        Assert.Equal(SlotLabelAction.Restore, tracker.Next(QuickSlotBar, State(showGlyphs: false), force: false));
    }

    /// <summary>Glyphs were never on: a redraw with them off has nothing of ours to remove.</summary>
    [Fact]
    public void Invalidated_bar_never_labelled_is_skipped()
    {
        var tracker = new SlotLabelTracker();
        tracker.Next(Hotbar, State(showGlyphs: false), force: false);

        tracker.Invalidate();

        Assert.Equal(SlotLabelAction.Skip, tracker.Next(Hotbar, State(showGlyphs: false), force: false));
    }

    /// <summary>Which bars show our labels, for restoring them when the plugin unloads.</summary>
    /// <param name="showGlyphs">Glyph state at the bar's last update.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Labelled_follows_the_last_update(bool showGlyphs)
    {
        var tracker = new SlotLabelTracker();
        tracker.Next(Hotbar, State(showGlyphs), force: false);

        Assert.Equal(showGlyphs, tracker.Labelled(Hotbar));
    }

    /// <summary>After an invalidation, a bar still counts as labelled until it's actually restored.</summary>
    [Fact]
    public void Invalidation_keeps_labelled_bars()
    {
        var tracker = new SlotLabelTracker();
        tracker.Next(Hotbar, State(showGlyphs: true), force: false);

        tracker.Invalidate();

        Assert.True(tracker.Labelled(Hotbar));
    }

    /// <summary>Forgetting everything (after the plugin restored the labels) leaves no bar labelled.</summary>
    [Fact]
    public void Clear_forgets_every_bar()
    {
        var tracker = new SlotLabelTracker();
        tracker.Next(Hotbar, State(showGlyphs: true), force: false);

        tracker.Clear();

        Assert.False(tracker.Labelled(Hotbar));
    }
}
