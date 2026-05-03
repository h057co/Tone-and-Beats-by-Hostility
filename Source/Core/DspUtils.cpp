#include "DspUtils.h"
#include <algorithm>
#include <cmath>

namespace ToneAndBeats
{

std::vector<float> DspUtils::applyLowPassFilter(const std::vector<float>& samples, int sampleRate, float cutoffHz)
{
    std::vector<float> result = samples;
    juce::IIRFilter filter;
    filter.setCoefficients(juce::IIRCoefficients::makeLowPass(sampleRate, cutoffHz));
    filter.processSamples(result.data(), (int)result.size());
    return result;
}

std::vector<float> DspUtils::applyHighPassFilter(const std::vector<float>& samples, int sampleRate, float cutoffHz)
{
    std::vector<float> result = samples;
    juce::IIRFilter filter;
    filter.setCoefficients(juce::IIRCoefficients::makeHighPass(sampleRate, cutoffHz));
    filter.processSamples(result.data(), (int)result.size());
    return result;
}

std::vector<float> DspUtils::applyBandPassFilter(const std::vector<float>& samples, int sampleRate, float centerFreqHz, float bandwidthHz)
{
    std::vector<float> result = samples;
    juce::IIRFilter filter;
    float Q = centerFreqHz / bandwidthHz;
    filter.setCoefficients(juce::IIRCoefficients::makeBandPass(sampleRate, centerFreqHz, Q));
    filter.processSamples(result.data(), (int)result.size());
    return result;
}

std::vector<float> DspUtils::applyTransientEnhancementFilter(const std::vector<float>& samples)
{
    if (samples.empty()) return {};
    
    std::vector<float> filtered(samples.size());
    filtered[0] = samples[0];
    
    for (size_t i = 1; i < samples.size(); ++i)
    {
        filtered[i] = samples[i] - 0.95f * samples[i - 1];
    }
    
    return filtered;
}

float DspUtils::computeRMS(const float* samples, int numSamples)
{
    if (numSamples <= 0) return 0.0f;
    
    double sum = 0.0;
    for (int i = 0; i < numSamples; ++i)
        sum += samples[i] * samples[i];
        
    return std::sqrt((float)(sum / numSamples));
}

std::vector<float> DspUtils::normalize(const std::vector<float>& samples)
{
    float maxAbs = 0.0f;
    for (auto s : samples)
        maxAbs = std::max(maxAbs, std::abs(s));
        
    if (maxAbs < 1e-6f) return samples;
    
    std::vector<float> result = samples;
    for (auto& s : result)
        s /= maxAbs;
        
    return result;
}

float DspUtils::getAdaptiveThreshold(const std::vector<float>& energy, int currentIndex, int windowSize, float multiplier)
{
    int start = std::max(0, currentIndex - windowSize);
    int end = std::min((int)energy.size(), currentIndex + windowSize);
    
    double sum = 0.0;
    for (int i = start; i < end; ++i)
        sum += energy[i];
        
    float avg = (float)(sum / (end - start));
    return avg * multiplier;
}

} // namespace ToneAndBeats
