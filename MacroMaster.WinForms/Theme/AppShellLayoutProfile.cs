using System.Drawing;
using System.Windows.Forms;

namespace MacroMaster.WinForms.Theme;

internal enum AppShellLayoutMode
{
    Expanded,
    Compact,
    Constrained
}

internal readonly record struct AppShellLayoutProfile(
    AppShellLayoutMode Mode,
    Size PreferredClientSize,
    Size MinimumClientSize,
    Padding RootPadding,
    AppShellMainMetrics Main,
    AppShellChromeMetrics Chrome,
    float FitRatio);

internal readonly record struct AppShellMainMetrics(
    float LibraryColumnPercent,
    float PreviewColumnPercent,
    float SummaryColumnPercent,
    Padding MainMargin,
    int CardGap,
    Padding PrimaryCardContentPadding,
    Padding SectionCardContentPadding,
    int SectionHeaderHeight,
    int EventHeaderHeight,
    int EventSearchWidth,
    int EventTypeFilterWidth,
    int EventSmartFilterWidth,
    int EventHeaderGap,
    Padding EventHeaderPadding,
    Padding EventSearchPadding,
    int LibraryHeaderHeight,
    int LibrarySearchHeight,
    int LibraryFooterHeight,
    int LibraryIconButtonWidth,
    int LibraryScrollColumnWidth,
    Padding LibraryHeaderMargin,
    Padding LibraryButtonMargin,
    Padding LibrarySearchMargin,
    Padding LibrarySearchPadding,
    Padding LibraryFooterMargin,
    Padding LibraryFooterPadding,
    float LibraryFooterMacroCaptionPercent,
    float LibraryFooterMacroValuePercent,
    float LibraryFooterEventCaptionPercent,
    float LibraryFooterEventValuePercent,
    int SummaryDetailsHeight,
    int SummaryCaptionColumnWidth,
    Padding SummaryDetailsMargin,
    Padding SummaryDetailsPadding,
    int SummaryValueLeftMargin,
    int SummaryMapTitleHeight,
    Padding SummaryMapMargin);

internal readonly record struct AppShellChromeMetrics(
    int TitleBarRowHeight,
    int TitleBarIconColumnWidth,
    int TitleBarStatusWidth,
    int TitleBarButtonWidth,
    int TitleBarButtonHeight,
    int TitleBarIconSize,
    int TitleBarIconInset,
    int TitleBarIconCornerRadius,
    Padding TitleBarLogoMargin,
    int TitleBarStatusCornerRadius,
    int TitleBarStatusDotSize,
    int TitleBarStatusDotInset,
    int TitleBarStatusGlowInset,
    int TitleBarStatusTextGap,
    int TitleBarStatusTextRightInset,
    Padding TitleBarPadding,
    Padding TitleBarStatusMargin,
    Padding TitleBarButtonMargin,
    int TitleBarButtonCornerRadius,
    int TitleBarButtonIconSize,
    int TitleBarButtonHorizontalInset,
    int TitleBarButtonTopInset,
    int TitleBarButtonBottomInset,
    float TitleBarButtonIconPenWidth,
    int ToolbarRowHeight,
    int ToolbarControlMinimumHeight,
    Padding ToolbarHostMargin,
    Padding ToolbarContentPadding,
    Padding ToolbarControlPadding,
    Padding ToolbarButtonMargin);

internal static class AppShellLayoutProfileResolver
{
    private const int BaselineClientWidth = 1280;
    private const int BaselineClientHeight = 760;
    private const int MinimumClientWidth = 960;
    private const int MinimumClientHeight = 620;
    private const float CompactFitThreshold = 0.94f;
    private const float ConstrainedFitThreshold = 0.78f;
    private const int DashboardCardVerticalInset = 2;

