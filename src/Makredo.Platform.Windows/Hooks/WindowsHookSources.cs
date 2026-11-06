using Makredo.Application.Abstractions;
using Makredo.Application.Services;

namespace Makredo.Platform.Windows.Hooks;

public static class WindowsHookSources
{
    public static (WindowsKeyboardHookSource Keyboard, WindowsMouseHookSource Mouse) Create(IAppLogger? logger = null)
    {
        IAppLogger resolvedLogger = logger ?? NullAppLogger.Instance;
        var owner = new WindowsHookThread(resolvedLogger);
        return (new WindowsKeyboardHookSource(owner, resolvedLogger), new WindowsMouseHookSource(owner, resolvedLogger));
    }
}
