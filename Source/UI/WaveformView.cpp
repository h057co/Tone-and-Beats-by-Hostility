#include "WaveformView.h"
#include "LookAndFeel.h"

namespace ToneAndBeats
{

WaveformView::WaveformView()
    : playbackProgress(0)
{
    setMouseCursor(juce::MouseCursor::PointingHandCursor);

    timeStartLabel.setText("0:00", juce::dontSendNotification);
    timeStartLabel.setFont(juce::FontOptions(10.0f));
    timeStartLabel.setColour(juce::Label::textColourId, HostilityLookAndFeel::getTextMuted());
    timeStartLabel.setJustificationType(juce::Justification::centredLeft);
    addAndMakeVisible(timeStartLabel);

    timeEndLabel.setText("0:00", juce::dontSendNotification);
    timeEndLabel.setFont(juce::FontOptions(10.0f));
    timeEndLabel.setColour(juce::Label::textColourId, HostilityLookAndFeel::getTextMuted());
    timeEndLabel.setJustificationType(juce::Justification::centredRight);
    addAndMakeVisible(timeEndLabel);
}

void WaveformView::setWaveformData(const WaveformData& data)
{
    waveformData = data;
    repaint();
}

void WaveformView::setProgress(double progress)
{
    playbackProgress = progress;
    repaint();
}

void WaveformView::setDuration(double seconds)
{
    int mins = (int)seconds / 60;
    int secs = (int)seconds % 60;
    timeEndLabel.setText(juce::String::formatted("%d:%02d", mins, secs), juce::dontSendNotification);
}

void WaveformView::resized()
{
    auto bounds = getLocalBounds();
    auto timeline = bounds.removeFromBottom(20);
    timeStartLabel.setBounds(timeline.removeFromLeft(50));
    timeEndLabel.setBounds(timeline.removeFromRight(50));
}

void WaveformView::paint(juce::Graphics& g)
{
    auto bounds = getLocalBounds().toFloat().reduced(10.0f);
    bounds.removeFromBottom(15.0f); // Space for timeline

    if (waveformData.waveformPoints.empty())
    {
        g.setColour(HostilityLookAndFeel::getTextMain().withAlpha(0.3f));
        g.setFont(juce::FontOptions(16.0f));
        g.drawText("DRAG AUDIO FILE HERE", bounds, juce::Justification::centred);
        
        g.setColour(HostilityLookAndFeel::getAccent().withAlpha(0.1f));
        g.drawHorizontalLine(static_cast<int>(bounds.getCentreY()), bounds.getX() + 20, bounds.getRight() - 20);
    }
    else
    {
        drawWaveform(g, bounds);
        drawPlayhead(g, bounds);
    }
}

void WaveformView::drawWaveform(juce::Graphics& g, const juce::Rectangle<float>& bounds)
{
    auto& points = waveformData.waveformPoints;
    if (points.empty())
        return;

    auto numPoints = static_cast<int>(points.size());
    auto pointWidth = bounds.getWidth() / numPoints;
    auto centerY = bounds.getCentreY();

    // Horizontal gradient: Active (Accent) to Inactive (Muted)
    juce::ColourGradient gradient(juce::ColourGradient::horizontal(
        HostilityLookAndFeel::getAccent(),
        HostilityLookAndFeel::getTextMuted().withAlpha(0.4f),
        bounds));
    g.setGradientFill(gradient);

    for (int i = 0; i < numPoints; i++)
    {
        auto x = bounds.getX() + i * pointWidth;
        auto minY = centerY + points[i].minValue * bounds.getHeight() * 0.45f;
        auto maxY = centerY + points[i].maxValue * bounds.getHeight() * 0.45f;

        g.fillRect(x, minY, std::max(1.0f, pointWidth - 0.5f), maxY - minY);
    }
    
    // Draw beats
    g.setColour(HostilityLookAndFeel::getTextMuted().withAlpha(0.3f));
    for (auto beatNorm : waveformData.beatPositions)
    {
        auto beatX = bounds.getX() + static_cast<float>(beatNorm) * bounds.getWidth();
        g.drawVerticalLine(juce::roundToInt(beatX), bounds.getY(), bounds.getBottom());
    }
}

void WaveformView::drawPlayhead(juce::Graphics& g, const juce::Rectangle<float>& bounds)
{
    if (playbackProgress <= 0 || playbackProgress > 1)
        return;

    auto x = bounds.getX() + bounds.getWidth() * static_cast<float>(playbackProgress);
    g.setColour(HostilityLookAndFeel::getPlayhead()); // Fixed color (Red)
    g.fillRect(x - 1.0f, bounds.getY(), 2.0f, bounds.getHeight());
}

void WaveformView::mouseDown(const juce::MouseEvent& event)
{
    auto bounds = getLocalBounds().toFloat().reduced(10.0f);
    bounds.removeFromBottom(15.0f);
    if (bounds.contains(event.position))
    {
        isDragging = true;
        playbackProgress = (event.position.x - bounds.getX()) / bounds.getWidth();
        playbackProgress = std::max(0.0, std::min(1.0, playbackProgress));
        if (onSeek) onSeek(playbackProgress);
        repaint();
    }
}

void WaveformView::mouseDrag(const juce::MouseEvent& event)
{
    if (isDragging)
    {
        auto bounds = getLocalBounds().toFloat().reduced(10.0f);
        bounds.removeFromBottom(15.0f);
        playbackProgress = (event.position.x - bounds.getX()) / bounds.getWidth();
        playbackProgress = std::max(0.0, std::min(1.0, playbackProgress));
        if (onSeek) onSeek(playbackProgress);
        repaint();
    }
}

void WaveformView::mouseUp(const juce::MouseEvent& /*event*/)
{
    isDragging = false;
}

} // namespace ToneAndBeats