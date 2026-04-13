namespace AudioAnalyzer.Services;

public static class BpmConstants
{
    public const double TRESILLO_RATIO = 1.5;
    public const double TRESILLO_TOLERANCE = 0.08;
    public const double HALF_TIME_RATIO = 0.667;

    public const double HIGH_CONFIDENCE_THRESHOLD = 0.85;
    public const double MEDIUM_CONFIDENCE_THRESHOLD = 0.65;
    public const double MIN_CONFIDENCE_THRESHOLD = 0.15;

    // Trap Masterizado: SoundTouch reporta ~101 BPM cuando el audio es 152 BPM (half-time)
    public const int TRAP_MIN_BPM = 95;                    // was 98, expanded lower
    public const int TRAP_MAX_BPM = 110;                   // was 105, expanded upper
    public const int TRAP_GRID_BPM_THRESHOLD = 150;       // was 160, lowered
    public const double TRAP_CORRECTION_MULTIPLIER = 1.5; // was 0.75, now double the ST BPM

    public const int SMART_THRESHOLD_BPM = 155;
}
