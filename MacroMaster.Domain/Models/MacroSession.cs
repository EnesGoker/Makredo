using System.Text.Json.Serialization;

namespace MacroMaster.Domain.Models;

public sealed class MacroSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "New Macro";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public string FormatVersion { get; set; } = MacroSessionFormat.CurrentVersion;

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<MacroEvent> Events { get; } = new();

    public int TotalEventCount => Events.Count;

    public int TotalDurationMs => Events.Sum(e => e.DelayMs);

    public void ReplaceEvents(IEnumerable<MacroEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        List<MacroEvent> materializedEvents = events.ToList();
        Events.Clear();
        Events.AddRange(materializedEvents);
    }
}
