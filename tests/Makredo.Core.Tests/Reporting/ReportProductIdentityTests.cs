using System;
using Makredo.Application.Reporting;
using Makredo.Domain;
using Makredo.Domain.Models;
using Xunit;

namespace Makredo.Core.Tests.Reporting;

public sealed class ReportProductIdentityTests
{
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
