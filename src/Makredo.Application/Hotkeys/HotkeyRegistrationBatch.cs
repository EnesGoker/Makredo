using Makredo.Application.Abstractions;

namespace Makredo.Application.Hotkeys;

public sealed record HotkeyRegistrationRequest(int Id, HotkeyBinding Binding);
public sealed record HotkeyRegistrationFailure(HotkeyBinding Binding, string Reason);

public static class HotkeyRegistrationBatch
{
    public static IReadOnlyList<HotkeyRegistrationFailure> Register(IEnumerable<HotkeyRegistrationRequest> requests, Action<HotkeyRegistrationRequest> register)
    {
        List<HotkeyRegistrationFailure> failures = [];
        foreach (HotkeyRegistrationRequest request in requests)
        {
            try { register(request); }
            catch (Exception exception) { failures.Add(new(request.Binding, exception.Message)); }
        }
        return failures;
    }
}
