namespace Makredo.Application.Services;

internal static class SingleInstanceStartup
{
    internal static bool TryStart(Func<bool> tryAcquire, Action signalExisting)
    {
        ArgumentNullException.ThrowIfNull(tryAcquire);
        ArgumentNullException.ThrowIfNull(signalExisting);

        try
        {
            if (tryAcquire())
            {
                return true;
            }
        }
        catch (AbandonedMutexException)
        {
            // The current thread owns an abandoned mutex after WaitOne throws.
            return true;
        }

        signalExisting();
        return false;
    }
}
