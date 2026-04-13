namespace AudioAnalyzer.Models;

public class LoudnessResult
{
    // LUFS values
    /// <summary>
    /// Integrated LUFS (Loudness Units relative to Full Scale) - negative value, e.g., -10.0
    /// </summary>
    public double IntegratedLufs { get; set; }
    
    /// <summary>
    /// LRA (Loudness Range) - positive value representing the range between 10th and 90th percentiles, e.g., 5.4
    /// This is extracted from FFmpeg's 'input_lra' field
    /// </summary>
    public double LoudnessRange { get; set; }
    
    /// <summary>
    /// True Peak in dBFS (Digital Full Scale) - can be positive (clipping) or negative (within range)
    /// </summary>
    public double TruePeak { get; set; }
    
    // Backward compatibility property (deprecated, use LoudnessRange instead)
    public double ShortTermLufs 
    { 
        get => LoudnessRange;
        set => LoudnessRange = value;
    }
    
    public bool IsValid => IntegratedLufs < 0 && IntegratedLufs > -70;

    public string IntegratedDisplay => IsValid ? $"{IntegratedLufs:F1}" : "--";
    
    /// <summary>
    /// Display format for LRA (Loudness Range) in Loudness Units
    /// </summary>
    public string LraDisplay => LoudnessRange > 0 && LoudnessRange < 50 ? $"{LoudnessRange:F1} LU" : "--";
    
    public string TruePeakDisplay => TruePeak != 0 ? $"{TruePeak:F1} dBFS" : "--";
}