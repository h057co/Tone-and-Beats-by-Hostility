#pragma once

#include <JuceHeader.h>
#include <vector>
#include <utility>

namespace ToneAndBeats
{

struct DspConstants
{
    static constexpr int FFT_SIZE = 2048;
    static constexpr int FFT_SIZE_KEY_DETECTION = 16384;
    static constexpr int HOP_SIZE = 512;

    static constexpr double LOW_FREQ_CUTOFF = 200.0;
    static constexpr double TRANSIENT_LOW_BAND = 150.0;
    static constexpr double TRANSIENT_HIGH_BAND_MIN = 2000.0;
    static constexpr double TRANSIENT_HIGH_BAND_MAX = 8000.0;

    static constexpr double TRANSIENT_DEAD_TIME = 0.030;
    static constexpr double DUPLICATE_THRESHOLD = 0.015;

    static constexpr double ENERGY_THRESHOLD_LOW = 0.5;
    static constexpr double ENERGY_THRESHOLD_HIGH = 0.2;

    static constexpr double TRANSIENT_THRESHOLD_LOW = 1.0;
    static constexpr double TRANSIENT_THRESHOLD_HI = 0.4;

    static constexpr double HIT_TOLERANCE_SEC = 0.025;

    static constexpr int SF_FFT_SIZE = 2048;
    static constexpr int SF_HOP_SIZE = 512;
    static constexpr double SF_ONSET_WINDOW_SEC = 0.030;

    static constexpr int BPM_RANGE_MIN = 50;
    static constexpr int BPM_RANGE_MAX = 200;
};

struct BpmConstants
{
    static constexpr double TRESILLO_RATIO = 1.5;
    static constexpr double TRESILLO_TOLERANCE = 0.08;
    static constexpr double HALF_TIME_RATIO = 0.667;

    static constexpr double HIGH_CONFIDENCE_THRESHOLD = 0.85;
    static constexpr double MEDIUM_CONFIDENCE_THRESHOLD = 0.65;
    static constexpr double MIN_CONFIDENCE_THRESHOLD = 0.15;

    static constexpr int TRAP_MIN_BPM = 95;
    static constexpr int TRAP_MAX_BPM = 110;
    static constexpr int TRAP_GRID_BPM_THRESHOLD = 150;
    static constexpr double TRAP_CORRECTION_MULTIPLIER = 1.5;

    static constexpr int SMART_THRESHOLD_BPM = 155;
};

enum class BpmRangeProfile
{
    Auto,
    Low_50_100,
    Mid_75_150,
    High_100_200,
    VeryHigh_150_300
};

struct BpmResult
{
    double primaryBpm;
    double alternativeBpm;
};

struct KeyResult
{
    juce::String key;
    juce::String mode;
    juce::String relativeKey;
    juce::String relativeMode;
    double confidence;
};

struct LoudnessResult
{
    double integratedLufs;
    double loudnessRange;
    double truePeak;

    bool isValid() const { return integratedLufs < 0 && integratedLufs > -70; }
    juce::String integratedDisplay() const { return isValid() ? juce::String(integratedLufs, 1) : "--"; }
    juce::String loudnessRangeDisplay() const { return juce::String(loudnessRange, 1) + " LU"; }
    juce::String truePeakDisplay() const { return juce::String(truePeak, 1) + " dBFS"; }
};

struct WaveformPoint
{
    float minValue;
    float maxValue;
};

struct WaveformData
{
    std::vector<WaveformPoint> waveformPoints;
    std::vector<double> beatPositions;
    double duration;
    int sampleRate;
};

struct AnalysisResult
{
    BpmResult bpm;
    KeyResult key;
    LoudnessResult loudness;
    WaveformData waveform;
    bool success;
    juce::String errorMessage;
};

} // namespace ToneAndBeats