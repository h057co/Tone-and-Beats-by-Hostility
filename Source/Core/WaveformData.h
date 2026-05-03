#pragma once

#include <JuceHeader.h>
#include "Constants.h"

namespace ToneAndBeats
{

class WaveformDataExtractor
{
public:
    WaveformDataExtractor();

    static WaveformData extract(const std::vector<float>& monoSamples, int sampleRate, int numPoints = 1000);
};

} // namespace ToneAndBeats