namespace Makredo.Infrastructure.Migration;

internal static class MigrationStagingCleanup
{
    private static readonly TimeSpan MinimumAge = TimeSpan.FromHours(24);

    internal static void RemoveAbandonedDirectories(
        string sourcePath,
        string destinationPath,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        string parentPath = Path.GetDirectoryName(destinationPath)!;
        string prefix = $".{Path.GetFileName(destinationPath)}.migration-";
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        bool protectedTreesChecked = false;

        try
        {
            foreach (string candidatePath in Directory.EnumerateDirectories(parentPath))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = Path.GetFileName(candidatePath);
                // Be conservative on case-insensitive Unix volumes as well as Windows.
                if (string.Equals(candidatePath, sourcePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidatePath, destinationPath, StringComparison.OrdinalIgnoreCase)
                    || name.Length != prefix.Length + 32
                    || !name.StartsWith(prefix, comparison)
                    || !Guid.TryParseExact(name[prefix.Length..], "N", out _))
                {
                    continue;
                }

                if (!IsOldRegularDirectory(candidatePath, timeProvider))
                {
                    continue;
                }

                if (!protectedTreesChecked)
                {
                    // A child link can make a staging-shaped path part of live source/final data.
                    // Avoid walking protected trees at all unless there is a stale candidate.
                    if (!HasNoReparsePoints(sourcePath, cancellationToken)
                        || !HasNoReparsePoints(destinationPath, cancellationToken))
                    {
                        return;
                    }

                    protectedTreesChecked = true;
                }

                TryRemoveCandidate(candidatePath, timeProvider, cancellationToken);
            }
        }
        catch (IOException)
        {
            // Optional cleanup must not prevent a valid migration when its parent is unavailable.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryRemoveCandidate(
        string candidatePath,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!IsOldRegularDirectory(candidatePath, timeProvider))
            {
                return;
            }

            using MigrationStagingLease? lease = MigrationStagingLease.TryAcquireForCleanup(candidatePath);
            if (lease is null)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (IsOldRegularDirectory(candidatePath, timeProvider))
            {
                // Directory.Delete removes child links themselves rather than following their targets.
                Directory.Delete(candidatePath, recursive: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (IOException)
        {
            // A stale attempt may be inaccessible or may already have been removed by another process.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool IsOldRegularDirectory(string path, TimeProvider timeProvider)
    {
        try
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.Directory) == 0
                || (attributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            var lastWriteTime = new DateTimeOffset(Directory.GetLastWriteTimeUtc(path));
            return timeProvider.GetUtcNow() - lastWriteTime > MinimumAge;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool HasNoReparsePoints(string rootPath, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileAttributes rootAttributes;
            try
            {
                rootAttributes = File.GetAttributes(rootPath);
            }
            catch (FileNotFoundException)
            {
                return true;
            }
            catch (DirectoryNotFoundException)
            {
                return true;
            }

            if ((rootAttributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }

            if ((rootAttributes & FileAttributes.Directory) == 0)
            {
                return true;
            }

            Stack<string> pendingDirectories = new();
            pendingDirectories.Push(rootPath);
            while (pendingDirectories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string directory = pendingDirectories.Pop();
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }

                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    FileAttributes attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        return false;
                    }

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        pendingDirectories.Push(entry);
                    }
                }
            }

            return true;
        }
        catch (IOException)
        {
            // Unknown traversal state must never authorize deletion of potentially aliased data.
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