    public static AppShellLayoutProfile Resolve(
        Rectangle workingArea,
        float densityScale)
    {
        float safeDensityScale = Math.Clamp(densityScale, 1f, 3f);
        Size desiredClientSize = ScaleSize(
            BaselineClientWidth,
            BaselineClientHeight,
            safeDensityScale);
        Size minimumClientSize = ScaleSize(
            MinimumClientWidth,
            MinimumClientHeight,
            ResolveMinimumScale(safeDensityScale));
        Size availableClientSize = ResolveAvailableClientSize(
            workingArea,
            desiredClientSize,
            minimumClientSize,
            safeDensityScale);
        Size preferredClientSize = new(
            Math.Clamp(desiredClientSize.Width, minimumClientSize.Width, availableClientSize.Width),
            Math.Clamp(desiredClientSize.Height, minimumClientSize.Height, availableClientSize.Height));

        float fitRatio = Math.Min(
            (float)preferredClientSize.Width / Math.Max(1, desiredClientSize.Width),
            (float)preferredClientSize.Height / Math.Max(1, desiredClientSize.Height));
        AppShellLayoutMode mode = ResolveMode(fitRatio);

        return new AppShellLayoutProfile(
            mode,
            preferredClientSize,
            minimumClientSize,
            ResolveRootPadding(mode, safeDensityScale),
            ResolveMainMetrics(mode, safeDensityScale),
            ResolveChromeMetrics(mode, safeDensityScale),
            fitRatio);
    }

    private static float ResolveMinimumScale(float densityScale)
    {
        return Math.Clamp(densityScale, 1f, 1.25f);
    }

    private static Size ResolveAvailableClientSize(
        Rectangle workingArea,
        Size desiredClientSize,
        Size minimumClientSize,
        float densityScale)
    {
        if (workingArea.Width <= 0 || workingArea.Height <= 0)
        {
            return desiredClientSize;
        }

        int margin = Scale(24, Math.Clamp(densityScale, 1f, 1.5f));
        int availableWidth = Math.Max(minimumClientSize.Width, workingArea.Width - (margin * 2));
        int availableHeight = Math.Max(minimumClientSize.Height, workingArea.Height - (margin * 2));

        return new Size(availableWidth, availableHeight);
    }

    private static AppShellLayoutMode ResolveMode(float fitRatio)
    {
        if (fitRatio >= CompactFitThreshold)
        {
            return AppShellLayoutMode.Expanded;
        }

        return fitRatio >= ConstrainedFitThreshold
            ? AppShellLayoutMode.Compact
            : AppShellLayoutMode.Constrained;
    }

    private static Padding ResolveRootPadding(
        AppShellLayoutMode mode,
        float densityScale)
    {
        float paddingScale = mode switch
        {
            AppShellLayoutMode.Expanded => densityScale,
            AppShellLayoutMode.Compact => Math.Min(densityScale, 1.5f),
            _ => Math.Min(densityScale, 1.25f)
        };

        return new Padding(
            Scale(18, paddingScale),
            0,
            Scale(18, paddingScale),
            Scale(16, paddingScale));
    }

