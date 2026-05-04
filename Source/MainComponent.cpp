#include "MainComponent.h"
#include <juce_gui_basics/juce_gui_basics.h>
#include "BinaryData.h"

namespace ToneAndBeats
{

static juce::String getCamelotKey(const juce::String& key, const juce::String& mode)
{
    juce::String k = key + " " + mode;
    if (k == "B Major") return "1B";
    if (k == "F# Major") return "2B";
    if (k == "C# Major" || k == "Db Major") return "3B";
    if (k == "G# Major" || k == "Ab Major") return "4B";
    if (k == "D# Major" || k == "Eb Major") return "5B";
    if (k == "A# Major" || k == "Bb Major") return "6B";
    if (k == "F Major") return "7B";
    if (k == "C Major") return "8B";
    if (k == "G Major") return "9B";
    if (k == "D Major") return "10B";
    if (k == "A Major") return "11B";
    if (k == "E Major") return "12B";

    if (k == "G# Minor" || k == "Ab Minor") return "1A";
    if (k == "D# Minor" || k == "Eb Minor") return "2A";
    if (k == "A# Minor" || k == "Bb Minor") return "3A";
    if (k == "F Minor") return "4A";
    if (k == "C Minor") return "5A";
    if (k == "G Minor") return "6A";
    if (k == "D Minor") return "7A";
    if (k == "A Minor") return "8A";
    if (k == "E Minor") return "9A";
    if (k == "B Minor") return "10A";
    if (k == "F# Minor") return "11A";
    if (k == "C# Minor" || k == "Db Minor") return "12A";

    return "";
}

static juce::String formatTime(double seconds)
{
    int mins = static_cast<int>(seconds) / 60;
    int secs = static_cast<int>(seconds) % 60;
    return juce::String::formatted("%02d:%02d", mins, secs);
}

static void configLabel(juce::Label& lbl, float size, juce::Colour color, juce::Justification just, int style = juce::Font::plain)
{
    lbl.setFont(juce::FontOptions(size, style));
    lbl.setColour(juce::Label::textColourId, color);
    lbl.setJustificationType(just);
}

MainComponent::MainComponent()
    : hasResults(false)
{
    // HEADER: Title + Logo
    titleLabel.setText("Tone & Beats", juce::dontSendNotification);
    configLabel(titleLabel, 24.0f, HostilityLookAndFeel::getAccent(), juce::Justification::centred, juce::Font::bold);
    addAndMakeVisible(titleLabel);

    // Cargar Logo desde BinaryData para asegurar portabilidad
    logoImage = juce::ImageFileFormat::loadFrom(BinaryData::HOST_BLANCO_png, BinaryData::HOST_BLANCO_pngSize);
    
    if (logoImage.isValid())
    {
        // Escalar la imagen manualmente un 25% más para asegurar el tamaño solicitado
        logoImage = logoImage.rescaled(static_cast<int>(logoImage.getWidth() * 1.25), 
                                       static_cast<int>(logoImage.getHeight() * 1.25), 
                                       juce::Graphics::highResamplingQuality);
        logoComponent.setImage(logoImage, juce::RectanglePlacement::centred);
        addAndMakeVisible(logoComponent);
    }
    else
    {
        // Fallback si no encuentra el logo
        titleLabel.setText("Tone & Beats (Logo Not Found)", juce::dontSendNotification);
    }

    // Row 1: File Selection
    fileLabel.setText("No file selected", juce::dontSendNotification);
    configLabel(fileLabel, 12.0f, HostilityLookAndFeel::getTextMuted(), juce::Justification::centredLeft);
    addAndMakeVisible(fileLabel);

    browseButton.setButtonText("Browse");
    browseButton.setColour(juce::TextButton::buttonColourId, HostilityLookAndFeel::getAccent());
    browseButton.addListener(this);
    addAndMakeVisible(browseButton);

    // Row 2: Format Summary
    formatLabel.setText("No file loaded", juce::dontSendNotification);
    configLabel(formatLabel, 14.0f, HostilityLookAndFeel::getTextMain(), juce::Justification::centred, juce::Font::bold);
    addAndMakeVisible(formatLabel);

    // Row 3: Waveform (Flexible *)
    addAndMakeVisible(waveformView);

    // Row 4: Loudness
    lufsTitleLabel.setText("LUFS", juce::dontSendNotification);
    configLabel(lufsTitleLabel, 12.0f, HostilityLookAndFeel::getTextMuted(), juce::Justification::centred);
    addAndMakeVisible(lufsTitleLabel);
    lufsValueLabel.setText("--", juce::dontSendNotification);
    configLabel(lufsValueLabel, 20.0f, HostilityLookAndFeel::getTextMain(), juce::Justification::centred, juce::Font::bold);
    addAndMakeVisible(lufsValueLabel);
    lufsSubLabel.setText("Integrated", juce::dontSendNotification);
    configLabel(lufsSubLabel, 9.0f, HostilityLookAndFeel::getTextMuted(), juce::Justification::centred);
    addAndMakeVisible(lufsSubLabel);

    lraTitleLabel.setText("LRA", juce::dontSendNotification);
    configLabel(lraTitleLabel, 12.0f, HostilityLookAndFeel::getTextMuted(), juce::Justification::centred);
    addAndMakeVisible(lraTitleLabel);
    lraValueLabel.setText("--", juce::dontSendNotification);
    configLabel(lraValueLabel, 20.0f, HostilityLookAndFeel::getTextMain(), juce::Justification::centred, juce::Font::bold);
    addAndMakeVisible(lraValueLabel);
    lraSubLabel.setText("LRA", juce::dontSendNotification);
    configLabel(lraSubLabel, 9.0f, HostilityLookAndFeel::getTextMuted(), juce::Justification::centred);
    addAndMakeVisible(lraSubLabel);

    tpTitleLabel.setText("True Peak", juce::dontSendNotification);
    configLabel(tpTitleLabel, 12.0f, HostilityLookAndFeel::getTextMuted(), juce::Justification::centred);
    addAndMakeVisible(tpTitleLabel);
    tpValueLabel.setText("--", juce::dontSendNotification);
    configLabel(tpValueLabel, 20.0f, HostilityLookAndFeel::getTextMain(), juce::Justification::centred, juce::Font::bold);
    addAndMakeVisible(tpValueLabel);
    tpSubLabel.setText("dBFS", juce::dontSendNotification);
    configLabel(tpSubLabel, 9.0f, HostilityLookAndFeel::getTextMuted(), juce::Justification::centred);
    addAndMakeVisible(tpSubLabel);

    // Row 5: Transport
    playButton.setButtonText(juce::String::fromUTF8("\xe2\x96\xb6 Play"));
    playButton.addListener(this);
    addAndMakeVisible(playButton);
    pauseButton.setButtonText(juce::String::fromUTF8("\xe2\x8f\xb8 Pause"));
    pauseButton.addListener(this);
    addAndMakeVisible(pauseButton);
    stopButton.setButtonText(juce::String::fromUTF8("\xe2\x8f\xb9 Stop"));
    stopButton.addListener(this);
    addAndMakeVisible(stopButton);
    timeLabel.setText("00:00 / 00:00", juce::dontSendNotification);
    configLabel(timeLabel, 12.0f, HostilityLookAndFeel::getTextMuted(), juce::Justification::centred);
    addAndMakeVisible(timeLabel);

    // Visual cues for interactive labels
    bpmValueLabel.setMouseCursor(juce::MouseCursor::PointingHandCursor);
    bpmSubLabel.setMouseCursor(juce::MouseCursor::PointingHandCursor);
    keyValueLabel.setMouseCursor(juce::MouseCursor::PointingHandCursor);
    keySubLabel.setMouseCursor(juce::MouseCursor::PointingHandCursor);

    // Row 6: Status
    statusLabel.setText("Ready", juce::dontSendNotification);
    configLabel(statusLabel, 12.0f, HostilityLookAndFeel::getTextMuted(), juce::Justification::centred);
    addAndMakeVisible(statusLabel);

    // Row 7: BPM Range
    rangeLabel.setText("BPM Range:", juce::dontSendNotification);
    configLabel(rangeLabel, 12.0f, HostilityLookAndFeel::getTextMuted(), juce::Justification::centredRight);
    addAndMakeVisible(rangeLabel);
    bpmRangeSelector.addItem(juce::String::fromUTF8("Auto (Recomendado)"), 1);
    bpmRangeSelector.addItem("Low (50 - 100 BPM)", 2);
    bpmRangeSelector.addItem("Mid (75 - 150 BPM)", 3);
    bpmRangeSelector.addItem("High (100 - 200 BPM)", 4);
    bpmRangeSelector.addItem("Very High (150 - 300 BPM)", 5);
    bpmRangeSelector.setSelectedItemIndex(0);
    addAndMakeVisible(bpmRangeSelector);

    // Row 8: Actions
    analyzeButton.setButtonText(juce::String::fromUTF8("\xf0\x9f\x94\x8d Analyze Audio"));
    analyzeButton.setColour(juce::TextButton::buttonColourId, HostilityLookAndFeel::getAnalyze());
    analyzeButton.addListener(this);
    analyzeButton.setEnabled(false);
    addAndMakeVisible(analyzeButton);
    saveButton.setButtonText(juce::String::fromUTF8("\xf0\x9f\x92\xbe Save to Metadata"));
    saveButton.setColour(juce::TextButton::buttonColourId, HostilityLookAndFeel::getAccent());
    saveButton.addListener(this);
    saveButton.setEnabled(false);
    addAndMakeVisible(saveButton);
    
    progressBar.setColour(juce::ProgressBar::foregroundColourId, HostilityLookAndFeel::getAnalyze());
    progressBar.setColour(juce::ProgressBar::backgroundColourId, HostilityLookAndFeel::getBackground());
    addAndMakeVisible(progressBar);

    // Row 9: Results
    bpmTitleLabel.setText("BPM", juce::dontSendNotification);
    configLabel(bpmTitleLabel, 12.0f, HostilityLookAndFeel::getTextMuted(), juce::Justification::centred);
    addAndMakeVisible(bpmTitleLabel);
    bpmValueLabel.setText("--", juce::dontSendNotification);
    configLabel(bpmValueLabel, 32.0f, HostilityLookAndFeel::getAccent(), juce::Justification::centred, juce::Font::bold);
    addAndMakeVisible(bpmValueLabel);
    bpmSubLabel.setText("Detected", juce::dontSendNotification);
    configLabel(bpmSubLabel, 9.0f, HostilityLookAndFeel::getTextMuted(), juce::Justification::centred);
    addAndMakeVisible(bpmSubLabel);

    keyTitleLabel.setText("KEY", juce::dontSendNotification);
    configLabel(keyTitleLabel, 12.0f, HostilityLookAndFeel::getTextMuted(), juce::Justification::centred);
    addAndMakeVisible(keyTitleLabel);
    keyValueLabel.setText("--", juce::dontSendNotification);
    configLabel(keyValueLabel, 32.0f, HostilityLookAndFeel::getPink(), juce::Justification::centred, juce::Font::bold);
    addAndMakeVisible(keyValueLabel);
    keySubLabel.setText("Confidence: 0%", juce::dontSendNotification);
    configLabel(keySubLabel, 9.0f, HostilityLookAndFeel::getTextMuted(), juce::Justification::centred);
    addAndMakeVisible(keySubLabel);

    keyPlayButton.setButtonText(juce::String::fromUTF8("\xe2\x96\xb6"));
    keyPlayButton.setColour(juce::TextButton::buttonColourId, juce::Colours::transparentBlack);
    keyPlayButton.setColour(juce::TextButton::textColourOffId, HostilityLookAndFeel::getPink());
    keyPlayButton.addListener(this);
    addAndMakeVisible(keyPlayButton);

    pianoToggleButton.setButtonText(juce::String::fromUTF8("\xf0\x9f\x8e\xb9")); // Piano Emoji
    pianoToggleButton.setColour(juce::TextButton::buttonColourId, juce::Colours::transparentBlack);
    pianoToggleButton.setColour(juce::TextButton::textColourOffId, HostilityLookAndFeel::getPink());
    pianoToggleButton.setClickingTogglesState(true);
    pianoToggleButton.addListener(this);
    addAndMakeVisible(pianoToggleButton);

    addAndMakeVisible(pianoScaleView);
    pianoScaleView.setVisible(false);
    pianoScaleView.onCloseClicked = [this] {
        pianoToggleButton.setToggleState(false, juce::sendNotification);
    };

    // Row 10: Footer
    aboutButton.setButtonText(juce::String::fromUTF8("Acerca de"));
    aboutButton.setColour(juce::TextButton::buttonColourId, juce::Colours::transparentBlack);
    aboutButton.setColour(juce::TextButton::textColourOffId, HostilityLookAndFeel::getTextMuted());
    aboutButton.addListener(this);
    addAndMakeVisible(aboutButton);

    footerLink.setButtonText("www.hostilitymusic.com");
    footerLink.setURL(juce::URL("https://www.hostilitymusic.com"));
    footerLink.setColour(juce::HyperlinkButton::textColourId, HostilityLookAndFeel::getTextMuted());
    addAndMakeVisible(footerLink);

    // Update Manager Setup - Estilo Overlay flotante
    updateButton.setButtonText(juce::String::fromUTF8("\xf0\x9f\x94\x94 Update")); // Bell Icon
    updateButton.setColour(juce::TextButton::buttonColourId, HostilityLookAndFeel::getAccent().withAlpha(0.9f));
    updateButton.setColour(juce::TextButton::textColourOffId, juce::Colours::white);
    updateButton.setVisible(false);
    updateButton.setAlwaysOnTop(true);
    updateButton.onClick = [this] {
        juce::AlertWindow::showOkCancelBox(juce::AlertWindow::QuestionIcon, "Update Available", 
            "A new version (" + updateManager.getLatestVersion() + ") is available. Do you want to download and install it now?\n\nRelease Notes:\n" + updateManager.getReleaseNotes(),
            "Update", "Later", nullptr,
            juce::ModalCallbackFunction::create([this](int result) {
                if (result != 0) 
                {
                    updateButton.setVisible(false);
                    if (updateProgressBar) 
                    {
                        updateProgressBar->setVisible(true);
                        resized();
                    }
                    updateManager.startUpdateDownload();
                }
            }));
    };
    addChildComponent(updateButton);

    // Barra de progreso de actualización (oculta por defecto)
    updateProgressBar = std::make_unique<juce::ProgressBar>(updateProgress);
    updateProgressBar->setTextToDisplay("Downloading Update...");
    updateProgressBar->setColour(juce::ProgressBar::foregroundColourId, HostilityLookAndFeel::getAccent());
    addChildComponent(updateProgressBar.get());

    updateManager.onUpdateAvailable = [this](juce::String /*version*/, juce::String /*notes*/) {
        updateButton.setVisible(true);
        resized();
    };

    updateManager.onDownloadProgress = [this](float progress) {
        updateProgress = static_cast<double>(progress);
        juce::MessageManager::callAsync([this]() { repaint(); });
    };

    updateManager.onDownloadFinished = [this](bool success, juce::String msg) {
        if (updateProgressBar) updateProgressBar->setVisible(false);
        if (!success)
        {
            updateButton.setVisible(true);
            juce::AlertWindow::showMessageBoxAsync(juce::AlertWindow::WarningIcon, "Update Failed", msg);
            statusLabel.setText("Update failed.", juce::dontSendNotification);
        }
    };

    updateManager.checkForUpdates(true); // Silent check on startup

    setSize(400, 720); // Window limits

    waveformView.onSeek = [this](double progress) {
        if (transportSource.getLengthInSeconds() > 0.0)
            transportSource.setPosition(progress * transportSource.getLengthInSeconds());
    };

    formatManager.registerBasicFormats();
    deviceManager.initialise(0, 2, nullptr, true);
    
    mixerSource.addInputSource(&transportSource, false);
    mixerSource.addInputSource(&scalePlayerSource, false);
    sourcePlayer.setSource(&mixerSource);
    
    deviceManager.addAudioCallback(&sourcePlayer);
    
    // Add listener for key scale playback and BPM/Key swap
    keyValueLabel.addMouseListener(this, false);
    keySubLabel.addMouseListener(this, false);
    bpmValueLabel.addMouseListener(this, false);
    bpmSubLabel.addMouseListener(this, false);
    
    startTimerHz(60);
}

MainComponent::~MainComponent()
{
    stopTimer();
    deviceManager.removeAudioCallback(&sourcePlayer);
    sourcePlayer.setSource(nullptr);
    transportSource.setSource(nullptr);
}

void MainComponent::paint(juce::Graphics& g)
{
    g.fillAll(HostilityLookAndFeel::getBackground());
    auto drawCard = [&](juce::Rectangle<float> bounds) {
        g.setColour(HostilityLookAndFeel::getSurface());
        g.fillRoundedRectangle(bounds, 8.0f);
        g.setColour(HostilityLookAndFeel::getBorder());
        g.drawRoundedRectangle(bounds, 8.0f, 1.0f);
    };

    drawCard(fileCard); drawCard(formatCard); drawCard(waveformCard); drawCard(loudnessCard);
    drawCard(transportCard); drawCard(statusCard); drawCard(rangeCard); drawCard(actionCard); drawCard(resultsCard);

    if (isDragging) {
        g.setColour(HostilityLookAndFeel::getAccent().withAlpha(0.4f));
        g.drawRoundedRectangle(getLocalBounds().toFloat().reduced(4.0f), 8.0f, 3.0f);
    }
}

void MainComponent::resized()
{
    auto bounds = getLocalBounds().reduced(10).toFloat();
    
    // Allocate footer from bottom
    footerLink.setBounds(bounds.removeFromBottom(15).toNearestInt());
    aboutButton.setBounds(bounds.removeFromBottom(25).withSizeKeepingCentre(100, 20).toNearestInt());
    bounds.removeFromBottom(5); // gap before results

    // Standard layout (not affected by piano overlay)
    float bottomTotalHeight = 435.0f;
    auto bottomArea = bounds.removeFromBottom(bottomTotalHeight);

    // Allocate top area
    float topH = 170.0f;
    auto topArea = bounds.removeFromTop(topH);

    // Waveform takes the remaining space (with a visual minimum of 120px)
    waveformCard = bounds.withHeight(std::max(120.0f, bounds.getHeight())).reduced(5, 0);
    waveformView.setBounds(waveformCard.reduced(5).toNearestInt());

    // --- Layout Top Area ---
    titleLabel.setBounds(topArea.removeFromTop(30).toNearestInt());
    logoComponent.setBounds(topArea.removeFromTop(40).toNearestInt());
    
    // Ubicar botón de update como overlay en la esquina superior derecha
    if (updateButton.isVisible())
    {
        updateButton.setBounds(getWidth() - 95, 15, 80, 24);
        updateButton.toFront(false);
    }

    if (updateProgressBar && updateProgressBar->isVisible())
    {
        updateProgressBar->setBounds(getWidth() / 2 - 100, 15, 200, 20);
    }

    topArea.removeFromTop(5);
    
    fileCard = topArea.removeFromTop(40).reduced(5, 0);
    auto fileContent = fileCard.reduced(10, 8); 
    browseButton.setBounds(fileContent.removeFromRight(80).toNearestInt());
    fileLabel.setBounds(fileContent.toNearestInt());
    topArea.removeFromTop(10);
    
    formatCard = topArea.removeFromTop(30).reduced(5, 0);
    formatLabel.setBounds(formatCard.toNearestInt());

    // --- Layout Bottom Area ---
    loudnessCard = bottomArea.removeFromTop(70).reduced(5, 0);
    auto lArea = loudnessCard.reduced(10, 8);
    auto colW = lArea.getWidth() / 3.0f;
    auto lufsCol = lArea.removeFromLeft(colW);
    lufsTitleLabel.setBounds(lufsCol.removeFromTop(18).toNearestInt());
    lufsSubLabel.setBounds(lufsCol.removeFromBottom(12).toNearestInt());
    lufsValueLabel.setBounds(lufsCol.toNearestInt());
    auto lraCol = lArea.removeFromLeft(colW);
    lraTitleLabel.setBounds(lraCol.removeFromTop(18).toNearestInt());
    lraSubLabel.setBounds(lraCol.removeFromBottom(12).toNearestInt());
    lraValueLabel.setBounds(lraCol.toNearestInt());
    auto tpCol = lArea;
    tpTitleLabel.setBounds(tpCol.removeFromTop(18).toNearestInt());
    tpSubLabel.setBounds(tpCol.removeFromBottom(12).toNearestInt());
    tpValueLabel.setBounds(tpCol.toNearestInt());
    bottomArea.removeFromTop(10);

    transportCard = bottomArea.removeFromTop(75).reduced(5, 0);
    auto tArea = transportCard.reduced(10, 8);
    auto btnArea = tArea.removeFromTop(32).withSizeKeepingCentre(260, 30);
    playButton.setBounds(btnArea.removeFromLeft(80).toNearestInt()); btnArea.removeFromLeft(10);
    pauseButton.setBounds(btnArea.removeFromLeft(80).toNearestInt()); btnArea.removeFromLeft(10);
    stopButton.setBounds(btnArea.removeFromLeft(80).toNearestInt());
    timeLabel.setBounds(tArea.withTrimmedTop(5).toNearestInt());
    bottomArea.removeFromTop(10);

    statusCard = bottomArea.removeFromTop(35).reduced(5, 0);
    statusLabel.setBounds(statusCard.toNearestInt());
    bottomArea.removeFromTop(10);

    rangeCard = bottomArea.removeFromTop(40).reduced(5, 0);
    auto rArea = rangeCard.reduced(10, 8);
    rangeLabel.setBounds(rArea.removeFromLeft(100).toNearestInt()); rArea.removeFromLeft(10);
    bpmRangeSelector.setBounds(rArea.toNearestInt());
    bottomArea.removeFromTop(10);

    actionCard = bottomArea.removeFromTop(80).reduced(5, 0);
    auto actArea = actionCard.reduced(10, 8);
    auto pBarArea = actArea.removeFromBottom(5);
    progressBar.setBounds(pBarArea.toNearestInt());
    actArea.removeFromBottom(5);
    float bW = (actArea.getWidth() - 10.0f) * 0.5f;
    analyzeButton.setBounds(actArea.removeFromLeft(bW).toNearestInt());
    actArea.removeFromLeft(10); 
    saveButton.setBounds(actArea.toNearestInt());
    bottomArea.removeFromTop(10);

    resultsCard = bottomArea.removeFromTop(85).reduced(5, 0);
    auto resArea = resultsCard.reduced(10, 8);
    auto rColW = resArea.getWidth() / 2.0f;
    auto bpmCol = resArea.removeFromLeft(rColW);
    bpmTitleLabel.setBounds(bpmCol.removeFromTop(20).toNearestInt());
    bpmSubLabel.setBounds(bpmCol.removeFromBottom(15).toNearestInt());
    bpmValueLabel.setBounds(bpmCol.toNearestInt());
    auto keyCol = resArea;
    auto keyTitleArea = keyCol.removeFromTop(20);
    float labelW = 32.0f; 
    float btnW = 18.0f;  
    float pianoBtnW = 18.0f;
    float hGap = 4.0f;    
    auto centeredHeader = keyTitleArea.withSizeKeepingCentre(labelW + hGap + btnW + hGap + pianoBtnW, 20);
    keyTitleLabel.setBounds(centeredHeader.removeFromLeft(labelW).toNearestInt());
    keyPlayButton.setBounds(centeredHeader.removeFromLeft(btnW + hGap).withTrimmedLeft(hGap).toNearestInt());
    pianoToggleButton.setBounds(centeredHeader.toNearestInt());
    
    keySubLabel.setBounds(keyCol.removeFromBottom(15).toNearestInt());
    keyValueLabel.setBounds(keyCol.toNearestInt());

    // Piano Overlay Logic
    bool pianoVisible = pianoScaleView.isVisible();
    if (pianoVisible)
    {
        // Positioned as an overlay at the bottom, floating over the results/actions
        auto overlayBounds = getLocalBounds().withSizeKeepingCentre(380, 95).withBottom(getLocalBounds().getBottom() - 40);
        pianoScaleView.setBounds(overlayBounds.toNearestInt());
        pianoScaleView.toFront(true);
    }
}

bool MainComponent::isInterestedInFileDrag(const juce::StringArray& files) { return files.size() == 1 && AudioDataProvider::isSupported(juce::File(files[0])); }
void MainComponent::fileDragEnter(const juce::StringArray&, int, int) { isDragging = true; repaint(); }
void MainComponent::fileDragExit(const juce::StringArray&) { isDragging = false; repaint(); }
void MainComponent::filesDropped(const juce::StringArray& files, int, int) { isDragging = false; repaint(); if (files.size() == 1) { juce::File file(files[0]); if (AudioDataProvider::isSupported(file)) loadAudioFile(file); } }

void MainComponent::buttonClicked(juce::Button* button)
{
    if (button == &browseButton) {
        fileChooser = std::make_unique<juce::FileChooser>("Select audio...", juce::File::getSpecialLocation(juce::File::userMusicDirectory), "*.wav;*.mp3;*.aiff;*.aif;*.flac;*.ogg");
        fileChooser->launchAsync(juce::FileBrowserComponent::openMode | juce::FileBrowserComponent::canSelectFiles, [this](const juce::FileChooser& c) {
            juce::File r = c.getResult(); if (r.existsAsFile()) loadAudioFile(r);
        });
    } else if (button == &playButton) transportSource.start();
    else if (button == &pauseButton) transportSource.stop();
    else if (button == &stopButton) { transportSource.stop(); transportSource.setPosition(0.0); }
    else if (button == &analyzeButton) { if (currentFile.exists()) analyzeFile(); }
    else if (button == &aboutButton) {
        auto* dw = new juce::AlertWindow(juce::String::fromUTF8("Acerca de Tone & Beats"), 
                                        juce::String::fromUTF8("Version ") + ProjectInfo::versionString + juce::String::fromUTF8("\nAudio Analysis & Metadata Tool\nDeveloped by Hostility\n\n"
                                        "Si te gusta mi trabajo, puedes apoyarme para seguir mejorando esta herramienta:\n\n"
                                        "Third-Party Licenses:\n"
                                        "- JUCE Framework (GPLv3/Personal)\n"
                                        "- SoundTouch Library (LGPL v2.1)\n"
                                        "- libebur128 (MIT)\n"
                                        "- TagLib (LGPL/MPL)\n\n"
                                        "Código Fuente (GPLv3):\n"
                                        "https://github.com/h057co/Tone-and-Beats-by-Hostility"), 
                                        juce::AlertWindow::NoIcon);
        
        auto customComp = std::make_unique<juce::Component>();
        customComp->setSize(320, 50);
        auto btnArea = customComp->getLocalBounds().reduced(20, 5);
        auto leftBtnArea = btnArea.removeFromLeft(btnArea.getWidth() / 2).reduced(5, 0);
        auto rightBtnArea = btnArea.reduced(5, 0);
        
        auto* kofiBtn = new juce::TextButton("Ko-fi");
        kofiBtn->setBounds(leftBtnArea);
        kofiBtn->onClick = [] { juce::URL("https://ko-fi.com/hostilityme").launchInDefaultBrowser(); };
        customComp->addAndMakeVisible(kofiBtn);
        
        auto* beerBtn = new juce::TextButton("Bre-B");
        beerBtn->setBounds(rightBtnArea);
        beerBtn->onClick = [] { 
            auto img = juce::ImageFileFormat::loadFrom(BinaryData::qrdonaciones_png, BinaryData::qrdonaciones_pngSize);
            auto* qrWindow = new juce::AlertWindow("Donaciones - Bre-B", juce::String::fromUTF8("Escanea este código para apoyarme.\n¡Gracias por la cerveza!"), juce::AlertWindow::NoIcon);
            
            auto* imgComp = new juce::ImageComponent();
            imgComp->setImage(img, juce::RectanglePlacement::centred);
            // Limit max size to something reasonable but easy to scan
            int w = juce::jmin(img.getWidth(), 400);
            int h = juce::jmin(img.getHeight(), 400);
            imgComp->setSize(w, h);
            
            qrWindow->addCustomComponent(imgComp);
            qrWindow->addButton("Cerrar", 0, juce::KeyPress(juce::KeyPress::escapeKey));
            qrWindow->enterModalState(true, juce::ModalCallbackFunction::create([qrWindow](int) { delete qrWindow; }), true);
        };
        customComp->addAndMakeVisible(beerBtn);

        dw->addCustomComponent(customComp.release());
        dw->addButton("Cerrar", 0, juce::KeyPress(juce::KeyPress::escapeKey));
        
        dw->enterModalState(true, juce::ModalCallbackFunction::create([dw](int) { delete dw; }), true);
    }
    else if (button == &saveButton && hasResults) {
        double activeBpm = isBpmSwapped ? lastBpmResult.alternativeBpm : lastBpmResult.primaryBpm;
        juce::String k = isKeySwapped ? lastKeyResult.relativeKey : lastKeyResult.key;
        juce::String m = isKeySwapped ? lastKeyResult.relativeMode : lastKeyResult.mode;
        
        if (MetadataWriter::writeMetadata(currentFile, activeBpm, k, m))
            juce::AlertWindow::showMessageBoxAsync(juce::AlertWindow::InfoIcon, "Success", juce::String::fromUTF8("¡Metadatos guardados con éxito!"));
        else
            juce::AlertWindow::showMessageBoxAsync(juce::AlertWindow::WarningIcon, "Error", juce::String::fromUTF8("Error al guardar metadatos."));
    }
    else if (button == &keyPlayButton && hasResults)
    {
        if (scalePlayerSource.getIsPlaying()) {
            scalePlayerSource.stopScale();
        } else {
            juce::String k = isKeySwapped ? lastKeyResult.relativeKey : lastKeyResult.key;
            juce::String m = isKeySwapped ? lastKeyResult.relativeMode : lastKeyResult.mode;
            double activeBpm = isBpmSwapped ? lastBpmResult.alternativeBpm : lastBpmResult.primaryBpm;
            scalePlayerSource.playScale(k, m, activeBpm);
        }
    }
    else if (button == &pianoToggleButton)
    {
        pianoScaleView.setVisible(pianoToggleButton.getToggleState());
        resized();
    }
}

void MainComponent::mouseDown(const juce::MouseEvent& event)
{
    if ((event.eventComponent == &bpmValueLabel || event.eventComponent == &bpmSubLabel) && hasResults)
    {
        isBpmSwapped = !isBpmSwapped;
        updateUI();
    }
    else if ((event.eventComponent == &keyValueLabel || event.eventComponent == &keySubLabel) && hasResults)
    {
        isKeySwapped = !isKeySwapped;
        updateUI();
    }
}

void MainComponent::loadAudioFile(const juce::File& file)
{
    // Detach source before modifying buffers to avoid audio thread crashes
    transportSource.setSource(nullptr);
    memorySource.reset();
    
    currentFile = file;
    fileLabel.setText(file.getFileName(), juce::dontSendNotification);
    fileLabel.setColour(juce::Label::textColourId, HostilityLookAndFeel::getTextMain());
    analyzeButton.setEnabled(true);
    hasResults = false;
    isBpmSwapped = false;
    isKeySwapped = false;
    statusLabel.setText(juce::String::fromUTF8("Archivo cargado. Listo para analizar."), juce::dontSendNotification);
    
    loadedAudio = audioProvider.loadMono(file);
    if (!loadedAudio.first.empty()) 
        waveformView.setWaveformData(WaveformDataExtractor::extract(loadedAudio.first, loadedAudio.second));
    
    std::unique_ptr<juce::AudioFormatReader> reader(formatManager.createReaderFor(file));
    if (reader != nullptr) 
    {
        // Prevent overflow and massive allocations
        int numSamples = static_cast<int>(std::min<juce::int64>(reader->lengthInSamples, 1000000000)); // ~6 hours max
        
        playbackBuffer.setSize(static_cast<int>(reader->numChannels), numSamples);
        reader->read(&playbackBuffer, 0, numSamples, 0, true, true);
        
        memorySource = std::make_unique<juce::MemoryAudioSource>(playbackBuffer, false, false);
        transportSource.setSource(memorySource.get(), 0, nullptr, reader->sampleRate);
        waveformView.setDuration(static_cast<double>(numSamples) / reader->sampleRate);
        
        juce::String sr = juce::String(reader->sampleRate) + " Hz";
        juce::String bitd = juce::String(reader->bitsPerSample) + "-bit";
        juce::String ch = reader->numChannels == 2 ? "Stereo" : "Mono";
        formatLabel.setText("PCM - " + sr + " - " + bitd + " - " + ch, juce::dontSendNotification);
    }
    else
    {
        formatLabel.setText(juce::String::fromUTF8("Error: Formato no soportado"), juce::dontSendNotification);
        analyzeButton.setEnabled(false);
    }
    
    updateUI();
}

void MainComponent::analyzeFile()
{
    if (loadedAudio.first.empty() || !analyzeButton.isEnabled()) return;
    analyzeButton.setEnabled(false); saveButton.setEnabled(false);
    analysisProgress = 0.0; statusLabel.setText("Analizando audio...", juce::dontSendNotification);
    auto samples = loadedAudio.first; auto sampleRate = loadedAudio.second;
    juce::Thread::launch([this, samples, sampleRate]() {
        analysisProgress = 20.0; auto b = bpmDetector.detect(samples, sampleRate);
        analysisProgress = 50.0; auto k = keyDetector.detect(samples, sampleRate);
        analysisProgress = 70.0; auto l = loudnessAnalyzer.analyze(currentFile);
        analysisProgress = 100.0;
        juce::MessageManager::callAsync([this, b, k, l]() {
            lastBpmResult = b; lastKeyResult = k; lastLoudnessResult = l; hasResults = true;
            waveformView.setWaveformData(WaveformDataExtractor::extract(loadedAudio.first, loadedAudio.second));
            statusLabel.setText(juce::String::fromUTF8("¡Análisis completo!"), juce::dontSendNotification); updateUI();
        });
    });
}

void MainComponent::updateUI()
{
    if (hasResults) {
        double currentBpm = isBpmSwapped ? lastBpmResult.alternativeBpm : lastBpmResult.primaryBpm;
        double altBpm = isBpmSwapped ? lastBpmResult.primaryBpm : lastBpmResult.alternativeBpm;
        
        bpmValueLabel.setText(juce::String(currentBpm, 1), juce::dontSendNotification);
        bpmValueLabel.setColour(juce::Label::textColourId, isBpmSwapped ? HostilityLookAndFeel::getPlayhead() : HostilityLookAndFeel::getAccent());
        bpmSubLabel.setText("Alt: " + juce::String(altBpm, 1) + " BPM", juce::dontSendNotification);

        juce::String k = isKeySwapped ? lastKeyResult.relativeKey : lastKeyResult.key;
        juce::String m = isKeySwapped ? lastKeyResult.relativeMode : lastKeyResult.mode;
        juce::String relK = isKeySwapped ? lastKeyResult.key : lastKeyResult.relativeKey;
        juce::String relM = isKeySwapped ? lastKeyResult.mode : lastKeyResult.relativeMode;

        juce::String kt = k + " " + (m == "Major" ? "Maj" : "Min");
        juce::String cam = getCamelotKey(k, m);
        if (cam.isNotEmpty()) kt += " (" + cam + ")";
        
        keyValueLabel.setText(kt, juce::dontSendNotification);
        keyValueLabel.setColour(juce::Label::textColourId, isKeySwapped ? HostilityLookAndFeel::getPlayhead() : HostilityLookAndFeel::getPink());
        
        juce::String relText = relK + " " + (relM == "Major" ? "Maj" : "Min");
        keySubLabel.setText("Conf: " + juce::String(juce::roundToInt(lastKeyResult.confidence * 100)) + "% | Rel: " + relText, juce::dontSendNotification);
        lufsValueLabel.setText(juce::String(lastLoudnessResult.integratedLufs, 1), juce::dontSendNotification);
        lraValueLabel.setText(juce::String(lastLoudnessResult.loudnessRange, 1) + " LU", juce::dontSendNotification);
        tpValueLabel.setText(juce::String(lastLoudnessResult.truePeak, 1), juce::dontSendNotification);
        tpValueLabel.setColour(juce::Label::textColourId, lastLoudnessResult.truePeak >= 0.0 ? HostilityLookAndFeel::getRed() : HostilityLookAndFeel::getTextMain());
    }
    else {
        bpmValueLabel.setText("--", juce::dontSendNotification);
        bpmSubLabel.setText("", juce::dontSendNotification);
        keyValueLabel.setText("--", juce::dontSendNotification);
        keySubLabel.setText("", juce::dontSendNotification);
        lufsValueLabel.setText("--", juce::dontSendNotification);
        lraValueLabel.setText("--", juce::dontSendNotification);
        tpValueLabel.setText("--", juce::dontSendNotification);
        tpValueLabel.setColour(juce::Label::textColourId, HostilityLookAndFeel::getTextMain());
    }
    analyzeButton.setEnabled(true);
    saveButton.setEnabled(hasResults);

    if (hasResults)
    {
        juce::String k = isKeySwapped ? lastKeyResult.relativeKey : lastKeyResult.key;
        juce::String m = isKeySwapped ? lastKeyResult.relativeMode : lastKeyResult.mode;
        
        static const juce::StringArray noteNames = {"C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"};
        int rootIdx = noteNames.indexOf(k);
        pianoScaleView.setScale(rootIdx, m);
    }
    else
    {
        pianoScaleView.clearScale();
    }
}

void MainComponent::timerCallback()
{
    displayProgress = analysisProgress.load() / 100.0; // ProgressBar expects 0.0 to 1.0

    if (transportSource.getLengthInSeconds() > 0.0) {
        if (transportSource.isPlaying() && !waveformView.isUserDragging()) waveformView.setProgress(transportSource.getCurrentPosition() / transportSource.getLengthInSeconds());
        timeLabel.setText(formatTime(transportSource.getCurrentPosition()) + " / " + formatTime(transportSource.getLengthInSeconds()), juce::dontSendNotification);
    }

    // Update scale play button icon
    if (scalePlayerSource.getIsPlaying()) {
        keyPlayButton.setButtonText(juce::String::fromUTF8("\xe2\x8f\xb9")); // Stop icon
    } else {
        keyPlayButton.setButtonText(juce::String::fromUTF8("\xe2\x96\xb6")); // Play icon
    }
}

} // namespace ToneAndBeats
