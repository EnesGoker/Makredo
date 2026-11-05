namespace Makredo.Infrastructure.Migration;

internal sealed class MigrationStagingLease : IDisposable
{
    private readonly string _lockPath;
    private readonly FileStream _stream;
    private bool _disposed;

    private MigrationStagingLease(string lockPath, FileStream stream)
    {
        _lockPath = lockPath;
        _stream = stream;
    }

    internal static MigrationStagingLease AcquireNew(string stagingPath)
    {
        string lockPath = stagingPath + ".lock";
        return new MigrationStagingLease(
            lockPath,
            new FileStream(lockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None));
    }

    internal static MigrationStagingLease? TryAcquireForCleanup(string stagingPath)
    {
        string lockPath = stagingPath + ".lock";
        try
        {
            if (!IsSafeLockPath(lockPath))
            {
                return null;
            }

            var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try
            {
                // Recheck after opening so an observed link is never owned or removed by cleanup.
                if (!IsSafeLockPath(lockPath))
                {
                    stream.Dispose();
                    return null;
                }

                return new MigrationStagingLease(lockPath, stream);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }
        catch (IOException)
        {
            // An active attempt holds this adjacent file exclusively, even if its directory is old.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _stream.Dispose();
        }
        catch (IOException)
        {
            // Lease housekeeping must not hide the original copy failure.
        }

        try
        {
            File.Delete(_lockPath);
        }
        catch (IOException)
        {
            // A crashed or inaccessible lease can be retried during later cleanup.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool IsSafeLockPath(string lockPath)
    {
        try
        {
            FileAttributes attributes = File.GetAttributes(lockPath);
            return (attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0;
        }
        catch (FileNotFoundException)
        {
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
    }
}
