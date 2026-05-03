#include "Loudness.h"
#include "AudioDataProvider.h"
#include <ebur128.h>
#include <cmath>

namespace ToneAndBeats
{

LoudnessAnalyzer::LoudnessAnalyzer()
{
}

LoudnessResult LoudnessAnalyzer::analyze(const std::vector<float>& monoSamples, int sampleRate)
{
    return analyzeFromSamples(monoSamples, sampleRate);
}

LoudnessResult LoudnessAnalyzer::analyze(const File& file)
{
    int rate = 0;
    auto buffer = AudioDataProvider::loadAudio(file, rate);
    return analyze(buffer, rate);
}

LoudnessResult LoudnessAnalyzer::analyze(const juce::AudioBuffer<float>& buffer, int sampleRate)
{
    LoudnessResult result;
    result.integratedLufs = -70.0;
    result.loudnessRange = 0.0;
    result.truePeak = -70.0;

    int numChannels = buffer.getNumChannels();
    int numSamples = buffer.getNumSamples();

    if (numSamples <= 0 || sampleRate <= 0 || numChannels <= 0)
        return result;

    ebur128_state* st = ebur128_init(static_cast<unsigned int>(numChannels), 
                                     static_cast<unsigned long>(sampleRate), 
                                     EBUR128_MODE_I | EBUR128_MODE_LRA | EBUR128_MODE_TRUE_PEAK);

    if (!st)
        return result;

    // Set channel map (standard stereo)
    if (numChannels == 2)
    {
        ebur128_set_channel(st, 0, EBUR128_LEFT);
        ebur128_set_channel(st, 1, EBUR128_RIGHT);
    }

    // Interleave samples
    std::vector<float> interleaved(static_cast<size_t>(numSamples * numChannels));
    for (int i = 0; i < numSamples; ++i)
    {
        for (int ch = 0; ch < numChannels; ++ch)
        {
            interleaved[static_cast<size_t>(i * numChannels + ch)] = buffer.getSample(ch, i);
        }
    }

    int err = ebur128_add_frames_float(st, interleaved.data(), static_cast<size_t>(numSamples));
    
    if (err == EBUR128_SUCCESS)
    {
        double integrated = -70.0;
        ebur128_loudness_global(st, &integrated);
        result.integratedLufs = (integrated < -70.0) ? -70.0 : integrated;

        double lra = 0.0;
        ebur128_loudness_range(st, &lra);
        result.loudnessRange = lra;

        double maxTp = -70.0;
        for (int ch = 0; ch < numChannels; ++ch)
        {
            double tp = 0.0;
            ebur128_true_peak(st, static_cast<unsigned int>(ch), &tp);
            double tpDb = (tp > 0) ? 20.0 * std::log10(tp) : -70.0;
            if (tpDb > maxTp) maxTp = tpDb;
        }
        result.truePeak = maxTp;
    }

    ebur128_destroy(&st);
    return result;
}

LoudnessResult LoudnessAnalyzer::analyze(const String& filePath)
{
    return analyze(File(filePath));
}

LoudnessResult LoudnessAnalyzer::analyzeFromSamples(const std::vector<float>& monoSamples, int sampleRate)
{
    LoudnessResult result;
    result.integratedLufs = -70.0;
    result.loudnessRange = 0.0;
    result.truePeak = -70.0;

    if (monoSamples.empty() || sampleRate <= 0)
        return result;

    // Initialize libebur128 state
    // Mode I (Integrated), LRA (Loudness Range), TRUE_PEAK
    ebur128_state* st = ebur128_init(1, static_cast<unsigned long>(sampleRate), 
                                     EBUR128_MODE_I | EBUR128_MODE_LRA | EBUR128_MODE_TRUE_PEAK);

    if (!st)
        return result;

    // Add frames
    int err = ebur128_add_frames_float(st, monoSamples.data(), monoSamples.size());
    
    if (err == EBUR128_SUCCESS)
    {
        // Get Integrated Loudness
        double integrated = -70.0;
        ebur128_loudness_global(st, &integrated);
        result.integratedLufs = integrated;

        // Get Loudness Range
        double lra = 0.0;
        ebur128_loudness_range(st, &lra);
        result.loudnessRange = lra;

        // Get True Peak (channel 0)
        double tp = 0.0;
        ebur128_true_peak(st, 0, &tp);
        // Convert to dBTP
        result.truePeak = (tp > 0) ? 20.0 * std::log10(tp) : -70.0;
    }

    // Cleanup
    ebur128_destroy(&st);

    return result;
}

} // namespace ToneAndBeats