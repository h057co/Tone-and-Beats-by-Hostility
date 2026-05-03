#pragma once

#include <JuceHeader.h>
#include "Constants.h"

namespace ToneAndBeats
{

class WaveformView : public juce::Component
{
public:
    WaveformView();

    void setWaveformData(const WaveformData& data);
    void setProgress(double progress);
    void setDuration(double seconds);

    void paint(juce::Graphics& g) override;
    void resized() override;

    void mouseDown(const juce::MouseEvent& event) override;
    void mouseDrag(const juce::MouseEvent& event) override;
    void mouseUp(const juce::MouseEvent& event) override;

    std::function<void(double)> onSeek;
    bool isUserDragging() const { return isDragging; }

private:
    WaveformData waveformData;
    double playbackProgress;
    bool isDragging = false;

    juce::Label timeStartLabel;
    juce::Label timeEndLabel;

    void drawWaveform(juce::Graphics& g, const juce::Rectangle<float>& bounds);
    void drawPlayhead(juce::Graphics& g, const juce::Rectangle<float>& bounds);
};

} // namespace ToneAndBeats