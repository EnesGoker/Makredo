using System.Diagnostics;
using System.Runtime.InteropServices;
using Makredo.Infrastructure.Persistence;
using Makredo.Windows.Tests.Playback;
using Xunit;

namespace Makredo.Windows.Tests.Startup;

[Collection("Native input")]
public sealed class SingleInstanceIntegrationTests
{
    private const int MinimizeWindow = 6;
    private static readonly TimeSpan StartupDeadline = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ActivationDeadline = TimeSpan.FromSeconds(8);

    [HostedWindowsInputFact]
    public async Task DuplicateLaunchExitsSuccessfullyAndRestoresTheFirstWindow()
    {
        HostedWindowsInputFactAttribute.RequireHostedWindowsCi();
        string workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")
            ?? throw new InvalidOperationException("The hosted CI workspace is required.");
        string executable = Path.Combine(workspace, "artifacts", "bin", "Makredo.WinForms", "Release", "net10.0-windows", "Makredo.exe");
        Assert.True(File.Exists(executable), $"The Windows Release build must provide {executable}.");

        string storagePath = AppStoragePaths.CreateDefault().RootDirectoryPath;
        string legacyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Polly");
        Assert.False(Path.Exists(storagePath), "The process test requires a fresh Makredo data directory on the hosted runner.");
        Assert.False(Path.Exists(legacyPath), "The process test requires a hosted runner without legacy data.");

        try
        {
            using Process primary = StartApp(executable);
            try
            {
                nint firstWindow = 0;
                Assert.True(await WaitUntilAsync(() =>
                {
                    primary.Refresh();
                    if (primary.HasExited)
                    {
                        return false;
                    }

                    firstWindow = primary.MainWindowHandle;
                    return firstWindow != 0 && primary.MainWindowTitle == "Makredo Kontrol Merkezi";
                }, StartupDeadline), "The first Makredo process did not show its main window.");

                Assert.True(ShowWindowAsync(firstWindow, MinimizeWindow), "The first window could not be minimized.");
                Assert.True(await WaitUntilAsync(() => IsIconic(firstWindow), ActivationDeadline),
                    "The first window must be minimized before launching the duplicate.");

                using Process duplicate = StartApp(executable);
                try
                {
                    Assert.True(await WaitUntilAsync(() => duplicate.HasExited, ActivationDeadline),
                        "The duplicate Makredo process remained running.");
                    Assert.Equal(0, duplicate.ExitCode);
                    Assert.False(primary.HasExited, "The first Makredo process must remain running.");
                    Assert.True(await WaitUntilAsync(() => IsWindowVisible(firstWindow) && !IsIconic(firstWindow), ActivationDeadline),
                        "The first Makredo window did not restore after the duplicate launched.");
                }
                finally
                {
                    await StopTestProcessAsync(duplicate);
                }
            }
            finally
            {
                await StopTestProcessAsync(primary);
            }
        }
        finally
        {
            if (Directory.Exists(storagePath))
            {
                Directory.Delete(storagePath, recursive: true);
            }
        }
    }

    private static Process StartApp(string executable) =>
        Process.Start(new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executable)!
        }) ?? throw new InvalidOperationException("Makredo.exe could not be started.");

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        TimeProvider time = TimeProvider.System;
        long startedAt = time.GetTimestamp();
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50), time);
        while (!condition())
        {
            if (time.GetElapsedTime(startedAt) >= timeout)
            {
                return false;
            }

            await timer.WaitForNextTickAsync();
        }

        return true;
    }

    private static async Task StopTestProcessAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            // The test process exited between the check and Kill.
        }

        using var deadline = new CancellationTokenSource(ActivationDeadline, TimeProvider.System);
        await process.WaitForExitAsync(deadline.Token);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(nint window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);
}
