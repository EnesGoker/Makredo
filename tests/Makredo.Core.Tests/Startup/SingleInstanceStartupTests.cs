using Makredo.Application.Services;
using Xunit;

namespace Makredo.Core.Tests.Startup;

public sealed class SingleInstanceStartupTests
{
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void SignalsOnlyWhenAnotherInstanceOwnsTheLock(bool acquired, int expectedSignals)
    {
        int attempts = 0;
        int signals = 0;

        bool shouldStart = SingleInstanceStartup.TryStart(
            () =>
            {
                attempts++;
                return acquired;
            },
            () => signals++);

        Assert.Equal(acquired, shouldStart);
        Assert.Equal(1, attempts);
        Assert.Equal(expectedSignals, signals);
    }

    [Fact]
    public void AbandonedLockAllowsStartupWithoutSignaling()
    {
        int signals = 0;

        bool shouldStart = SingleInstanceStartup.TryStart(
            () => throw new AbandonedMutexException(),
            () => signals++);

        Assert.True(shouldStart);
        Assert.Equal(0, signals);
    }

    [Fact]
    public void UnexpectedLockErrorPropagatesWithoutSignaling()
    {
        var expected = new UnauthorizedAccessException("The instance lock could not be accessed.");
        int signals = 0;

        var error = Assert.Throws<UnauthorizedAccessException>(() =>
            SingleInstanceStartup.TryStart(() => throw expected, () => signals++));

        Assert.Same(expected, error);
        Assert.Equal(0, signals);
    }
}
