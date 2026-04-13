namespace AudioAnalyzer.Models;

public class LoudnessResult
{
    // LUFS values
    public double IntegratedLufs { get; set; }   // Negative, e.g., -10.0
    public double ShortTermLufs { get; set; }      // LRA (Loudness Range), positive, e.g., 5.4
    
    // True Peak in dBTP (can be positive = clipping, or negative = within range)
    public double TruePeak { get; set; }
    
    public bool IsValid => IntegratedLufs < 0 && IntegratedLufs > -70;

    public string IntegratedDisplay => IsValid ? $"{IntegratedLufs:F1}" : "--";
    public string ShortTermDisplay => ShortTermLufs > 0 && ShortTermLufs < 50 ? $"{ShortTermLufs:F1}" : "--";
    public string LraDisplay => ShortTermLufs > 0 && ShortTermLufs < 50 ? $"{ShortTermLufs:F1} LU" : "--";
    public string TruePeakDisplay => TruePeak != 0 ? $"{TruePeak:F1} dBTP" : "--";
}