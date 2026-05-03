#pragma once

#include <JuceHeader.h>
#include <vector>

class AudioDataProvider
{
public:
    static std::vector<juce::String> getSupportedExtensions();
    static bool isSupported(const juce::File& file);
    static std::pair<std::vector<float>, int> loadMono(const juce::File& file);
    static juce::AudioBuffer<float> loadAudio(const juce::File& file, int& outSampleRate);
};