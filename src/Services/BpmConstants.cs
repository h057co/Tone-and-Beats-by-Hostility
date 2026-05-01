namespace AudioAnalyzer.Services;

public static class BpmConstants
{
    public const double TRESILLO_RATIO = 1.5;
    public const double TRESILLO_TOLERANCE = 0.08;
    public const double HALF_TIME_RATIO = 0.667;

    public const double HIGH_CONFIDENCE_THRESHOLD = 0.85; // Back to safe original
    public const double MEDIUM_CONFIDENCE_THRESHOLD = 0.65;
    public const double MIN_CONFIDENCE_THRESHOLD = 0.15;

    // Trap/Tresillo Rescue: Focused range
    public const int TRAP_MIN_BPM = 60;                    
    public const int TRAP_MAX_BPM = 105;
    public const int TRAP_GRID_BPM_THRESHOLD = 140;       
    public const double TRAP_CORRECTION_MULTIPLIER = 1.5;

    public const int SMART_THRESHOLD_BPM = 155;
}
