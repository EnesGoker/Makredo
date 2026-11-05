using System.Drawing;
using System.Threading.Tasks;
using Makredo.WinForms.Theme;
using Xunit;

namespace Makredo.Windows.Tests;

public sealed class AppShellLayoutProfileTests
{
    [Fact(DisplayName = "AppShellLayoutProfile keeps expanded layout when the designed shell fits")]
    public Task AppShellLayoutProfile_UsesExpandedLayoutWhenDesignedShellFits()
    {
        AppShellLayoutProfile profile = AppShellLayoutProfileResolver.Resolve(
            new Rectangle(0, 0, 3360, 2100),
            densityScale: 2f);

        Assert.Equal(
            AppShellLayoutMode.Expanded,
            profile.Mode);
        Assert.Equal(
            new Size(2560, 1520),
            profile.PreferredClientSize);
        Assert.True(
            profile.FitRatio >= 0.94f,
            "Expanded layout should only be selected when the designed shell substantially fits.");
        // Expanded chrome preserves the 40px design baseline at 200% density.
        Assert.Equal(
            80,
            profile.Chrome.TitleBarRowHeight);
        Assert.Equal(
            212,
            profile.Chrome.ToolbarRowHeight);
        Assert.Equal(
            18f,
            profile.Main.SummaryColumnPercent);
        Assert.Equal(
            36,
            profile.Main.PrimaryCardContentPadding.Left);

        return Task.CompletedTask;
    }

    [Fact(DisplayName = "AppShellLayoutProfile constrains startup size on small high-DPI working areas")]
    public Task AppShellLayoutProfile_ConstrainsSmallHighDpiWorkingAreas()
    {
        AppShellLayoutProfile profile = AppShellLayoutProfileResolver.Resolve(
            new Rectangle(0, 0, 1920, 1200),
            densityScale: 2f);

        Assert.Equal(
            AppShellLayoutMode.Constrained,
            profile.Mode);
        Assert.True(
            profile.PreferredClientSize.Width <= 1920,
            "Constrained startup width should fit inside the working area.");
        Assert.True(
            profile.PreferredClientSize.Height <= 1200,
            "Constrained startup height should fit inside the working area.");
        Assert.True(
            profile.MinimumClientSize.Width < 1920,
            "The minimum shell size should remain resizable below the constrained working area.");
        AppShellLayoutProfile expandedProfile = AppShellLayoutProfileResolver.Resolve(
            new Rectangle(0, 0, 3360, 2100),
            densityScale: 2f);
        Assert.True(
            profile.Chrome.TitleBarRowHeight < expandedProfile.Chrome.TitleBarRowHeight,
            "Constrained chrome should reclaim vertical space from the title bar.");
        Assert.True(
            profile.Chrome.ToolbarRowHeight < expandedProfile.Chrome.ToolbarRowHeight,
            "Constrained chrome should reclaim vertical space from the toolbar row.");
        Assert.True(
            profile.Chrome.ToolbarControlMinimumHeight <= profile.Chrome.ToolbarRowHeight,
            "Toolbar control minimum height should fit inside the constrained toolbar row.");
        Assert.True(
            profile.Main.SummaryColumnPercent > expandedProfile.Main.SummaryColumnPercent,
            "Constrained main layout should reserve more width for the summary column.");
        Assert.True(
            profile.Main.PrimaryCardContentPadding.Left < expandedProfile.Main.PrimaryCardContentPadding.Left,
            "Constrained main layout should reclaim content padding from the primary cards.");
        Assert.True(
            profile.Main.EventSearchWidth < expandedProfile.Main.EventSearchWidth,
            "Constrained event toolbar should use narrower fixed filter controls.");
        Assert.True(
            profile.Main.SummaryCaptionColumnWidth < expandedProfile.Main.SummaryCaptionColumnWidth,
            "Constrained summary details should reduce fixed caption width before truncating values.");

        return Task.CompletedTask;
    }

    [Theory(DisplayName = "AppShellLayoutProfile keeps toolbar chrome metrics internally consistent")]
    [InlineData(1280, 760, 1f)]
    [InlineData(1280, 760, 1.25f)]
    [InlineData(1280, 760, 1.5f)]
    [InlineData(1280, 760, 2f)]
    [InlineData(1920, 1200, 1f)]
    [InlineData(1920, 1200, 1.25f)]
    [InlineData(1920, 1200, 1.5f)]
    [InlineData(1920, 1200, 2f)]
    [InlineData(3360, 2100, 1f)]
    [InlineData(3360, 2100, 1.25f)]
    [InlineData(3360, 2100, 1.5f)]
    [InlineData(3360, 2100, 2f)]
    public Task AppShellLayoutProfile_KeepsToolbarMetricsConsistent(int width, int height, float densityScale)
    {
        const int dashboardCardVerticalInset = 2;
        AppShellLayoutProfile profile = AppShellLayoutProfileResolver.Resolve(
            new Rectangle(0, 0, width, height),
            densityScale);
        int availableToolbarHeight = profile.Chrome.ToolbarRowHeight
            - profile.Chrome.ToolbarHostMargin.Vertical
            - profile.Chrome.ToolbarContentPadding.Vertical
            - dashboardCardVerticalInset;

        Assert.True(
            profile.Chrome.ToolbarControlMinimumHeight > 0,
            $"Toolbar minimum height should stay positive for {profile.Mode} at {densityScale:0.##}x.");
        Assert.True(
            profile.Chrome.ToolbarControlMinimumHeight <= availableToolbarHeight,
            $"Toolbar minimum height should fit its effective row. Mode={profile.Mode}; Density={densityScale:0.##}; Minimum={profile.Chrome.ToolbarControlMinimumHeight}; Available={availableToolbarHeight}.");

        return Task.CompletedTask;
    }
}