    private static AppShellChromeMetrics ResolveChromeMetrics(
        AppShellLayoutMode mode,
        float densityScale)
    {
        float chromeScale = ResolveChromeScale(mode, densityScale);
        int titleBarRowHeight = Scale(40, chromeScale);
        int toolbarRowHeight = Scale(ResolveToolbarRowHeight(mode), chromeScale);
        Padding toolbarHostMargin = new(
            0,
            Scale(ResolveToolbarHostTopMargin(mode), chromeScale),
            0,
            Scale(4, chromeScale));
        Padding toolbarContentPadding = new(
            Scale(ResolveToolbarContentHorizontalPadding(mode), chromeScale),
            Scale(ResolveToolbarContentVerticalPadding(mode), chromeScale),
            Scale(ResolveToolbarContentHorizontalPadding(mode), chromeScale),
            Scale(ResolveToolbarContentVerticalPadding(mode), chromeScale));

        return new AppShellChromeMetrics(
            TitleBarRowHeight: titleBarRowHeight,
            TitleBarIconColumnWidth: Scale(50, chromeScale),
            TitleBarStatusWidth: Scale(ResolveStatusWidth(mode), chromeScale),
            TitleBarButtonWidth: Scale(ResolveTitleButtonWidth(mode), chromeScale),
            TitleBarButtonHeight: Scale(ResolveTitleButtonHeight(mode), chromeScale),
            TitleBarIconSize: Scale(36, chromeScale),
            TitleBarIconInset: Scale(4, chromeScale),
            TitleBarIconCornerRadius: Scale(5, chromeScale),
            TitleBarLogoMargin: new Padding(
                0,
                0,
                Scale(8, chromeScale),
                Scale(57, chromeScale)),
            TitleBarStatusCornerRadius: Scale(8, chromeScale),
            TitleBarStatusDotSize: Scale(8, chromeScale),
            TitleBarStatusDotInset: Scale(12, chromeScale),
            TitleBarStatusGlowInset: Scale(2, chromeScale),
            TitleBarStatusTextGap: Scale(8, chromeScale),
            TitleBarStatusTextRightInset: Scale(16, chromeScale),
            TitleBarPadding: new Padding(
                0,
                0,
                Scale(ResolveTitleBarRightPadding(mode), chromeScale),
                Scale(6, chromeScale)),
            TitleBarStatusMargin: new Padding(
                Scale(55, chromeScale),
                0,
                Scale(8, chromeScale),
                Scale(6, chromeScale)),
            TitleBarButtonMargin: new Padding(
                Scale(2, chromeScale),
                0,
                0,
                Scale(ResolveTitleButtonBottomMargin(mode), chromeScale)),
            TitleBarButtonCornerRadius: Scale(8, chromeScale),
            TitleBarButtonIconSize: Scale(12, chromeScale),
            TitleBarButtonHorizontalInset: Scale(3, chromeScale),
            TitleBarButtonTopInset: Scale(1, chromeScale),
            TitleBarButtonBottomInset: Scale(5, chromeScale),
            TitleBarButtonIconPenWidth: Math.Max(1.6f, chromeScale * 1.35f),
            ToolbarRowHeight: toolbarRowHeight,
            ToolbarControlMinimumHeight: ResolveToolbarControlMinimumHeight(
                mode,
                chromeScale,
                toolbarRowHeight,
                toolbarHostMargin,
                toolbarContentPadding),
            ToolbarHostMargin: toolbarHostMargin,
            ToolbarContentPadding: toolbarContentPadding,
            ToolbarControlPadding: new Padding(
                Scale(ResolveToolbarControlHorizontalPadding(mode), chromeScale),
                Scale(ResolveToolbarControlVerticalPadding(mode), chromeScale),
                Scale(ResolveToolbarControlHorizontalPadding(mode), chromeScale),
                Scale(ResolveToolbarControlVerticalPadding(mode), chromeScale)),
            ToolbarButtonMargin: new Padding(
                Scale(ResolveToolbarButtonHorizontalMargin(mode), chromeScale),
                Scale(ResolveToolbarButtonVerticalMargin(mode), chromeScale),
                Scale(ResolveToolbarButtonHorizontalMargin(mode), chromeScale),
                Scale(ResolveToolbarButtonVerticalMargin(mode), chromeScale)));
    }

