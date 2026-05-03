#include "LookAndFeel.h"

namespace ToneAndBeats
{

HostilityLookAndFeel::HostilityLookAndFeel()
{
    setColour(juce::ResizableWindow::backgroundColourId, getBackground());
    setColour(juce::TextButton::buttonColourId, getAccent());
    setColour(juce::TextButton::textColourOffId, juce::Colours::white);
    setColour(juce::Label::textColourId, getTextMain());
    setColour(juce::ComboBox::backgroundColourId, getBackground());
    setColour(juce::ComboBox::textColourId, getTextMain());
    setColour(juce::ComboBox::outlineColourId, getAccent());
}

void HostilityLookAndFeel::drawButtonBackground(
    juce::Graphics& g, juce::Button& button, const juce::Colour& backgroundColour,
    bool shouldDrawButtonAsHighlighted, bool shouldDrawButtonAsDown)
{
    auto bounds = button.getLocalBounds().toFloat().reduced(0.5f);
    auto baseColor = backgroundColour;

    if (shouldDrawButtonAsDown)
        baseColor = baseColor.darker(0.2f);
    else if (shouldDrawButtonAsHighlighted)
        baseColor = baseColor.brighter(0.1f);

    g.setColour(baseColor);
    g.fillRoundedRectangle(bounds, 6.0f);

    // Dibujar borde si el fondo es el de superficie (estilo botón Save)
    if (backgroundColour == getSurface())
    {
        g.setColour(getBorder());
        g.drawRoundedRectangle(bounds, 6.0f, 1.0f);
    }
}

} // namespace ToneAndBeats
