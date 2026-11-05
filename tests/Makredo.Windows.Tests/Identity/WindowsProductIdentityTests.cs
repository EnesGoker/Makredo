using System;
using System.Windows.Forms;
using Makredo.Application.Services;
using Makredo.Domain;
using Makredo.WinForms;
using Xunit;

namespace Makredo.Windows.Tests.Identity;

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
}
