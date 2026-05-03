/*
  ==============================================================================

    PianoScaleView.cpp
    Created: 2 May 2026
    Author: Antigravity

  ==============================================================================
*/

#include "PianoScaleView.h"
#include "LookAndFeel.h"

namespace ToneAndBeats
{

PianoScaleView::PianoScaleView()
{
    closeButton.setButtonText(juce::String::fromUTF8("✕"));
    closeButton.setColour(juce::TextButton::buttonColourId, HostilityLookAndFeel::getRed().withAlpha(0.6f));
    closeButton.setColour(juce::TextButton::textColourOffId, juce::Colours::white);
    closeButton.onClick = [this] { if (onCloseClicked) onCloseClicked(); };
    addAndMakeVisible(closeButton);
}

PianoScaleView::~PianoScaleView()
{
}

void PianoScaleView::paint(juce::Graphics& g)
{
    auto bounds = getLocalBounds().toFloat();
    
    // Fondo premium para el overlay (translucido)
    g.setColour(HostilityLookAndFeel::getSurface().withAlpha(0.95f));
    g.fillRoundedRectangle(bounds, 8.0f);
    
    // Borde elegante
    g.setColour(HostilityLookAndFeel::getBorder());
    g.drawRoundedRectangle(bounds.reduced(0.5f), 8.0f, 1.5f);

    auto keyArea = getLocalBounds().reduced(12, 10).toFloat();
    float width = keyArea.getWidth();
    float height = keyArea.getHeight();
    float whiteKeyWidth = width / 7.0f;
    float blackKeyWidth = whiteKeyWidth * 0.6f;
    float blackKeyHeight = height * 0.6f;

    // Draw White Keys
    for (int i = 0; i < 7; ++i)
    {
        juce::Rectangle<float> keyRect(keyArea.getX() + i * whiteKeyWidth, keyArea.getY(), whiteKeyWidth, height);
        
        bool isHighlighted = false;
        int noteIdx = whiteKeyIndexes[i];
        for (int h : highlightedKeys) {
            if (h == noteIdx) {
                isHighlighted = true;
                break;
            }
        }

        g.setColour(isHighlighted ? HostilityLookAndFeel::getAccent().withAlpha(0.6f) : juce::Colours::white);
        g.fillRect(keyRect.reduced(0.5f, 0.0f));
        
        g.setColour(HostilityLookAndFeel::getBorder());
        g.drawRect(keyRect.reduced(0.5f, 0.0f), 1.0f);
        
        // Mark root note with a premium glowing dot
        if (noteIdx == root) {
            auto dotRect = keyRect.withSizeKeepingCentre(8, 8).withY(keyArea.getBottom() - 15);
            g.setColour(HostilityLookAndFeel::getAccent().withAlpha(0.3f));
            g.fillEllipse(dotRect.expanded(3.0f));
            g.setColour(HostilityLookAndFeel::getAccent());
            g.fillEllipse(dotRect);
        }
    }

    // Draw Black Keys
    float blackOffsets[] = { 1, 2, 4, 5, 6 }; // Relative to white key positions
    for (int i = 0; i < 5; ++i)
    {
        float x = keyArea.getX() + (blackOffsets[i] * whiteKeyWidth) - (blackKeyWidth / 2.0f);
        juce::Rectangle<float> keyRect(x, keyArea.getY(), blackKeyWidth, blackKeyHeight);

        bool isHighlighted = false;
        int noteIdx = blackKeyIndexes[i];
        for (int h : highlightedKeys) {
            if (h == noteIdx) {
                isHighlighted = true;
                break;
            }
        }

        g.setColour(isHighlighted ? HostilityLookAndFeel::getAccent().withAlpha(0.8f) : juce::Colours::black);
        g.fillRect(keyRect);
        
        g.setColour(HostilityLookAndFeel::getBorder());
        g.drawRect(keyRect, 1.0f);

        if (noteIdx == root) {
            auto dotRect = keyRect.withSizeKeepingCentre(6, 6).withY(keyArea.getY() + blackKeyHeight - 10);
            g.setColour(HostilityLookAndFeel::getAccent().withAlpha(0.3f));
            g.fillEllipse(dotRect.expanded(2.0f));
            g.setColour(HostilityLookAndFeel::getAccent());
            g.fillEllipse(dotRect);
        }
    }
}

void PianoScaleView::resized()
{
    closeButton.setBounds(getWidth() - 35, 8, 26, 26);
}

void PianoScaleView::setScale(int rootNote, const juce::String& scaleMode)
{
    root = rootNote % 12;
    mode = scaleMode;
    highlightedKeys.clear();

    std::vector<int> intervals;
    if (mode == "Major")
        intervals = {0, 2, 4, 5, 7, 9, 11};
    else // Minor
        intervals = {0, 2, 3, 5, 7, 8, 10};

    for (int interval : intervals)
    {
        highlightedKeys.push_back((root + interval) % 12);
    }

    repaint();
}

void PianoScaleView::clearScale()
{
    root = -1;
    highlightedKeys.clear();
    repaint();
}

} // namespace ToneAndBeats
