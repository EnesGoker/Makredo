namespace MacroMaster.Domain.Models;

public sealed class PlaybackSettings
{
    public double SpeedMultiplier { get; set; } = 1.0;
    public int RepeatCount { get; set; } = 1;
    public int InitialDelayMs { get; set; }
    public bool LoopIndefinitely { get; set; }
    public bool UseRelativeCoordinates { get; set; }
    public bool SimulationMode { get; set; }
    public bool StopOnError { get; set; } = true;
    public bool PreserveOriginalTiming { get; set; } = true;
}
