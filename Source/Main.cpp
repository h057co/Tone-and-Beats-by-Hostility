#include <JuceHeader.h>
#include "MainComponent.h"
#include "UI/LookAndFeel.h"

namespace ToneAndBeats
{

class ToneAndBeatsApplication : public juce::JUCEApplication
{
public:
    ToneAndBeatsApplication() {}

    const juce::String getApplicationName() override    { return ProjectInfo::projectName; }
    const juce::String getApplicationVersion() override { return ProjectInfo::versionString; }
    bool moreThanOneInstanceAllowed() override    { return false; }

    void initialise (const juce::String& /*commandLine*/) override
    {
        juce::LookAndFeel::setDefaultLookAndFeel(&hostilityLookAndFeel);
        mainWindow.reset (new MainWindow (getApplicationName()));
    }

    void shutdown() override
    {
        mainWindow = nullptr;
    }

    void systemRequestedQuit() override
    {
        quit();
    }

    void anotherInstanceStarted (const juce::String& /*commandLine*/) override
    {
    }

    class MainWindow : public juce::DocumentWindow
    {
    public:
        MainWindow(juce::String /*name*/)
            : DocumentWindow("Tone & Beats By Hostility",
                             HostilityLookAndFeel::getBackground(),
                             DocumentWindow::allButtons)
        {
            setUsingNativeTitleBar(true);
            setContentOwned(new MainComponent(), true);

           #if JUCE_IOS || JUCE_ANDROID
            setFullScreen(true);
           #else
            // Cargar icono desde BinaryData
            juce::Image iconImage = juce::ImageFileFormat::loadFrom(BinaryData::Icono_jpg, BinaryData::Icono_jpgSize);
            
            if (iconImage.isValid()) {
                setIcon(iconImage);
            } else {
                setName("Tone & Beats By Hostility (Icon Load Failed)");
            }

            setResizable(true, true);
            setResizeLimits(400, 800, 10000, 10000);
            centreWithSize(400, 850);
           #endif

            setVisible(true);
        }

        void closeButtonPressed() override
        {
            JUCEApplication::getInstance()->systemRequestedQuit();
        }

    private:
        JUCE_DECLARE_NON_COPYABLE_WITH_LEAK_DETECTOR(MainWindow)
    };

private:
    std::unique_ptr<MainWindow> mainWindow;
    HostilityLookAndFeel hostilityLookAndFeel;
};

} // namespace ToneAndBeats

START_JUCE_APPLICATION(ToneAndBeats::ToneAndBeatsApplication)