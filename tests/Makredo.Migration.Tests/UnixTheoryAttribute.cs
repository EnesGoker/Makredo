using Xunit;

namespace Makredo.Migration.Tests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class UnixTheoryAttribute : TheoryAttribute
{
    public UnixTheoryAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "Symbolic-link creation on Windows requires privileges that are not guaranteed on CI.";
        }
    }
}
