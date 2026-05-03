#pragma once

#include <JuceHeader.h>
#include "Constants.h"

namespace ToneAndBeats
{

class KeyDetector
{
public:
    KeyResult detect(const std::vector<float>& monoSamples, int sampleRate);
    KeyResult detect(const File& file);
    KeyResult detect(const String& filePath);

private:
    KeyResult detectFromSamples(const std::vector<float>& monoSamples, int sampleRate);

    struct InternalKey { int root; juce::String mode; double score; };
    InternalKey findNote(const std::vector<double>& chroma);

    juce::String noteNames[12] = {"C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"};
};

} // namespace ToneAndBeats