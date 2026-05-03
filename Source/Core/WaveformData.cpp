#include "WaveformData.h"

namespace ToneAndBeats
{

WaveformDataExtractor::WaveformDataExtractor()
{
}

WaveformData WaveformDataExtractor::extract(const std::vector<float>& monoSamples, int sampleRate, int numPoints)
{
    WaveformData data;
    data.sampleRate = sampleRate;
    data.duration = monoSamples.size() / static_cast<double>(sampleRate);

    if (monoSamples.empty())
        return data;

    auto samplesPerPoint = monoSamples.size() / numPoints;
    if (samplesPerPoint < 1) samplesPerPoint = 1;

    for (int i = 0; i < numPoints; i++)
    {
        auto start = static_cast<size_t>(i * samplesPerPoint);
        auto end = std::min(start + static_cast<size_t>(samplesPerPoint), monoSamples.size());

        if (start >= monoSamples.size())
            break;

        float minVal = 1.0f;
        float maxVal = -1.0f;

        for (auto j = start; j < end; j++)
        {
            if (monoSamples[j] < minVal) minVal = monoSamples[j];
            if (monoSamples[j] > maxVal) maxVal = monoSamples[j];
        }

        data.waveformPoints.push_back({minVal, maxVal});
    }

    return data;
}

} // namespace ToneAndBeats