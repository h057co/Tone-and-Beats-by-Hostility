/*
  ==============================================================================

    PianoScaleView.h
    Created: 2 May 2026
    Author: Antigravity

  ==============================================================================
*/

#pragma once

#include <JuceHeader.h>
#include <vector>

namespace ToneAndBeats
{

class PianoScaleView : public juce::Component
{
public:
    PianoScaleView();
    ~PianoScaleView() override;

    void paint(juce::Graphics& g) override;
    void resized() override;

    void setScale(int rootNote, const juce::String& mode);
    void clearScale();

    std::function<void()> onCloseClicked;

private:
    juce::TextButton closeButton { "X" };
    int root = -1;
    juce::String mode;
    std::vector<int> highlightedKeys;

    // Mapping of white/black keys in one octave (0=C, 1=C#, ...)
    const std::vector<int> whiteKeyIndexes = {0, 2, 4, 5, 7, 9, 11};
    const std::vector<int> blackKeyIndexes = {1, 3, 6, 8, 10};

    JUCE_DECLARE_NON_COPYABLE_WITH_LEAK_DETECTOR(PianoScaleView)
};

} // namespace ToneAndBeats
