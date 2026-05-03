#pragma once

#include <JuceHeader.h>
#include "Core/AudioDataProvider.h"
#include "Core/BpmDetector.h"
#include "Core/KeyDetector.h"
#include "Core/Loudness.h"
#include "Core/Metadata.h"
#include "Core/WaveformData.h"
#include "Core/ScalePlayerSource.h"
#include "UI/LookAndFeel.h"
#include "UI/WaveformView.h"
#include "UI/PianoScaleView.h"

namespace ToneAndBeats
{

class MainComponent : public juce::Component,
                   public juce::FileDragAndDropTarget,
                   public juce::Button::Listener,
                   public juce::Timer
{
public:
    MainComponent();
    ~MainComponent() override;

    void paint(juce::Graphics& g) override;
    void resized() override;

    bool isInterestedInFileDrag(const juce::StringArray& files) override;
    void fileDragEnter(const juce::StringArray& files, int x, int y) override;
    void fileDragExit(const juce::StringArray& files) override;
    void filesDropped(const juce::StringArray& files, int x, int y) override;

    void buttonClicked(juce::Button* button) override;
    void mouseDown(const juce::MouseEvent& event) override;

private:
    void loadAudioFile(const File& file);
    void analyzeFile();
    void updateUI();
    void timerCallback() override;

    juce::Label titleLabel;
    juce::ImageComponent logoComponent;
    juce::Image logoImage;
    
    juce::Label fileLabel;
    juce::Label formatLabel;

    juce::Label bpmTitleLabel;
    juce::Label bpmValueLabel;
    juce::Label bpmSubLabel;
    
    juce::Label keyTitleLabel;
    juce::Label keyValueLabel;
    juce::Label keySubLabel;
    
    juce::Label lufsTitleLabel;
    juce::Label lufsValueLabel;
    juce::Label lufsSubLabel;
    
    juce::Label lraTitleLabel;
    juce::Label lraValueLabel;
    juce::Label lraSubLabel;
    
    juce::Label tpTitleLabel;
    juce::Label tpValueLabel;
    juce::Label tpSubLabel;

    juce::Label timeLabel;
    juce::Label rangeLabel;
    juce::Label statusLabel;
    juce::HyperlinkButton footerLink;

    std::atomic<double> analysisProgress{0.0};
    double displayProgress = 0.0;
    juce::ProgressBar progressBar{displayProgress};

    juce::TextButton browseButton;
    juce::TextButton analyzeButton;
    juce::TextButton saveButton;
    juce::TextButton aboutButton;
    juce::TextButton keyPlayButton;
    juce::TextButton pianoToggleButton;
    
    PianoScaleView pianoScaleView;
    
    juce::TextButton playButton;
    juce::TextButton pauseButton;
    juce::TextButton stopButton;

    juce::ComboBox bpmRangeSelector;

    WaveformView waveformView;

    std::unique_ptr<juce::FileChooser> fileChooser;

    AudioDataProvider audioProvider;
    BpmDetector bpmDetector;
    KeyDetector keyDetector;
    LoudnessAnalyzer loudnessAnalyzer;

    // Audio Playback
    juce::AudioDeviceManager deviceManager;
    juce::AudioFormatManager formatManager;
    
    juce::AudioBuffer<float> playbackBuffer;
    std::unique_ptr<juce::MemoryAudioSource> memorySource;
    juce::AudioTransportSource transportSource;
    
    // Custom sources for scale playback
    ScalePlayerSource scalePlayerSource;
    juce::MixerAudioSource mixerSource;
    
    juce::AudioSourcePlayer sourcePlayer;

    std::pair<std::vector<float>, int> loadedAudio;
    BpmResult lastBpmResult;
    KeyResult lastKeyResult;
    LoudnessResult lastLoudnessResult;
    WaveformData lastWaveformData;

    File currentFile;
    bool hasResults;
    bool isBpmSwapped = false;
    bool isKeySwapped = false;
    bool isDragging = false;
    juce::String fileFormatInfo = "PCM • 44100 Hz • 16-bit • Stereo";

    // Card bounds for painting
    juce::Rectangle<float> fileCard;
    juce::Rectangle<float> formatCard;
    juce::Rectangle<float> waveformCard;
    juce::Rectangle<float> loudnessCard;
    juce::Rectangle<float> transportCard;
    juce::Rectangle<float> statusCard;
    juce::Rectangle<float> rangeCard;
    juce::Rectangle<float> actionCard;
    juce::Rectangle<float> resultsCard;
};

} // namespace ToneAndBeats
