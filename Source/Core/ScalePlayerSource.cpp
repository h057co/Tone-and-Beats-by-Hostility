#include "ScalePlayerSource.h"
#include <cmath>
#include <vector>

namespace ToneAndBeats {

static const juce::StringArray NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
static const std::vector<int> MajorIntervals = { 0, 2, 4, 5, 7, 9, 11, 12 };
static const std::vector<int> MinorIntervals = { 0, 2, 3, 5, 7, 8, 10, 12 };

ScalePlayerSource::ScalePlayerSource() {}
ScalePlayerSource::~ScalePlayerSource() {}

void ScalePlayerSource::prepareToPlay(int /*samplesPerBlockExpected*/, double sampleRate)
{
    currentSampleRate = sampleRate;
}

void ScalePlayerSource::releaseResources()
{
    scaleBuffer.setSize(0, 0);
}

void ScalePlayerSource::getNextAudioBlock(const juce::AudioSourceChannelInfo& bufferToFill)
{
    if (!isPlaying || scaleBuffer.getNumSamples() == 0)
    {
        bufferToFill.clearActiveBufferRegion();
        return;
    }

    int numSamplesToCopy = std::min(bufferToFill.numSamples, scaleBuffer.getNumSamples() - readPosition);
    
    if (numSamplesToCopy > 0)
    {
        for (int channel = 0; channel < bufferToFill.buffer->getNumChannels(); ++channel)
        {
            if (channel < scaleBuffer.getNumChannels())
            {
                bufferToFill.buffer->copyFrom(channel, bufferToFill.startSample, 
                                             scaleBuffer, channel, readPosition, numSamplesToCopy);
            }
            else if (scaleBuffer.getNumChannels() == 1)
            {
                // Copy mono to other channels
                bufferToFill.buffer->copyFrom(channel, bufferToFill.startSample, 
                                             scaleBuffer, 0, readPosition, numSamplesToCopy);
            }
        }
        readPosition += numSamplesToCopy;
    }

    // If there's remaining space in the buffer, clear it
    if (numSamplesToCopy < bufferToFill.numSamples)
    {
        bufferToFill.buffer->clear(bufferToFill.startSample + numSamplesToCopy, 
                                  bufferToFill.numSamples - numSamplesToCopy);
    }

    if (readPosition >= scaleBuffer.getNumSamples())
    {
        isPlaying = false;
        readPosition = 0;
    }
}

void ScalePlayerSource::playScale(const juce::String& rootNote, const juce::String& mode, double bpm)
{
    generateScale(rootNote, mode, bpm);
    readPosition = 0;
    isPlaying = true;
}

void ScalePlayerSource::stopScale()
{
    isPlaying = false;
    readPosition = 0;
}

void ScalePlayerSource::generateScale(const juce::String& rootNote, const juce::String& mode, double bpm)
{
    int rootIndex = NoteNames.indexOf(rootNote.toUpperCase());
    if (rootIndex == -1) return;

    int rootMidi = 60 + rootIndex; // C4 base
    const auto& intervals = mode.equalsIgnoreCase("Minor") ? MinorIntervals : MajorIntervals;
    
    double noteDurationSeconds = 60.0 / (bpm > 0 ? bpm : 120.0);
    int samplesPerNote = static_cast<int>(noteDurationSeconds * currentSampleRate);
    int totalSamples = samplesPerNote * static_cast<int>(intervals.size());
    
    scaleBuffer.setSize(1, totalSamples);
    scaleBuffer.clear();

    auto* writePointer = scaleBuffer.getWritePointer(0);
    
    // 5ms fade in/out to avoid clicks
    int fadeSamples = static_cast<int>(0.005 * currentSampleRate);

    int currentPosition = 0;
    
    for (int interval : intervals)
    {
        int midiNote = rootMidi + interval;
        double frequency = 440.0 * std::pow(2.0, (midiNote - 69) / 12.0);
        double phase = 0.0;
        double phaseIncrement = frequency * juce::MathConstants<double>::twoPi / currentSampleRate;

        for (int i = 0; i < samplesPerNote; ++i)
        {
            float sample = static_cast<float>(std::sin(phase)) * 0.4f; // 0.4 gain
            
            // Apply envelope
            if (i < fadeSamples)
                sample *= (static_cast<float>(i) / fadeSamples);
            else if (i > samplesPerNote - fadeSamples)
                sample *= (static_cast<float>(samplesPerNote - i) / fadeSamples);
                
            writePointer[currentPosition + i] = sample;
            phase += phaseIncrement;
            if (phase >= juce::MathConstants<double>::twoPi)
                phase -= juce::MathConstants<double>::twoPi;
        }
        currentPosition += samplesPerNote;
    }
}

} // namespace ToneAndBeats
