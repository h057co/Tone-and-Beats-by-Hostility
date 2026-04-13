namespace AudioAnalyzer.Models;

public class WaveformData
{
    public List<double[]> WaveformPoints { get; set; } = new();
    public List<double> BeatPositions { get; set; } = new();
    public List<EnergySection> EnergySections { get; set; } = new();
    public List<TempoChange> TempoChanges { get; set; } = new();
    public double Duration { get; set; }
    public int SampleRate { get; set; }
}

public class EnergySection
{
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public double Energy { get; set; }
    public string EnergyLevel { get; set; } = "low";
    public double EnergyChangePercent { get; set; }
}

public class TempoChange
{
    public double Time { get; set; }
    public double PreviousBpm { get; set; }
    public double NewBpm { get; set; }
    public double ChangeAmount { get; set; }
}
