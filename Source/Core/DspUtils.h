#pragma once

#include <JuceHeader.h>
#include <vector>

namespace ToneAndBeats
{

class DspUtils
{
public:
    /**
     * Applies a 2nd-order Butterworth low-pass filter to a buffer of samples.
     */
    static std::vector<float> applyLowPassFilter(const std::vector<float>& samples, int sampleRate, float cutoffHz);

    /**
     * Applies a 2nd-order Butterworth high-pass filter to a buffer of samples.
     */
    static std::vector<float> applyHighPassFilter(const std::vector<float>& samples, int sampleRate, float cutoffHz);
    static std::vector<float> applyBandPassFilter(const std::vector<float>& samples, int sampleRate, float centerFreqHz, float bandwidthHz);

    /**
     * Applies a simple pre-emphasis (high-pass) filter to enhance transients.
     * y[i] = x[i] - 0.95 * x[i-1]
     */
    static std::vector<float> applyTransientEnhancementFilter(const std::vector<float>& samples);

    /**
     * Computes the RMS energy of a window of samples.
     */
    static float computeRMS(const float* samples, int numSamples);

    /**
     * Normalizes a buffer of samples to its maximum absolute peak.
     */
    static std::vector<float> normalize(const std::vector<float>& samples);

    /**
     * Finds the local adaptive threshold in a buffer of energy values.
     */
    static float getAdaptiveThreshold(const std::vector<float>& energy, int currentIndex, int windowSize, float multiplier);
};

} // namespace ToneAndBeats
