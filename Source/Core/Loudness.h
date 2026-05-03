#pragma once

#include <JuceHeader.h>
#include "Constants.h"

namespace ToneAndBeats
{

class LoudnessAnalyzer
{
public:
    LoudnessAnalyzer();

    LoudnessResult analyze(const std::vector<float>& monoSamples, int sampleRate);
    LoudnessResult analyze(const juce::AudioBuffer<float>& buffer, int sampleRate);
    LoudnessResult analyze(const File& file);
    LoudnessResult analyze(const String& filePath);

private:
    LoudnessResult analyzeFromSamples(const std::vector<float>& monoSamples, int sampleRate);
};

} // namespace ToneAndBeats