    private static AppShellMainMetrics ResolveMainMetrics(
        AppShellLayoutMode mode,
        float densityScale)
    {
        float mainScale = ResolveMainScale(mode, densityScale);
        (float libraryPercent, float previewPercent, float summaryPercent) = ResolveMainColumns(mode);

        return new AppShellMainMetrics(
            LibraryColumnPercent: libraryPercent,
            PreviewColumnPercent: previewPercent,
            SummaryColumnPercent: summaryPercent,
            MainMargin: new Padding(
                0,
                Scale(ResolveMainTopMargin(mode), mainScale),
                0,
                Scale(ResolveMainBottomMargin(mode), mainScale)),
            CardGap: Scale(ResolveMainCardGap(mode), mainScale),
            PrimaryCardContentPadding: new Padding(Scale(ResolvePrimaryCardPadding(mode), mainScale)),
            SectionCardContentPadding: new Padding(
                Scale(ResolveSectionCardHorizontalPadding(mode), mainScale),
                Scale(ResolveSectionCardTopPadding(mode), mainScale),
                Scale(ResolveSectionCardHorizontalPadding(mode), mainScale),
                Scale(ResolveSectionCardBottomPadding(mode), mainScale)),
            SectionHeaderHeight: Scale(ResolveSectionHeaderHeight(mode), mainScale),
            EventHeaderHeight: Scale(ResolveEventHeaderHeight(mode), mainScale),
            EventSearchWidth: Scale(ResolveEventSearchWidth(mode), mainScale),
            EventTypeFilterWidth: Scale(ResolveEventTypeFilterWidth(mode), mainScale),
            EventSmartFilterWidth: Scale(ResolveEventSmartFilterWidth(mode), mainScale),
            EventHeaderGap: Scale(ResolveEventHeaderGap(mode), mainScale),
            EventHeaderPadding: new Padding(0, 0, 0, Scale(ResolveEventHeaderBottomPadding(mode), mainScale)),
            EventSearchPadding: new Padding(
                Scale(ResolveEventSearchHorizontalPadding(mode), mainScale),
                Scale(ResolveEventSearchTopPadding(mode), mainScale),
                Scale(ResolveEventSearchHorizontalPadding(mode), mainScale),
                Scale(ResolveEventSearchBottomPadding(mode), mainScale)),
            LibraryHeaderHeight: Scale(ResolveLibraryHeaderHeight(mode), mainScale),
            LibrarySearchHeight: Scale(ResolveLibrarySearchHeight(mode), mainScale),
            LibraryFooterHeight: Scale(ResolveLibraryFooterHeight(mode), mainScale),
            LibraryIconButtonWidth: Scale(ResolveLibraryIconButtonWidth(mode), mainScale),
            LibraryScrollColumnWidth: Scale(ResolveLibraryScrollColumnWidth(mode), mainScale),
            LibraryHeaderMargin: new Padding(0, 0, Scale(ResolveLibraryTrailingInset(mode), mainScale), 0),
            LibraryButtonMargin: new Padding(
                Scale(ResolveLibraryButtonLeftMargin(mode), mainScale),
                Scale(ResolveLibraryButtonTopMargin(mode), mainScale),
                0,
                Scale(ResolveLibraryButtonBottomMargin(mode), mainScale)),
            LibrarySearchMargin: new Padding(
                0,
                Scale(ResolveLibrarySearchTopMargin(mode), mainScale),
                Scale(ResolveLibraryTrailingInset(mode), mainScale),
                Scale(ResolveLibrarySearchBottomMargin(mode), mainScale)),
            LibrarySearchPadding: new Padding(
                Scale(ResolveLibrarySearchHorizontalPadding(mode), mainScale),
                Scale(ResolveLibrarySearchTopPadding(mode), mainScale),
                Scale(ResolveLibrarySearchHorizontalPadding(mode), mainScale),
                Scale(ResolveLibrarySearchBottomPadding(mode), mainScale)),
            LibraryFooterMargin: new Padding(
                0,
                Scale(ResolveLibraryFooterTopMargin(mode), mainScale),
                Scale(ResolveLibraryTrailingInset(mode), mainScale),
                0),
            LibraryFooterPadding: new Padding(
                Scale(ResolveLibraryFooterHorizontalPadding(mode), mainScale),
                Scale(ResolveLibraryFooterVerticalPadding(mode), mainScale),
                Scale(ResolveLibraryFooterHorizontalPadding(mode), mainScale),
                Scale(ResolveLibraryFooterVerticalPadding(mode), mainScale)),
            LibraryFooterMacroCaptionPercent: ResolveLibraryFooterMacroCaptionPercent(mode),
            LibraryFooterMacroValuePercent: ResolveLibraryFooterMacroValuePercent(mode),
            LibraryFooterEventCaptionPercent: ResolveLibraryFooterEventCaptionPercent(mode),
            LibraryFooterEventValuePercent: ResolveLibraryFooterEventValuePercent(mode),
            SummaryDetailsHeight: Scale(ResolveSummaryDetailsHeight(mode), mainScale),
            SummaryCaptionColumnWidth: Scale(ResolveSummaryCaptionColumnWidth(mode), mainScale),
            SummaryDetailsMargin: new Padding(0, 0, 0, Scale(ResolveSummaryDetailsBottomMargin(mode), mainScale)),
            SummaryDetailsPadding: new Padding(
                Scale(ResolveSummaryDetailsHorizontalPadding(mode), mainScale),
                Scale(ResolveSummaryDetailsVerticalPadding(mode), mainScale),
                Scale(ResolveSummaryDetailsHorizontalPadding(mode), mainScale),
                Scale(ResolveSummaryDetailsVerticalPadding(mode), mainScale)),
            SummaryValueLeftMargin: Scale(ResolveSummaryValueLeftMargin(mode), mainScale),
            SummaryMapTitleHeight: Scale(ResolveSummaryMapTitleHeight(mode), mainScale),
            SummaryMapMargin: new Padding(0, 0, 0, Scale(ResolveSummaryMapBottomMargin(mode), mainScale)));
    }

