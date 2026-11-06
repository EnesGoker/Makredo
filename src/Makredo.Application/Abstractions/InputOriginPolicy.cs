namespace Makredo.Application.Abstractions;

public static class InputOriginPolicy
{
    public static ulong PlaybackSignature => IntPtr.Size == 8 ? 0xD79A6C53E2184F9BUL : 0x679A6C53UL;

    public static bool ShouldRecord(bool injected, ulong extraInfo, bool recordInjectedInput)
        => extraInfo != PlaybackSignature && (!injected || recordInjectedInput);
}
