using Makredo.Application.Abstractions;
using Makredo.Application.Filters;
using Makredo.Application.Reporting;
using Makredo.Application.Services;
using Makredo.Core.Tests.Support;
using Makredo.Domain.Enums;
using Makredo.Domain.Models;
using Makredo.Infrastructure.Diagnostics;
using Makredo.Infrastructure.Persistence;
using Xunit;
using static Makredo.Core.Tests.Support.TestData;

namespace Makredo.Core.Tests.Reporting;

public sealed class MacroReportGeneratorTests
{
    [Fact(DisplayName = "MacroReportGenerator summarizes session analysis")]
    public Task MacroReportGenerator_SummarizesSessionAnalysis()
    {
        var session = new MacroSession
        {
            Name = "Report Session",
            CreatedAtUtc = new DateTime(2026, 5, 6, 9, 30, 0, DateTimeKind.Utc)
        };
        session.Events.AddRange(
            [
                CreateMouseMoveEvent(10, 100, 200),
                CreateMouseMoveEvent(20, 101, 201),
                CreateMouseMoveEvent(30, 102, 202),
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftDown,
                    DelayMs = 320,
                    X = 102,
                    Y = 202,
                    Description = "Sol tus basildi"
                },
                new MacroEvent
                {
                    EventType = MacroEventType.Mouse,
                    MouseActionType = MouseActionType.LeftUp,
                    DelayMs = 5,
                    X = 102,
                    Y = 202,
                    Description = "Sol tus birakildi"
                },
                new MacroEvent
                {
                    EventType = MacroEventType.Keyboard,
                    KeyboardActionType = KeyboardActionType.KeyDown,
                    DelayMs = 50,
                    KeyCode = 13,
                    ScanCode = 28,
                    KeyName = "Enter",
                    Description = "Enter tusu"
                }
            ]);

        MacroReportStatistics statistics = MacroReportGenerator.Analyze(session, "/tmp/report-session.json");
        string reportText = MacroReportGenerator.GenerateText(session, "/tmp/report-session.json");

        Assert.Equal(6, statistics.TotalEventCount);
        Assert.Equal(435, statistics.TotalDurationMs);
        Assert.Equal(1, statistics.KeyboardEventCount);
        Assert.Equal(5, statistics.MouseEventCount);
        Assert.Equal(3, statistics.MouseMoveCount);
        Assert.Equal(2, statistics.MouseClickCount);
        Assert.Equal(1, statistics.LongDelayCount);
        Assert.Equal(1, statistics.OptimizationCandidateCount);
        Assert.True(
            reportText.Contains("Toplam olay: 6", StringComparison.Ordinal),
            "Text report should include total event count.");
        Assert.True(
            reportText.Contains("Optimizasyon adayı: 1", StringComparison.Ordinal),
            "Text report should include optimization candidate count.");
        Assert.True(
            reportText.Contains("Olay Detaylari", StringComparison.Ordinal),
            "Text report should include the detailed event section.");
        Assert.True(
            reportText.Contains("001 | 00:00:00.010 | Fare | Move | X: 100, Y: 200 | 10 ms | Fare hareketi", StringComparison.Ordinal),
            "Text report should include detailed event rows.");

        return Task.CompletedTask;
    }

    [Fact(DisplayName = "MacroReportGenerator escapes HTML report values")]
    public Task MacroReportGenerator_EscapesHtmlReportValues()
    {
        var session = new MacroSession
        {
            Name = "<Macro & Test>",
            CreatedAtUtc = new DateTime(2026, 5, 6, 9, 30, 0, DateTimeKind.Utc)
        };
        session.Events.Add(
            new MacroEvent
            {
                EventType = MacroEventType.Mouse,
                MouseActionType = MouseActionType.Move,
                DelayMs = 10,
                X = 100,
                Y = 200,
                Description = "<detail & value>"
            });

        string reportHtml = MacroReportGenerator.GenerateHtml(session, "/tmp/source<unsafe>.json");

        Assert.True(
            reportHtml.Contains("&lt;Macro &amp; Test&gt;", StringComparison.Ordinal),
            "HTML report should encode the session name.");
        Assert.False(
            reportHtml.Contains("<Macro & Test>", StringComparison.Ordinal),
            "HTML report should not emit raw user-provided names.");
        Assert.True(
            reportHtml.Contains("source&lt;unsafe&gt;.json", StringComparison.Ordinal),
            "HTML report should encode file names.");
        Assert.True(
            reportHtml.Contains("&lt;detail &amp; value&gt;", StringComparison.Ordinal),
            "HTML report should encode event details.");
        Assert.True(
            reportHtml.Contains("<th>#</th>", StringComparison.Ordinal),
            "HTML report should include the detailed event table.");

        return Task.CompletedTask;
    }
}
