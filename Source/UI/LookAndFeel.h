#pragma once
#include <JuceHeader.h>

namespace ToneAndBeats
{

class HostilityLookAndFeel : public juce::LookAndFeel_V4
{
public:
    HostilityLookAndFeel();

    void drawButtonBackground(juce::Graphics& g, juce::Button& button,
                              const juce::Colour& backgroundColour,
                              bool shouldDrawButtonAsHighlighted,
                              bool shouldDrawButtonAsDown) override;

    static juce::Colour getBackground() { return juce::Colour(0xFF1E1E2E); }
    static juce::Colour getSurface()    { return juce::Colour(0xFF2D2D3D); }
    static juce::Colour getAccent()     { return juce::Colour(0xFF4A90D9); } // Blue
    static juce::Colour getAnalyze()    { return juce::Colour(0xFF6A5ACD); } // Purple
    static juce::Colour getTextMain()   { return juce::Colour(0xFFE0E0E0); }
    static juce::Colour getTextMuted()  { return juce::Colour(0xFFA0A0A0); }
    static juce::Colour getPink()       { return juce::Colour(0xFFE070DF); }
    static juce::Colour getRed()        { return juce::Colour(0xFFFF6464); }
    static juce::Colour getPlayhead()   { return juce::Colour(0xFFFF6B6B); }
    static juce::Colour getBorder()     { return juce::Colour(0xFF3D3D4D); }
};

} // namespace ToneAndBeats
