#pragma once
#include <juce_audio_basics/juce_audio_basics.h>
#include <juce_dsp/juce_dsp.h>

namespace ToneAndBeats {

class ScalePlayerSource : public juce::AudioSource
{
public:
    ScalePlayerSource();
    ~ScalePlayerSource() override;

    void prepareToPlay (int samplesPerBlockExpected, double sampleRate) override;
    void releaseResources() override;
    void getNextAudioBlock (const juce::AudioSourceChannelInfo& bufferToFill) override;

    void playScale(const juce::String& rootNote, const juce::String& mode, double bpm);
    void stopScale();
    bool getIsPlaying() const { return isPlaying; }

private:
    double currentSampleRate = 44100.0;
    
    // Waveform data for the scale
    juce::AudioBuffer<float> scaleBuffer;
    int readPosition = 0;
    bool isPlaying = false;
    
    // Generates the scale into scaleBuffer
    void generateScale(const juce::String& rootNote, const juce::String& mode, double bpm);
};

} // namespace ToneAndBeats
