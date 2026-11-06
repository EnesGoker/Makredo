using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Makredo.CiDiscovery;

internal static class Program
{
    private static readonly JsonSerializerOptions ReportOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [STAThread]
    private static int Main(string[] args)
    {
        // This gate precedes directory creation, UI initialization, and every native probe.
        if (!OperatingSystem.IsWindows()
            || !string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.Ordinal)
            || !string.Equals(Environment.GetEnvironmentVariable("RUNNER_OS"), "Windows", StringComparison.Ordinal)
            || Environment.GetEnvironmentVariable("RUNNER_ARCH") is not ("X64" or "ARM64"))
        {
            Console.Error.WriteLine("Refusing discovery: a GitHub Actions Windows runner is required.");
            return 2;
        }

        if (args.Length != 3 || args[0] != "--ci-discovery" || args[1] != "--output" || string.IsNullOrWhiteSpace(args[2]))
        {
            Console.Error.WriteLine("Explicit opt-in required: --ci-discovery --output <directory>");
            return 2;
        }

        return RunDiscovery(Path.GetFullPath(args[2]));
    }

    private static int RunDiscovery(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        using var process = Process.GetCurrentProcess();
        var report = new DiscoveryReport
        {
            Commit = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? string.Empty,
            RunId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID") ?? string.Empty,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            RunnerArchitecture = Environment.GetEnvironmentVariable("RUNNER_ARCH") ?? string.Empty,
            SessionId = process.SessionId,
            UserInteractive = Environment.UserInteractive,
            StartedAtUtc = TimeProvider.System.GetUtcNow()
        };

        try
        {
            System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25), TimeProvider.System);
            using var form = new DiscoveryForm(report.ProcessArchitecture);
            form.Shown += async (_, _) =>
            {
                try
                {
                    report.Screenshot = await ScreenshotProbe.CaptureAsync(form, outputDirectory, timeout.Token);
                    report.Hooks = await HookProbe.RunAsync(timeout.Token);
                }
                catch (Exception exception)
                {
                    report.FatalError = exception.ToString();
                }
                finally
                {
                    form.Close();
                }
            };
            System.Windows.Forms.Application.Run(form);
        }
        catch (Exception exception)
        {
            report.FatalError = exception.ToString();
        }

        report.CompletedAtUtc = TimeProvider.System.GetUtcNow();
        string json = JsonSerializer.Serialize(report, ReportOptions);
        File.WriteAllText(Path.Combine(outputDirectory, "report.json"), json);
        Console.WriteLine(json);
        return report.FatalError is null ? 0 : 1;
    }
}

internal sealed class DiscoveryReport
{
    public int SchemaVersion { get; init; } = 1;
    public required string Commit { get; init; }
    public required string RunId { get; init; }
    public required string ProcessArchitecture { get; init; }
    public required string RunnerArchitecture { get; init; }
    public int SessionId { get; init; }
    public bool UserInteractive { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public bool ScreenshotSupported => Screenshot?.Supported == true;
    public string? ScreenshotPath => Screenshot?.FileName;
    public ScreenshotProbeResult? Screenshot { get; set; }
    public HookProbeResult? Hooks { get; set; }
    public string? FatalError { get; set; }
}
