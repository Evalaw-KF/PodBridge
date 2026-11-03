using PodBridge.Core.Ear;

namespace PodBridge.Core.Tests;

public sealed class EarDetectionPolicyTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static DateTimeOffset At(double seconds) => T0.AddSeconds(seconds);

    private static EarDetectionPolicy Started(int buds = 2)
    {
        var p = new EarDetectionPolicy();
        p.Update(buds, T0);
        return p;
    }

    [Fact]
    public void First_reading_sets_the_baseline_without_action()
    {
        var p = new EarDetectionPolicy();
        var r = p.Update(0, T0);
        Assert.Equal(EarAction.None, r.Action);
        Assert.False(r.StableChanged);   // a starting point, not a change
        Assert.Equal(0, r.StableCount);
    }

    [Fact]
    public void Pauses_after_the_debounce_when_a_bud_comes_out()
    {
        var p = Started();
        Assert.Equal(EarAction.None, p.Update(1, At(1)).Action);
        Assert.Equal(EarAction.None, p.Tick(At(2.0)).Action);
        var r = p.Tick(At(2.6));
        Assert.Equal(EarAction.Pause, r.Action);
        Assert.True(r.StableChanged);
        Assert.Equal(1, r.StableCount);
    }

    [Fact]
    public void Flicker_shorter_than_the_debounce_does_nothing()
    {
        var p = Started();
        p.Update(1, At(1));
        p.Update(2, At(1.5));
        var r = p.Tick(At(5));
        Assert.Equal(EarAction.None, r.Action);
        Assert.False(r.StableChanged);
        Assert.Equal(2, p.StableCount);
    }

    [Fact]
    public void Flicker_restarts_the_timer_for_a_new_value()
    {
        var p = Started();
        p.Update(1, At(1));
        p.Update(0, At(2));          // different pending value, timer restarts at 2
        Assert.Equal(EarAction.None, p.Tick(At(3)).Action);
        Assert.Equal(EarAction.Pause, p.Tick(At(3.6)).Action);
        Assert.Equal(0, p.StableCount);
    }

    [Fact]
    public void Resumes_when_both_buds_return_if_we_paused()
    {
        var p = Started();
        p.Update(0, At(1));
        Assert.Equal(EarAction.Pause, p.Tick(At(3)).Action);
        p.Update(2, At(10));
        Assert.Equal(EarAction.Resume, p.Tick(At(12)).Action);
        Assert.False(p.PausedByUs);
    }

    [Fact]
    public void Going_from_one_bud_to_zero_does_not_pause_again_and_one_bud_does_not_resume()
    {
        var p = Started();
        p.Update(1, At(1));
        Assert.Equal(EarAction.Pause, p.Tick(At(3)).Action);
        p.Update(0, At(4));
        Assert.Equal(EarAction.None, p.Tick(At(6)).Action);
        p.Update(1, At(7));
        Assert.Equal(EarAction.None, p.Tick(At(9)).Action);
        Assert.True(p.PausedByUs);
        p.Update(2, At(10));
        Assert.Equal(EarAction.Resume, p.Tick(At(12)).Action);
    }

    [Fact]
    public void Does_not_resume_when_nothing_was_paused()
    {
        var p = Started();
        p.Update(0, At(1));
        Assert.Equal(EarAction.Pause, p.Tick(At(3)).Action);
        p.PauseDidNothing();
        p.Update(2, At(5));
        Assert.Equal(EarAction.None, p.Tick(At(7)).Action);
    }

    [Fact]
    public void Does_not_pause_when_starting_with_buds_already_out()
    {
        var p = Started(0);
        p.Update(2, At(1));
        Assert.Equal(EarAction.None, p.Tick(At(3)).Action);   // 0 -> 2 and we paused nothing
        Assert.Equal(2, p.StableCount);
    }

    [Fact]
    public void AutoPause_off_never_pauses_or_resumes()
    {
        var p = Started();
        p.AutoPause = false;
        p.Update(0, At(1));
        Assert.Equal(EarAction.None, p.Tick(At(3)).Action);
        p.Update(2, At(5));
        Assert.Equal(EarAction.None, p.Tick(At(7)).Action);
    }

    [Fact]
    public void Resume_off_pauses_but_never_resumes_and_forgets_the_pause()
    {
        var p = Started();
        p.Resume = false;
        p.Update(0, At(1));
        Assert.Equal(EarAction.Pause, p.Tick(At(3)).Action);
        p.Update(2, At(5));
        Assert.Equal(EarAction.None, p.Tick(At(7)).Action);
        Assert.False(p.PausedByUs);
    }

    [Fact]
    public void One_bud_mode_pauses_only_when_both_are_out_and_resumes_with_one()
    {
        var p = Started();
        p.OnlyUseOneBud = true;
        p.Update(1, At(1));
        Assert.Equal(EarAction.None, p.Tick(At(3)).Action);     // 2 -> 1 is normal for this user
        p.Update(0, At(4));
        Assert.Equal(EarAction.Pause, p.Tick(At(6)).Action);
        p.Update(1, At(7));
        Assert.Equal(EarAction.Resume, p.Tick(At(9)).Action);
    }

    [Fact]
    public void Update_alone_commits_once_enough_time_has_passed()
    {
        var p = Started();
        p.Update(1, At(1));
        var r = p.Update(1, At(3));   // a repeat reading after the debounce also commits
        Assert.Equal(EarAction.Pause, r.Action);
    }

    [Fact]
    public void Tick_without_pending_change_reports_nothing()
    {
        var p = Started();
        var r = p.Tick(At(100));
        Assert.Equal(EarAction.None, r.Action);
        Assert.False(r.StableChanged);
    }

    [Fact]
    public void Readings_are_clamped_to_0_to_2()
    {
        var p = Started();
        p.Update(7, At(1));
        Assert.False(p.HasPendingChange);
        p.Update(-3, At(2));
        Assert.Equal(EarAction.Pause, p.Tick(At(4)).Action);
        Assert.Equal(0, p.StableCount);
    }

    [Fact]
    public void Custom_debounce_is_respected()
    {
        var p = Started();
        p.Debounce = TimeSpan.FromSeconds(5);
        p.Update(0, At(1));
        Assert.Equal(EarAction.None, p.Tick(At(4)).Action);
        Assert.Equal(EarAction.Pause, p.Tick(At(6.1)).Action);
    }

    [Fact]
    public void Reset_clears_state()
    {
        var p = Started();
        p.Update(0, At(1));
        p.Tick(At(3));
        p.Reset();
        Assert.Null(p.StableCount);
        Assert.False(p.PausedByUs);
        Assert.Equal(EarAction.None, p.Update(2, At(5)).Action);
    }
}
