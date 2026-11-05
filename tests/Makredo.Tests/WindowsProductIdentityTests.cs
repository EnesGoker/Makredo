using System;
using System.Windows.Forms;
using Makredo.Application.Services;
using Makredo.Domain;
using Makredo.Domain.Models;
using Makredo.WinForms;
using Makredo.WinForms.Reporting;
using Xunit;

namespace Makredo.Tests;

public sealed class WindowsProductIdentityTests
{
    [Fact]
    public void GlobalExceptionHandler_UsesProductNameForErrorCaption()
    {
        string? shownCaption = null;
        MessageBoxIcon shownIcon = MessageBoxIcon.None;

        using var registration = new GlobalExceptionHandlerRegistration(
            NullAppLogger.Instance,
            (_, caption, icon) =>
            {
                shownCaption = caption;
                shownIcon = icon;
            },
            registerHandlers: false);

        registration.HandleThreadException(new InvalidOperationException("Identity regression"));

        Assert.Equal(ProductInfo.Name, shownCaption);
        Assert.Equal(MessageBoxIcon.Error, shownIcon);
    }

    [Fact]
    public void TextReport_UsesProductNameForHeading()
    {
        var session = new MacroSession { Name = "Identity regression" };

        string report = MacroReportGenerator.GenerateText(session, filePath: null);

        Assert.StartsWith(ProductInfo.Name + " Oturum Raporu" + Environment.NewLine, report);
    }

    [Fact]
    public void HtmlReport_UsesProductNameForDocumentTitle()
    {
        var session = new MacroSession { Name = "Identity regression" };

        string report = MacroReportGenerator.GenerateHtml(session, filePath: null);

        Assert.Contains(
            "<title>" + ProductInfo.Name + " Oturum Raporu - Identity regression</title>",
            report,
            StringComparison.Ordinal);
    }
}
