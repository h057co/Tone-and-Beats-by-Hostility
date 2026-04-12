namespace AudioAnalyzer.Services;

public static class DspConstants
{
    // FFT Configuration
    public const int FFT_SIZE = 2048;
    public const int FFT_SIZE_KEY_DETECTION = 16384;
    public const int HOP_SIZE = 512;

    // Frequency Cutoffs (Hz)
    public const double LOW_FREQ_CUTOFF = 200.0;
    public const double TRANSIENT_LOW_BAND = 150.0;
    public const double TRANSIENT_HIGH_BAND_MIN = 2000.0;
    public const double TRANSIENT_HIGH_BAND_MAX = 8000.0;

    // Timing (seconds)
    public const double TRANSIENT_DEAD_TIME = 0.030;
    public const double DUPLICATE_THRESHOLD = 0.015;

    // Energy Thresholds
    public const double ENERGY_THRESHOLD_LOW = 0.5;
    public const double ENERGY_THRESHOLD_HIGH = 0.2;

    // BPM Detection
    public const int BPM_RANGE_MIN = 50;
    public const int BPM_RANGE_MAX = 200;
}
