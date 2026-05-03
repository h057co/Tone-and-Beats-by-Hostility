#include "KeyDetector.h"
#include "AudioDataProvider.h"
#include <cmath>

namespace ToneAndBeats
{

KeyResult KeyDetector::detect(const std::vector<float>& monoSamples, int sampleRate)
{
    return detectFromSamples(monoSamples, sampleRate);
}

KeyResult KeyDetector::detect(const File& file)
{
    AudioDataProvider provider;
    auto [samples, rate] = provider.loadMono(file);
    return detectFromSamples(samples, rate);
}

KeyResult KeyDetector::detect(const String& filePath)
{
    return detect(juce::File(filePath));
}

KeyResult KeyDetector::detectFromSamples(const std::vector<float>& monoSamples, int sampleRate)
{
    KeyResult result = {"Unknown", "Unknown", "Unknown", "Unknown", 0.0};

    if (monoSamples.size() < static_cast<size_t>(sampleRate))
        return result;

    const int maxAnalysisSeconds = 30;
    int maxSamples = maxAnalysisSeconds * sampleRate;
    
    std::vector<float> analysisData;
    if (monoSamples.size() > static_cast<size_t>(maxSamples))
    {
        size_t startOffset = (monoSamples.size() - maxSamples) / 2;
        analysisData = std::vector<float>(monoSamples.begin() + startOffset,
                                         monoSamples.begin() + startOffset + maxSamples);
    }
    else
    {
        analysisData = monoSamples;
    }

    const int fftOrder = 14; // 16384 points
    const int fftSize = 1 << fftOrder;
    const int hopSize = fftSize / 2;
    const double a4Freq = 440.0;
    
    juce::dsp::FFT fft(fftOrder);
    juce::dsp::WindowingFunction<float> window(fftSize, juce::dsp::WindowingFunction<float>::hann);
    
    std::vector<double> pcp(12, 0.0);
    std::vector<float> fftData(fftSize * 2, 0.0f);
    int numFrames = 0;

    for (size_t frameStart = 0; frameStart + fftSize < analysisData.size(); frameStart += hopSize)
    {
        std::fill(fftData.begin(), fftData.end(), 0.0f);
        std::copy(analysisData.begin() + frameStart, analysisData.begin() + frameStart + fftSize, fftData.begin());
        window.multiplyWithWindowingTable(fftData.data(), fftSize);
        fft.performFrequencyOnlyForwardTransform(fftData.data());

        // According to report: Use magnitudes (sqrt of power)
        // Note: performFrequencyOnlyForwardTransform in JUCE gives magnitudes.

        for (int pitchClass = 0; pitchClass < 12; pitchClass++)
        {
            double pitchEnergy = 0;
            // Calculate base frequency for this note (relative to A440)
            double c0Freq = a4Freq * std::pow(2.0, (pitchClass - 9) / 12.0);
            
            // Octave wrapping: we want to check multiple octaves of this note
            // The report does it by checking 8 harmonics of the base note.
            for (int harmonic = 1; harmonic <= 8; harmonic++)
            {
                double harmonicFreq = c0Freq * harmonic;
                
                // Bring into a reasonable range if needed or just use as is
                // Actually, the C# code uses c0Freq as the base and checks harmonics.
                // But we should check all octaves of this pitch class in the musical range.
                // Let's follow the C# logic: harmonicFreq = c0Freq * harmonic
                
                while (harmonicFreq < 50.0) harmonicFreq *= 2.0; // Ensure it's in audible range
                if (harmonicFreq > (sampleRate / 2.0)) break;

                int freqBin = static_cast<int>(harmonicFreq * fftSize / sampleRate);
                if (freqBin > 0 && freqBin < (fftSize / 2) - 1)
                {
                    // Spectral Interpolation (mitigates quantization errors)
                    double interpolatedMag = static_cast<double>(fftData[freqBin]) + 
                                            0.5 * (static_cast<double>(fftData[freqBin - 1]) + static_cast<double>(fftData[freqBin + 1]));
                    
                    pitchEnergy += interpolatedMag / static_cast<double>(harmonic);
                }
            }
            pcp[pitchClass] += pitchEnergy;
        }
        numFrames++;
    }

    if (numFrames > 0)
    {
        double totalEnergy = 0;
        for (int i = 0; i < 12; i++)
        {
            pcp[i] /= numFrames;
            totalEnergy += pcp[i];
        }
        
        if (totalEnergy > 1e-6)
        {
            for (int i = 0; i < 12; i++)
                pcp[i] /= totalEnergy;
        }

        auto best = findNote(pcp);
        
        result.key = noteNames[best.root];
        result.mode = best.mode;
        result.confidence = std::min(1.0, best.score); // Correlation-based confidence

        // Calculate Relative
        if (best.mode == "Major")
        {
            int relIdx = (best.root + 9) % 12;
            result.relativeKey = noteNames[relIdx];
            result.relativeMode = "Minor";
        }
        else
        {
            int relIdx = (best.root + 3) % 12;
            result.relativeKey = noteNames[relIdx];
            result.relativeMode = "Major";
        }
    }

    return result;
}

KeyDetector::InternalKey KeyDetector::findNote(const std::vector<double>& pcp)
{
    double majorProfile[12] = { 6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88 };
    double minorProfile[12] = { 6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17 };
    
    InternalKey best = { 0, "Major", -1.0 };

    for (int root = 0; root < 12; root++)
    {
        // Rotate PCP to align 'root' with the profiles' 0th index
        std::vector<double> rotatedPcp(12);
        for (int i = 0; i < 12; i++)
            rotatedPcp[i] = pcp[(i + root) % 12];

        // Compare with Major
        double majorCorr = 0, sumA2 = 0, sumB2 = 0;
        for (int i = 0; i < 12; i++)
        {
            majorCorr += rotatedPcp[i] * majorProfile[i];
            sumA2 += rotatedPcp[i] * rotatedPcp[i];
            sumB2 += majorProfile[i] * majorProfile[i];
        }
        double majorSim = (sumA2 > 0 && sumB2 > 0) ? (majorCorr / (std::sqrt(sumA2) * std::sqrt(sumB2))) : 0;

        if (majorSim > best.score)
        {
            best.score = majorSim;
            best.root = root;
            best.mode = "Major";
        }

        // Compare with Minor
        double minorCorr = 0;
        sumB2 = 0; // sumA2 stays same
        for (int i = 0; i < 12; i++)
        {
            minorCorr += rotatedPcp[i] * minorProfile[i];
            sumB2 += minorProfile[i] * minorProfile[i];
        }
        double minorSim = (sumA2 > 0 && sumB2 > 0) ? (minorCorr / (std::sqrt(sumA2) * std::sqrt(sumB2))) : 0;

        if (minorSim > best.score)
        {
            best.score = minorSim;
            best.root = root;
            best.mode = "Minor";
        }
    }

    return best;
}

} // namespace ToneAndBeats