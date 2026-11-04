using PodBridge.Core.Notifications;

namespace PodBridge.Core.Tests;

public sealed class NotificationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static NotificationInfo N(string id = "1", string app = "msg", string name = "Messages", string title = "Anna", string body = "See you at 5") =>
        new(id, app, name, title, body);

    private static ReadContext Ctx(bool enabled = true, bool onlyInEar = true, int? buds = 1, params string[] allowed) =>
        new(enabled, onlyInEar, buds, allowed.Length == 0 ? new[] { "msg" } : allowed);

    // ---- tracker ----

    [Fact]
    public void Tracker_first_poll_is_a_baseline()
    {
        var t = new NotificationTracker();
        Assert.Empty(t.Poll(new[] { N("1"), N("2") }));
        Assert.Single(t.Poll(new[] { N("1"), N("2"), N("3") }));
    }

    [Fact]
    public void Tracker_reports_each_notification_once()
    {
        var t = new NotificationTracker();
        t.Poll(Array.Empty<NotificationInfo>());
        Assert.Single(t.Poll(new[] { N("1") }));
        Assert.Empty(t.Poll(new[] { N("1") }));
    }

    [Fact]
    public void Tracker_reused_id_after_it_vanished_is_new_again()
    {
        var t = new NotificationTracker();
        t.Poll(Array.Empty<NotificationInfo>());
        t.Poll(new[] { N("1") });
        t.Poll(Array.Empty<NotificationInfo>());
        Assert.Single(t.Poll(new[] { N("1") }));
    }

    [Fact]
    public void Tracker_ignores_duplicate_ids_in_one_poll_and_empty_ids()
    {
        var t = new NotificationTracker();
        t.Poll(Array.Empty<NotificationInfo>());
        Assert.Single(t.Poll(new[] { N("1"), N("1"), N("") }));
    }

    [Fact]
    public void Tracker_reset_makes_next_poll_a_baseline()
    {
        var t = new NotificationTracker();
        t.Poll(Array.Empty<NotificationInfo>());
        t.Reset();
        Assert.Empty(t.Poll(new[] { N("1") }));
    }

    // ---- text dedupe ----

    [Fact]
    public void Same_text_within_window_is_a_duplicate_but_not_after()
    {
        var d = new RecentTextDeduper(TimeSpan.FromSeconds(10));
        Assert.False(d.IsDuplicate(N("1"), T0));
        Assert.True(d.IsDuplicate(N("2"), T0.AddSeconds(5)));
        Assert.False(d.IsDuplicate(N("3"), T0.AddSeconds(30)));
    }

    [Fact]
    public void Different_text_or_app_is_not_a_duplicate()
    {
        var d = new RecentTextDeduper();
        Assert.False(d.IsDuplicate(N(), T0));
        Assert.False(d.IsDuplicate(N(body: "other"), T0));
        Assert.False(d.IsDuplicate(N(app: "mail"), T0));
    }

    // ---- policy ----

    [Fact]
    public void Policy_reads_an_allowed_app_when_in_ear()
        => Assert.Equal(ReadDecision.Read, new NotificationReadPolicy().Evaluate(N(), Ctx(), T0));

    [Fact]
    public void Policy_disabled_reads_nothing()
        => Assert.Equal(ReadDecision.Disabled, new NotificationReadPolicy().Evaluate(N(), Ctx(enabled: false), T0));

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Policy_needs_a_bud_in_ear_when_asked(int? buds)
        => Assert.Equal(ReadDecision.NotInEar, new NotificationReadPolicy().Evaluate(N(), Ctx(buds: buds), T0));

    [Fact]
    public void Policy_ignores_ears_when_not_asked()
        => Assert.Equal(ReadDecision.Read, new NotificationReadPolicy().Evaluate(N(), Ctx(onlyInEar: false, buds: null), T0));

    [Fact]
    public void Policy_empty_allow_list_reads_nothing()
        => Assert.Equal(ReadDecision.AppNotAllowed,
            new NotificationReadPolicy().Evaluate(N(), new ReadContext(true, false, 2, Array.Empty<string>()), T0));

    [Fact]
    public void Policy_app_id_match_ignores_case()
        => Assert.Equal(ReadDecision.Read,
            new NotificationReadPolicy().Evaluate(N(app: "MSG"), Ctx(), T0));

    [Fact]
    public void Policy_skips_other_apps()
        => Assert.Equal(ReadDecision.AppNotAllowed, new NotificationReadPolicy().Evaluate(N(app: "mail"), Ctx(), T0));

    [Fact]
    public void Policy_never_reads_podbridge_itself()
        => Assert.Equal(ReadDecision.OwnApp,
            new NotificationReadPolicy().Evaluate(N(app: "pb", name: "PodBridge"), Ctx(allowed: "pb"), T0));

    [Fact]
    public void Policy_skips_empty_and_repeated_notifications()
    {
        var p = new NotificationReadPolicy();
        Assert.Equal(ReadDecision.Empty, p.Evaluate(N(title: " ", body: ""), Ctx(), T0));
        Assert.Equal(ReadDecision.Read, p.Evaluate(N("1"), Ctx(), T0));
        Assert.Equal(ReadDecision.Duplicate, p.Evaluate(N("2"), Ctx(), T0.AddSeconds(2)));
    }

    [Fact]
    public void Policy_skipped_notifications_do_not_poison_the_deduper()
    {
        var p = new NotificationReadPolicy();
        Assert.Equal(ReadDecision.NotInEar, p.Evaluate(N("1"), Ctx(buds: 0), T0));
        Assert.Equal(ReadDecision.Read, p.Evaluate(N("2"), Ctx(buds: 1), T0.AddSeconds(1)));
    }

    // ---- seen apps ----

    [Fact]
    public void Seen_apps_adds_updates_and_ignores_blank()
    {
        var list = new List<SeenApp>();
        Assert.True(SeenApps.Record(list, "a", "Alpha"));
        Assert.False(SeenApps.Record(list, "A", "Alpha"));
        Assert.True(SeenApps.Record(list, "a", "Alpha 2"));
        Assert.False(SeenApps.Record(list, " ", "x"));
        Assert.Single(list);
        Assert.Equal("Alpha 2", list[0].Name);
    }

    [Fact]
    public void Seen_apps_uses_id_when_name_missing_and_caps_the_list()
    {
        var list = new List<SeenApp>();
        SeenApps.Record(list, "x", "");
        Assert.Equal("x", list[0].Name);
        for (int i = 0; i < SeenApps.MaxApps + 10; i++)
        {
            SeenApps.Record(list, "app" + i, "App " + i);
        }
        Assert.Equal(SeenApps.MaxApps, list.Count);
        Assert.DoesNotContain(list, a => a.Id == "x");
    }

    [Fact]
    public void Seen_apps_sort_by_name()
    {
        var sorted = SeenApps.Sorted(new[] { new SeenApp { Id = "1", Name = "zed" }, new SeenApp { Id = "2", Name = "Alpha" } });
        Assert.Equal("Alpha", sorted[0].Name);
    }

    // ---- formatting ----

    [Fact]
    public void Format_title_only_says_app_and_title()
        => Assert.Equal("Messages. Anna", SpeechFormatter.Format(N(), fullText: false));

    [Fact]
    public void Format_full_text_adds_the_body()
        => Assert.Equal("Messages. Anna. See you at 5", SpeechFormatter.Format(N(), fullText: true));

    [Fact]
    public void Format_does_not_double_punctuation()
        => Assert.Equal("Messages. Anna! Hello there.", SpeechFormatter.Format(N(title: "Anna!", body: "Hello there."), true));

    [Fact]
    public void Format_says_repeated_parts_once()
        => Assert.Equal("Mail. Hello", SpeechFormatter.Format(N(name: "Mail", title: "Mail", body: "Hello"), true));

    [Fact]
    public void Format_replaces_links_and_collapses_whitespace()
        => Assert.Equal("Messages. Anna. look link now", SpeechFormatter.Format(N(body: "look\n https://example.com/a?b=c   now"), true));

    [Fact]
    public void Format_truncates_long_text()
    {
        string text = SpeechFormatter.Format(N(body: new string('a', 1000)), true);
        Assert.True(text.Length <= SpeechFormatter.MaxLength + 1);
        Assert.EndsWith("…", text);
    }

    [Fact]
    public void Format_handles_missing_parts()
    {
        Assert.Equal("Messages", SpeechFormatter.Format(N(title: "", body: ""), true));
        Assert.Equal(string.Empty, SpeechFormatter.Format(N(name: "", title: null!, body: null!), true));
    }

    // ---- queue ----

    [Fact]
    public void Queue_is_fifo_and_drops_oldest_when_full()
    {
        var q = new SpeechQueue(2);
        q.Enqueue("a");
        q.Enqueue("b");
        Assert.Equal(1, q.Enqueue("c"));
        Assert.True(q.TryDequeue(out var first));
        Assert.Equal("b", first);
        Assert.True(q.TryDequeue(out var second));
        Assert.Equal("c", second);
        Assert.False(q.TryDequeue(out _));
    }

    [Fact]
    public void Queue_ignores_blank_text_and_clears()
    {
        var q = new SpeechQueue();
        q.Enqueue("  ");
        Assert.Equal(0, q.Count);
        q.Enqueue("x");
        q.Clear();
        Assert.Equal(0, q.Count);
    }

    // ---- access text ----

    [Fact]
    public void Access_text_explains_every_state()
    {
        foreach (var a in Enum.GetValues<ListenerAccess>())
        {
            Assert.False(string.IsNullOrWhiteSpace(ListenerAccessText.Describe(a, false)));
        }
        Assert.Contains("packaged", ListenerAccessText.Describe(ListenerAccess.Unavailable, false));
        Assert.True(ListenerAccessText.CanRequest(ListenerAccess.NeedsRequest));
        Assert.True(ListenerAccessText.ShowOpenSettings(ListenerAccess.Denied));
        Assert.False(ListenerAccessText.FeatureUsable(ListenerAccess.Unavailable));
        Assert.True(ListenerAccessText.FeatureUsable(ListenerAccess.Denied));
    }
}
