#include "AudioDataProvider.h"

std::vector<juce::String> AudioDataProvider::getSupportedExtensions()
{
    return {"wav", "aiff", "aif", "flac", "ogg", "mp3"};
}

bool AudioDataProvider::isSupported(const juce::File& file)
{
    auto ext = file.getFileExtension().toLowerCase().replace(".", "");
    auto supported = getSupportedExtensions();
    return std::find(supported.begin(), supported.end(), ext) != supported.end();
}

std::pair<std::vector<float>, int> AudioDataProvider::loadMono(const juce::File& file)
{
    int sampleRate = 0;
    auto buffer = loadAudio(file, sampleRate);
    
    if (buffer.getNumSamples() == 0)
        return { {}, 0 };

    std::vector<float> samples(static_cast<size_t>(buffer.getNumSamples()));
    
    if (buffer.getNumChannels() == 1)
    {
        auto* readPtr = buffer.getReadPointer(0);
        std::copy(readPtr, readPtr + buffer.getNumSamples(), samples.begin());
    }
    else
    {
        auto numSamples = buffer.getNumSamples();
        for (int i = 0; i < numSamples; ++i)
        {
            float sum = 0;
            for (int ch = 0; ch < buffer.getNumChannels(); ++ch)
                sum += buffer.getSample(ch, i);
            
            samples[i] = sum / buffer.getNumChannels();
        }
    }

    return { samples, sampleRate };
}

juce::AudioBuffer<float> AudioDataProvider::loadAudio(const juce::File& file, int& outSampleRate)
{
    juce::AudioFormatManager manager;
    manager.registerBasicFormats();

    std::unique_ptr<juce::AudioFormatReader> reader(manager.createReaderFor(file));

    if (reader == nullptr)
    {
        outSampleRate = 0;
        return juce::AudioBuffer<float>();
    }

    outSampleRate = static_cast<int>(reader->sampleRate);
    int numSamples = static_cast<int>(reader->lengthInSamples);
    juce::AudioBuffer<float> buffer(reader->numChannels, numSamples);

    reader->read(&buffer, 0, numSamples, 0, true, true);

    return buffer;
}