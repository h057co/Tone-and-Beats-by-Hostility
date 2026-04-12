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
    public const int BPM_RANGE_MAX = 250;               // Ampliado para trap rápido

    // Transient detection thresholds (reducidos para audio masterizado)
    public const double TRANSIENT_THRESHOLD_LOW = 1.6;  // Era 2.0
    public const double TRANSIENT_THRESHOLD_HI  = 1.4;  // Era 1.8

    // Beat grid fitting tolerance (aumentado para mastering drift)
    public const double HIT_TOLERANCE_SEC = 0.025;      // Era 0.020 → 25ms

    // ── Spectral Flux (NUEVO v1.0.7) ──────────────────────────────────────
    // Usa NAudio.Dsp.FastFourierTransform — sin nuevas dependencias NuGet
    public const int SF_FFT_SIZE  = 1024;   // ~23ms @ 44100Hz (potencia de 2)
    public const int SF_HOP_SIZE  = 512;    // 50% overlap entre frames
    public const int SF_FFT_M     = 10;     // log2(1024) — requerido por NAudio FFT
    public const double SF_ONSET_THRESHOLD  = 0.15;  // Piso mínimo para considerar onset
    public const double SF_ONSET_WINDOW_SEC = 0.050; // Ventana non-max suppression (50ms)
}
