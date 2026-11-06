namespace Makredo.Application.Abstractions;

public static class InputOriginPolicy
{
    public const uint PlaybackSignature = 0x679A6C53;

    public static bool ShouldRecord(bool injected, ulong extraInfo, bool recordInjectedInput)
        => extraInfo != PlaybackSignature && (!injected || recordInjectedInput);
}