    private static float ResolveChromeScale(
        AppShellLayoutMode mode,
        float densityScale)
    {
        return mode switch
        {
            AppShellLayoutMode.Expanded => densityScale,
            AppShellLayoutMode.Compact => Math.Min(densityScale, 1.45f),
            _ => Math.Min(densityScale, 1.25f)
        };
    }

    private static float ResolveMainScale(
        AppShellLayoutMode mode,
        float densityScale)
    {
        return mode switch
        {
            AppShellLayoutMode.Expanded => densityScale,
            AppShellLayoutMode.Compact => Math.Min(densityScale, 1.35f),
            _ => Math.Min(densityScale, 1.15f)
        };
    }

    private static (float Library, float Preview, float Summary) ResolveMainColumns(AppShellLayoutMode mode)
    {
        return mode switch
        {
            AppShellLayoutMode.Constrained => (24f, 56.5f, 19.5f),
            AppShellLayoutMode.Compact => (25f, 56.5f, 18.5f),
            _ => (25.5f, 56.5f, 18f)
        };
    }

    private static int ResolveMainTopMargin(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 6 : 8;
    }

    private static int ResolveMainBottomMargin(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 8 : 10;
    }

    private static int ResolveMainCardGap(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 8 : 12;
    }

    private static int ResolvePrimaryCardPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 14 : 18;
    }

    private static int ResolveSectionCardHorizontalPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 14 : 18;
    }

    private static int ResolveSectionCardTopPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 12 : 16;
    }

    private static int ResolveSectionCardBottomPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 14 : 18;
    }

    private static int ResolveSectionHeaderHeight(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 34 : 38;
    }

    private static int ResolveEventHeaderHeight(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 38 : 42;
    }

    private static int ResolveEventSearchWidth(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 192 : 220;
    }

    private static int ResolveEventTypeFilterWidth(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 110 : 118;
    }

    private static int ResolveEventSmartFilterWidth(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 158 : 172;
    }

    private static int ResolveEventHeaderGap(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 7 : 10;
    }

    private static int ResolveEventHeaderBottomPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 6 : 8;
    }

    private static int ResolveEventSearchHorizontalPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 10 : 12;
    }

    private static int ResolveEventSearchTopPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 5 : 6;
    }

    private static int ResolveEventSearchBottomPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 4 : 5;
    }

    private static int ResolveLibraryHeaderHeight(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 38 : 42;
    }

    private static int ResolveLibrarySearchHeight(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 40 : 44;
    }

    private static int ResolveLibraryFooterHeight(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 44 : 50;
    }

    private static int ResolveLibraryIconButtonWidth(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 34 : 38;
    }

    private static int ResolveLibraryScrollColumnWidth(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 16 : 20;
    }

    private static int ResolveLibraryTrailingInset(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 8 : 12;
    }

    private static int ResolveLibraryButtonLeftMargin(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 6 : 8;
    }

    private static int ResolveLibraryButtonTopMargin(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 3 : 3;
    }

    private static int ResolveLibraryButtonBottomMargin(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 6 : 7;
    }

    private static int ResolveLibrarySearchTopMargin(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 2 : 3;
    }

    private static int ResolveLibrarySearchBottomMargin(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 6 : 8;
    }

    private static int ResolveLibrarySearchHorizontalPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 10 : 14;
    }

    private static int ResolveLibrarySearchTopPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 4 : 5;
    }

    private static int ResolveLibrarySearchBottomPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 3 : 4;
    }

    private static int ResolveLibraryFooterTopMargin(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 6 : 8;
    }

    private static int ResolveLibraryFooterHorizontalPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 8 : 12;
    }

    private static int ResolveLibraryFooterVerticalPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 4 : 5;
    }

    private static float ResolveLibraryFooterMacroCaptionPercent(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 32f : 38f;
    }

    private static float ResolveLibraryFooterMacroValuePercent(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 18f : 12f;
    }

    private static float ResolveLibraryFooterEventCaptionPercent(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 32f : 34f;
    }

    private static float ResolveLibraryFooterEventValuePercent(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 18f : 16f;
    }

    private static int ResolveSummaryDetailsHeight(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 150 : 168;
    }

    private static int ResolveSummaryCaptionColumnWidth(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 58 : 76;
    }

    private static int ResolveSummaryDetailsBottomMargin(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 10 : 14;
    }

    private static int ResolveSummaryDetailsHorizontalPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 10 : 14;
    }

    private static int ResolveSummaryDetailsVerticalPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 9 : 12;
    }

    private static int ResolveSummaryValueLeftMargin(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 2 : 4;
    }

    private static int ResolveSummaryMapTitleHeight(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 24 : 28;
    }

    private static int ResolveSummaryMapBottomMargin(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 8 : 10;
    }

    private static int ResolveStatusWidth(AppShellLayoutMode mode)
    {
        return mode switch
        {
            AppShellLayoutMode.Constrained => 150,
            AppShellLayoutMode.Compact     => 165,
            _                              => 180
        };
    }

    private static int ResolveTitleButtonWidth(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 38 : 44;
    }

    private static int ResolveTitleButtonHeight(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 38 : 41;
    }

    private static int ResolveTitleBarRightPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 6 : 10;
    }

    private static int ResolveTitleButtonBottomMargin(AppShellLayoutMode mode)
    {
        return 55;
    }

    private static int ResolveToolbarRowHeight(AppShellLayoutMode mode)
    {
        return mode switch
        {
            AppShellLayoutMode.Constrained => 88,
            AppShellLayoutMode.Compact => 98,
            _ => 106
        };
    }

    private static int ResolveToolbarControlDesiredHeight(AppShellLayoutMode mode)
    {
        return mode switch
        {
            AppShellLayoutMode.Constrained => 58,
            AppShellLayoutMode.Compact => 66,
            _ => 82
        };
    }

    private static int ResolveToolbarControlMinimumHeight(
        AppShellLayoutMode mode,
        float chromeScale,
        int toolbarRowHeight,
        Padding toolbarHostMargin,
        Padding toolbarContentPadding)
    {
        int desiredHeight = Scale(ResolveToolbarControlDesiredHeight(mode), chromeScale);
        int availableHeight = Math.Max(
            0,
            toolbarRowHeight
            - toolbarHostMargin.Vertical
            - toolbarContentPadding.Vertical
            - DashboardCardVerticalInset);

        return Math.Min(desiredHeight, availableHeight);
    }

    private static int ResolveToolbarHostTopMargin(AppShellLayoutMode mode)
    {
        return mode switch
        {
            AppShellLayoutMode.Constrained => 8,
            AppShellLayoutMode.Compact => 12,
            _ => 16
        };
    }

    private static int ResolveToolbarContentHorizontalPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 12 : 18;
    }

    private static int ResolveToolbarContentVerticalPadding(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Expanded ? 7 : 6;
    }

    private static int ResolveToolbarControlHorizontalPadding(AppShellLayoutMode mode)
    {
        return mode switch
        {
            AppShellLayoutMode.Constrained => 7,
            AppShellLayoutMode.Compact => 9,
            _ => 12
        };
    }

    private static int ResolveToolbarControlVerticalPadding(AppShellLayoutMode mode)
    {
        return mode switch
        {
            AppShellLayoutMode.Constrained => 5,
            AppShellLayoutMode.Compact => 7,
            _ => 10
        };
    }

    private static int ResolveToolbarButtonHorizontalMargin(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Constrained ? 4 : 5;
    }

    private static int ResolveToolbarButtonVerticalMargin(AppShellLayoutMode mode)
    {
        return mode == AppShellLayoutMode.Expanded ? 6 : 4;
    }

    private static Size ScaleSize(
        int width,
        int height,
        float scale)
    {
        return new Size(Scale(width, scale), Scale(height, scale));
    }

    private static int Scale(int value, float scale)
    {
        return (int)Math.Round(value * scale);
    }
}
