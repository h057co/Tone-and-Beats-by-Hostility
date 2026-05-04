/*
  ==============================================================================

    UpdateManager.h
    Created: 3 May 2026
    Author:  Hostility

  ==============================================================================
*/

#pragma once

#include <JuceHeader.h>

namespace ToneAndBeats
{

class UpdateManager : public juce::Thread,
                      private juce::Timer
{
public:
    UpdateManager();
    ~UpdateManager() override;

    /** Starts a check for updates.
        @param silent If true, no messages will be shown if no update is found.
    */
    void checkForUpdates(bool silent);

    /** Triggers the download and installation of the update. */
    void startUpdateDownload();

    /** Returns true if an update has been found but not yet downloaded. */
    bool isUpdateAvailable() const { return updateAvailable; }

    /** Returns the version string of the available update. */
    juce::String getLatestVersion() const { return latestVersion; }

    /** Returns the release notes for the new version. */
    juce::String getReleaseNotes() const { return releaseNotes; }

    /** Returns the current download progress (0.0 to 1.0). */
    float getDownloadProgress() const { return downloadProgress.load(); }

    /** Returns true if currently downloading. */
    bool isDownloading() const { return downloading.load(); }

    // Callbacks for UI integration
    std::function<void(juce::String, juce::String)> onUpdateAvailable;
    std::function<void(float)> onDownloadProgress;
    std::function<void(bool, juce::String)> onDownloadFinished;

private:
    void run() override;
    void timerCallback() override;

    bool checkGitHubReleases();
    bool downloadInstaller();

    bool silentCheck = true;
    bool updateAvailable = false;
    juce::String latestVersion;
    juce::String downloadUrl;
    juce::String releaseNotes;

    std::atomic<bool> downloading{ false };
    std::atomic<float> downloadProgress{ 0.0f };
    std::atomic<bool> shouldDownload{ false };

    JUCE_DECLARE_NON_COPYABLE_WITH_LEAK_DETECTOR(UpdateManager)
};

} // namespace ToneAndBeats
