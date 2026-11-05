using MacroMaster.Domain.Enums;
using MacroMaster.Domain.Models;
using MacroMaster.WinForms.Controls;
using System.Globalization;
using System.Net;
using System.Text;

namespace MacroMaster.WinForms.Reporting;

internal sealed record MacroReportStatistics(
    string SessionName,
    string FileName,
    string FilePath,
    DateTime CreatedAtLocal,
    DateTime GeneratedAtLocal,
    int TotalEventCount,
    int TotalDurationMs,
    int KeyboardEventCount,
    int MouseEventCount,
    int MouseMoveCount,
    int MouseClickCount,
    int MouseWheelCount,
    int SystemEventCount,
    int LongDelayCount,
    int InvalidOrIncompleteCount,
    int OptimizationCandidateCount,
    int LongestDelayMs,
    double AverageDelayMs,
    double OptimizationCandidateRate);

internal static class MacroReportGenerator
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    public static MacroReportStatistics Analyze(MacroSession session, string? filePath)
    {
        ArgumentNullException.ThrowIfNull(session);

        IReadOnlyList<MacroEvent> events = session.Events;
        int totalEventCount = events.Count;
        int totalDurationMs = events.Sum(macroEvent => Math.Max(0, macroEvent.DelayMs));
        int optimizationCandidateCount = EventListFilterEngine.Apply(
            events,
            new EventListFilterCriteria(
                null,
                EventListTypeFilterKind.All,
                EventListSmartFilterKind.OptimizationCandidates)).Length;
        int invalidOrIncompleteCount = EventListFilterEngine.Apply(
            events,
            new EventListFilterCriteria(
                null,
                EventListTypeFilterKind.All,
                EventListSmartFilterKind.InvalidOrIncomplete)).Length;

        return new MacroReportStatistics(
            session.Name,
            ResolveFileName(filePath),
            ResolveFilePath(filePath),
            ToLocalTime(session.CreatedAtUtc),
            DateTime.Now,
            totalEventCount,
            totalDurationMs,
            events.Count(macroEvent => macroEvent.EventType == MacroEventType.Keyboard),
            events.Count(macroEvent => macroEvent.EventType == MacroEventType.Mouse),
            events.Count(IsMouseMove),
            events.Count(IsMouseClick),
            events.Count(IsMouseWheel),
            events.Count(macroEvent => macroEvent.EventType == MacroEventType.System),
            events.Count(macroEvent => macroEvent.DelayMs >= EventListFilterEngine.LongDelayThresholdMs),
            invalidOrIncompleteCount,
            optimizationCandidateCount,
            totalEventCount == 0 ? 0 : events.Max(macroEvent => Math.Max(0, macroEvent.DelayMs)),
            totalEventCount == 0 ? 0d : totalDurationMs / (double)totalEventCount,
            totalEventCount == 0 ? 0d : optimizationCandidateCount * 100d / totalEventCount);
    }

    public static string GenerateText(MacroSession session, string? filePath)
    {
        MacroReportStatistics statistics = Analyze(session, filePath);
        var builder = new StringBuilder();

        builder.AppendLine("Polly Oturum Raporu");
        builder.AppendLine(new string('=', 29));
        builder.AppendLine();
        builder.AppendLine("Makro: " + statistics.SessionName);
        builder.AppendLine("Dosya: " + statistics.FileName);
        builder.AppendLine("Dosya yolu: " + statistics.FilePath);
        builder.AppendLine("Kayıt tarihi: " + FormatDateTime(statistics.CreatedAtLocal));
        builder.AppendLine("Rapor tarihi: " + FormatDateTime(statistics.GeneratedAtLocal));
        builder.AppendLine();
        builder.AppendLine("Özet");
        builder.AppendLine("----");
        builder.AppendLine("Toplam olay: " + statistics.TotalEventCount.ToString("N0", TurkishCulture));
        builder.AppendLine("Toplam süre: " + FormatDuration(statistics.TotalDurationMs));
        builder.AppendLine("Ortalama gecikme: " + FormatMilliseconds(statistics.AverageDelayMs));
        builder.AppendLine("En uzun bekleme: " + FormatDuration(statistics.LongestDelayMs));
        builder.AppendLine();
        builder.AppendLine("Olay Dağılımı");
        builder.AppendLine("-------------");
        builder.AppendLine("Klavye olayları: " + statistics.KeyboardEventCount.ToString("N0", TurkishCulture));
        builder.AppendLine("Mouse olayları: " + statistics.MouseEventCount.ToString("N0", TurkishCulture));
        builder.AppendLine("Mouse hareketleri: " + statistics.MouseMoveCount.ToString("N0", TurkishCulture));
        builder.AppendLine("Tıklama olayları: " + statistics.MouseClickCount.ToString("N0", TurkishCulture));
        builder.AppendLine("Mouse kaydırma olayları: " + statistics.MouseWheelCount.ToString("N0", TurkishCulture));
        builder.AppendLine("Sistem olayları: " + statistics.SystemEventCount.ToString("N0", TurkishCulture));
        builder.AppendLine();
        builder.AppendLine("Analiz");
        builder.AppendLine("------");
        builder.AppendLine("Uzun bekleme: " + statistics.LongDelayCount.ToString("N0", TurkishCulture));
        builder.AppendLine("Eksik veya hatalı olay: " + statistics.InvalidOrIncompleteCount.ToString("N0", TurkishCulture));
        builder.AppendLine("Optimizasyon adayı: " + statistics.OptimizationCandidateCount.ToString("N0", TurkishCulture));
        builder.AppendLine("Optimizasyon aday oranı: " + statistics.OptimizationCandidateRate.ToString("0.#", TurkishCulture) + "%");
        builder.AppendLine();
        builder.AppendLine("Öneri");
        builder.AppendLine("-----");
        builder.AppendLine(BuildRecommendation(statistics));
        builder.AppendLine();
        builder.AppendLine("Olay Detaylari");
        builder.AppendLine("---------------");
        builder.AppendLine("# | Zaman | Tür | Aksiyon | Konum | Gecikme | Detay");
        builder.AppendLine("--|-------|-----|---------|-------|---------|------");

        foreach (string row in BuildTextEventRows(session.Events))
        {
            builder.AppendLine(row);
        }

        return builder.ToString();
    }

    public static string GenerateHtml(MacroSession session, string? filePath)
    {
        MacroReportStatistics statistics = Analyze(session, filePath);
        string recommendation = BuildRecommendation(statistics);

        return $$"""
            <!doctype html>
            <html lang="tr">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Polly Oturum Raporu - {{Html(statistics.SessionName)}}</title>
              <style>
                :root {
                  color-scheme: dark;
                  --bg: #070b12;
                  --surface: #111622;
                  --surface-soft: #171e2e;
                  --border: #25324d;
                  --text: #f5f7fb;
                  --muted: #aab8dc;
                  --accent: #4c94ff;
                  --warning: #f5a524;
                }
                * { box-sizing: border-box; }
                body {
                  margin: 0;
                  background: var(--bg);
                  color: var(--text);
                  font: 15px/1.55 "Segoe UI", Arial, sans-serif;
                }
                main {
                  max-width: 1040px;
                  margin: 0 auto;
                  padding: 42px 28px 56px;
                }
                header {
                  border-bottom: 1px solid var(--border);
                  margin-bottom: 28px;
                  padding-bottom: 22px;
                }
                h1, h2, p { margin-top: 0; }
                h1 {
                  font-size: 30px;
                  line-height: 1.18;
                  margin-bottom: 10px;
                }
                h2 {
                  font-size: 18px;
                  margin-bottom: 14px;
                }
                .muted { color: var(--muted); }
                .grid {
                  display: grid;
                  grid-template-columns: repeat(auto-fit, minmax(190px, 1fr));
                  gap: 14px;
                  margin: 18px 0 28px;
                }
                .card {
                  background: var(--surface);
                  border: 1px solid var(--border);
                  border-radius: 10px;
                  padding: 16px 18px;
                }
                .label {
                  color: var(--muted);
                  font-size: 13px;
                  margin-bottom: 8px;
                }
                .value {
                  font-size: 23px;
                  font-weight: 700;
                }
                .section {
                  background: var(--surface-soft);
                  border: 1px solid var(--border);
                  border-radius: 12px;
                  margin-top: 18px;
                  padding: 20px;
                }
                table {
                  border-collapse: collapse;
                  width: 100%;
                }
                th, td {
                  border-bottom: 1px solid var(--border);
                  padding: 12px 8px;
                  text-align: left;
                }
                th {
                  color: var(--muted);
                  font-weight: 600;
                }
                tr:last-child td { border-bottom: 0; }
                .table-scroll {
                  overflow-x: auto;
                }
                .detail-table {
                  min-width: 860px;
                }
                .detail-table th,
                .detail-table td {
                  white-space: nowrap;
                }
                .detail-table td:last-child {
                  white-space: normal;
                  min-width: 220px;
                }
                .recommendation {
                  border-left: 4px solid var(--accent);
                  padding: 12px 14px;
                  background: rgba(76, 148, 255, 0.1);
                  border-radius: 8px;
                }
                .warning {
                  color: var(--warning);
                  font-weight: 700;
                }
              </style>
            </head>
            <body>
              <main>
                <header>
                  <h1>Polly Oturum Raporu</h1>
                  <p class="muted">{{Html(statistics.SessionName)}} için üretilen analiz raporu</p>
                </header>

                <section class="grid" aria-label="Özet metrikler">
                  {{MetricCard("Toplam olay", statistics.TotalEventCount.ToString("N0", TurkishCulture))}}
                  {{MetricCard("Toplam süre", FormatDuration(statistics.TotalDurationMs))}}
                  {{MetricCard("Mouse hareketi", statistics.MouseMoveCount.ToString("N0", TurkishCulture))}}
                  {{MetricCard("Optimizasyon adayı", statistics.OptimizationCandidateCount.ToString("N0", TurkishCulture))}}
                </section>

                <section class="section">
                  <h2>Oturum Bilgisi</h2>
                  <table>
                    <tbody>
                      {{TableRow("Makro", statistics.SessionName)}}
                      {{TableRow("Dosya", statistics.FileName)}}
                      {{TableRow("Dosya yolu", statistics.FilePath)}}
                      {{TableRow("Kayıt tarihi", FormatDateTime(statistics.CreatedAtLocal))}}
                      {{TableRow("Rapor tarihi", FormatDateTime(statistics.GeneratedAtLocal))}}
                    </tbody>
                  </table>
                </section>

                <section class="section">
                  <h2>Olay Dağılımı</h2>
                  <table>
                    <tbody>
                      {{TableRow("Klavye olayları", statistics.KeyboardEventCount.ToString("N0", TurkishCulture))}}
                      {{TableRow("Mouse olayları", statistics.MouseEventCount.ToString("N0", TurkishCulture))}}
                      {{TableRow("Mouse hareketleri", statistics.MouseMoveCount.ToString("N0", TurkishCulture))}}
                      {{TableRow("Tıklama olayları", statistics.MouseClickCount.ToString("N0", TurkishCulture))}}
                      {{TableRow("Mouse wheel olayları", statistics.MouseWheelCount.ToString("N0", TurkishCulture))}}
                      {{TableRow("Sistem olayları", statistics.SystemEventCount.ToString("N0", TurkishCulture))}}
                    </tbody>
                  </table>
                </section>

                <section class="section">
                  <h2>Analiz</h2>
                  <table>
                    <tbody>
                      {{TableRow("Ortalama gecikme", FormatMilliseconds(statistics.AverageDelayMs))}}
                      {{TableRow("En uzun bekleme", FormatDuration(statistics.LongestDelayMs))}}
                      {{TableRow("Uzun bekleme", statistics.LongDelayCount.ToString("N0", TurkishCulture))}}
                      {{TableRow("Eksik veya hatalı olay", statistics.InvalidOrIncompleteCount.ToString("N0", TurkishCulture))}}
                      {{TableRow("Optimizasyon aday oranı", statistics.OptimizationCandidateRate.ToString("0.#", TurkishCulture) + "%")}}
                    </tbody>
                  </table>
                </section>

                <section class="section">
                  <h2>Öneri</h2>
                  <p class="recommendation">{{Html(recommendation)}}</p>
                </section>

                <section class="section">
                  <h2>Olay Detayları</h2>
                  <div class="table-scroll">
                    <table class="detail-table">
                      <thead>
                        <tr>
                          <th>#</th>
                          <th>Zaman</th>
                          <th>Tür</th>
                          <th>Aksiyon</th>
                          <th>Konum</th>
                          <th>Gecikme</th>
                          <th>Detay</th>
                        </tr>
                      </thead>
                      <tbody>
                        {{BuildHtmlEventRows(session.Events)}}
                      </tbody>
                    </table>
                  </div>
                </section>
              </main>
            </body>
            </html>
            """;
    }

    private static string BuildRecommendation(MacroReportStatistics statistics)
    {
        if (statistics.InvalidOrIncompleteCount > 0)
        {
            return "Bu makroda eksik veya hatalı olaylar var. Oynatmadan önce olay listesini kontrol etmeniz önerilir.";
        }

        if (statistics.OptimizationCandidateCount > 0)
        {
            return FormattableString.Invariant(
                $"Bu makroda {statistics.OptimizationCandidateCount} optimizasyon adayı mouse hareketi bulundu. Optimize Et özelliği ile olay sayısı azaltılabilir.");
        }

        if (statistics.LongDelayCount > 0)
        {
            return "Makroda uzun beklemeler bulunuyor. Oynatma hızı veya gecikme ayarları sunumdan önce kontrol edilebilir.";
        }

        return "Bu makro için kritik bir optimizasyon uyarısı bulunmuyor.";
    }

    private static string MetricCard(string label, string value)
    {
        return $$"""
                  <article class="card">
                    <div class="label">{{Html(label)}}</div>
                    <div class="value">{{Html(value)}}</div>
                  </article>
            """;
    }

    private static string TableRow(string label, string value)
    {
        return $$"""
                      <tr>
                        <th>{{Html(label)}}</th>
                        <td>{{Html(value)}}</td>
                      </tr>
            """;
    }

    private static IEnumerable<string> BuildTextEventRows(IReadOnlyList<MacroEvent> events)
    {
        if (events.Count == 0)
        {
            yield return "Olay yok.";
            yield break;
        }

        int elapsedMs = 0;

        for (int index = 0; index < events.Count; index++)
        {
            MacroEvent macroEvent = events[index];
            elapsedMs += Math.Max(0, macroEvent.DelayMs);

            yield return string.Join(
                " | ",
                FormatRowNumber(index),
                FormatElapsedTime(elapsedMs),
                FormatEventType(macroEvent.EventType),
                FormatAction(macroEvent),
                FormatPosition(macroEvent),
                FormatMilliseconds(macroEvent.DelayMs),
                FormatDetail(macroEvent));
        }
    }

    private static string BuildHtmlEventRows(IReadOnlyList<MacroEvent> events)
    {
        if (events.Count == 0)
        {
            return """
                        <tr>
                          <td colspan="7">Olay yok.</td>
                        </tr>
                """;
        }

        var builder = new StringBuilder();
        int elapsedMs = 0;

        for (int index = 0; index < events.Count; index++)
        {
            MacroEvent macroEvent = events[index];
            elapsedMs += Math.Max(0, macroEvent.DelayMs);
            string rowHtml = $$"""
                        <tr>
                          <td>{{Html(FormatRowNumber(index))}}</td>
                          <td>{{Html(FormatElapsedTime(elapsedMs))}}</td>
                          <td>{{Html(FormatEventType(macroEvent.EventType))}}</td>
                          <td>{{Html(FormatAction(macroEvent))}}</td>
                          <td>{{Html(FormatPosition(macroEvent))}}</td>
                          <td>{{Html(FormatMilliseconds(macroEvent.DelayMs))}}</td>
                          <td>{{Html(FormatDetail(macroEvent))}}</td>
                        </tr>
                """;
            builder.AppendLine(rowHtml);
        }

        return builder.ToString();
    }

    private static string ResolveFileName(string? filePath)
    {
        return string.IsNullOrWhiteSpace(filePath)
            ? "Kaydedilmedi"
            : Path.GetFileName(filePath);
    }

    private static string ResolveFilePath(string? filePath)
    {
        return string.IsNullOrWhiteSpace(filePath)
            ? "Kaydedilmedi"
            : Path.GetFullPath(filePath);
    }

    private static DateTime ToLocalTime(DateTime value)
    {
        return value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime()
            : value.ToLocalTime();
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToString("dd.MM.yyyy HH:mm", TurkishCulture);
    }

    private static string FormatDuration(int milliseconds)
    {
        int normalizedMilliseconds = Math.Max(0, milliseconds);
        if (normalizedMilliseconds < 1000)
        {
            return FormatMilliseconds(normalizedMilliseconds);
        }

        return string.Create(
            TurkishCulture,
            $"{normalizedMilliseconds:N0} ms ({normalizedMilliseconds / 1000d:0.##} sn)");
    }

    private static string FormatMilliseconds(double milliseconds)
    {
        return string.Create(TurkishCulture, $"{Math.Max(0d, milliseconds):0.#} ms");
    }

    private static string FormatRowNumber(int sourceIndex)
    {
        return (sourceIndex + 1).ToString("000", CultureInfo.InvariantCulture);
    }

    private static string FormatElapsedTime(int elapsedMs)
    {
        TimeSpan elapsed = TimeSpan.FromMilliseconds(Math.Max(0, elapsedMs));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}.{elapsed.Milliseconds:000}");
    }

    private static string FormatEventType(MacroEventType eventType)
    {
        return eventType switch
        {
            MacroEventType.Keyboard => "Klavye",
            MacroEventType.Mouse => "Fare",
            MacroEventType.System => "Sistem",
            _ => eventType.ToString()
        };
    }

    private static string FormatAction(MacroEvent macroEvent)
    {
        return macroEvent.EventType switch
        {
            MacroEventType.Keyboard => macroEvent.KeyboardActionType.ToString(),
            MacroEventType.Mouse => macroEvent.MouseActionType.ToString(),
            MacroEventType.System => "Sistem",
            _ => "-"
        };
    }

    private static string FormatPosition(MacroEvent macroEvent)
    {
        return macroEvent.X.HasValue && macroEvent.Y.HasValue
            ? string.Create(CultureInfo.InvariantCulture, $"X: {macroEvent.X.Value}, Y: {macroEvent.Y.Value}")
            : "-";
    }

    private static string FormatDetail(MacroEvent macroEvent)
    {
        if (!string.IsNullOrWhiteSpace(macroEvent.Description))
        {
            return macroEvent.Description;
        }

        if (macroEvent.EventType == MacroEventType.Keyboard
            && !string.IsNullOrWhiteSpace(macroEvent.KeyName))
        {
            return macroEvent.KeyName;
        }

        if (macroEvent.EventType == MacroEventType.Mouse
            && macroEvent.MouseActionType == MouseActionType.Wheel
            && macroEvent.WheelDelta.HasValue)
        {
            return "Kaydırma değeri: " + macroEvent.WheelDelta.Value.ToString(CultureInfo.InvariantCulture);
        }

        return "-";
    }

    private static bool IsMouseMove(MacroEvent macroEvent)
    {
        return macroEvent.EventType == MacroEventType.Mouse
            && macroEvent.MouseActionType == MouseActionType.Move;
    }

    private static bool IsMouseClick(MacroEvent macroEvent)
    {
        return macroEvent.EventType == MacroEventType.Mouse
            && macroEvent.MouseActionType is MouseActionType.LeftDown
                or MouseActionType.LeftUp
                or MouseActionType.RightDown
                or MouseActionType.RightUp
                or MouseActionType.MiddleDown
                or MouseActionType.MiddleUp
                or MouseActionType.DoubleClick;
    }

    private static bool IsMouseWheel(MacroEvent macroEvent)
    {
        return macroEvent.EventType == MacroEventType.Mouse
            && macroEvent.MouseActionType == MouseActionType.Wheel;
    }

    private static string Html(string value)
    {
        return WebUtility.HtmlEncode(value);
    }
}
