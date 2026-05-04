/*
  ==============================================================================

    UpdateManager.cpp
    Created: 3 May 2026
    Author:  Hostility

  ==============================================================================
*/

#include "UpdateManager.h"

namespace ToneAndBeats
{

UpdateManager::UpdateManager()
    : Thread("UpdateManagerThread")
{
}

UpdateManager::~UpdateManager()
{
    stopThread(4000);
}

void UpdateManager::checkForUpdates(bool silent)
{
    silentCheck = silent;
    updateAvailable = false;
    shouldDownload = false;
    startThread();
}

void UpdateManager::startUpdateDownload()
{
    if (updateAvailable && !downloading)
    {
        shouldDownload = true;
        downloading = true;
        downloadProgress = 0.0f;
        // The thread is likely already finished checking, so we restart it
        if (!isThreadRunning())
            startThread();
    }
}

void UpdateManager::run()
{
    if (!shouldDownload)
    {
        if (checkGitHubReleases())
        {
            updateAvailable = true;
            if (onUpdateAvailable)
            {
                juce::MessageManager::callAsync([this]() {
                    onUpdateAvailable(latestVersion, releaseNotes);
                });
            }
        }
    }
    else
    {
        if (downloadInstaller())
        {
            downloading = false;
            if (onDownloadFinished)
            {
                juce::MessageManager::callAsync([this]() {
                    onDownloadFinished(true, "Download complete. Starting installer...");
                });
            }
        }
        else
        {
            downloading = false;
            if (onDownloadFinished)
            {
                juce::MessageManager::callAsync([this]() {
                    onDownloadFinished(false, "Download failed.");
                });
            }
        }
    }
}

void UpdateManager::timerCallback()
{
    if (downloading && onDownloadProgress)
        onDownloadProgress(downloadProgress.load());
}

bool UpdateManager::checkGitHubReleases()
{
    juce::URL url("https://api.github.com/repos/h057co/Tone-and-Beats-by-Hostility/releases/latest");
    
    // GitHub API requires a User-Agent and recommends the specific Accept header
    auto options = juce::URL::InputStreamOptions(juce::URL::ParameterHandling::inAddress)
                   .withExtraHeaders("User-Agent: ToneAndBeats-Updater\nAccept: application/vnd.github.v3+json")
                   .withConnectionTimeoutMs(10000);

    std::unique_ptr<juce::InputStream> stream(url.createInputStream(options));
    
    if (stream != nullptr)
    {
        auto response = stream->readEntireStreamAsString();
        auto json = juce::JSON::parse(response);

        if (json.isObject())
        {
            auto tagName = json.getDynamicObject()->getProperty("tag_name").toString();
            latestVersion = tagName.startsWithIgnoreCase("v") ? tagName.substring(1) : tagName;
            releaseNotes = json.getDynamicObject()->getProperty("body").toString();

            // Robust version comparison
            juce::String currentVersion = ProjectInfo::versionString;
            
            auto isVersionNewer = [](const juce::String& latest, const juce::String& current)
            {
                juce::StringArray latestParts;
                latestParts.addTokens (latest, ".", "");
                juce::StringArray currentParts;
                currentParts.addTokens (current, ".", "");
                
                for (int i = 0; i < juce::jmax (latestParts.size(), currentParts.size()); ++i)
                {
                    int l = (i < latestParts.size()) ? latestParts[i].getIntValue() : 0;
                    int c = (i < currentParts.size()) ? currentParts[i].getIntValue() : 0;
                    
                    if (l > c) return true;
                    if (l < c) return false;
                }
                return false;
            };

            if (isVersionNewer (latestVersion, currentVersion))
            {
                // Find the Windows installer asset (.exe)
                auto assets = json.getDynamicObject()->getProperty("assets");
                if (assets.isArray())
                {
                    for (int i = 0; i < (int)assets.size(); ++i)
                    {
                        auto asset = assets[i];
                        juce::String assetName = asset.getDynamicObject()->getProperty("name").toString();
                        if (assetName.endsWithIgnoreCase(".exe"))
                        {
                            downloadUrl = asset.getDynamicObject()->getProperty("browser_download_url").toString();
                            return true;
                        }
                    }
                }
            }
        }
    }

    return false;
}

bool UpdateManager::downloadInstaller()
{
    if (downloadUrl.isEmpty())
        return false;

    juce::URL url(downloadUrl);
    auto options = juce::URL::InputStreamOptions(juce::URL::ParameterHandling::inAddress)
                   .withConnectionTimeoutMs(30000);

    std::unique_ptr<juce::InputStream> stream(url.createInputStream(options));

    if (stream != nullptr)
    {
        auto tempFile = juce::File::getSpecialLocation(juce::File::tempDirectory)
                        .getChildFile("ToneAndBeats_Update.exe");

        if (tempFile.existsAsFile())
            tempFile.deleteFile();

        std::unique_ptr<juce::FileOutputStream> outputStream(tempFile.createOutputStream());

        if (outputStream != nullptr)
        {
            auto totalLength = stream->getTotalLength();
            const int bufferSize = 65536;
            juce::HeapBlock<char> buffer(bufferSize);

            juce::int64 downloaded = 0;
            while (!stream->isExhausted() && !threadShouldExit())
            {
                auto read = stream->read(buffer, bufferSize);
                if (read > 0)
                {
                    outputStream->write(buffer, read);
                    downloaded += read;
                    
                    if (totalLength > 0)
                        downloadProgress = static_cast<float>(downloaded) / static_cast<float>(totalLength);
                }
                else if (read < 0)
                {
                    return false; // Error
                }
            }

            outputStream.reset(); // Close file before starting process

            if (!threadShouldExit())
            {
                // Launch installer and exit app
                if (tempFile.startAsProcess())
                {
                    juce::MessageManager::callAsync([]() {
                        juce::JUCEApplication::getInstance()->systemRequestedQuit();
                    });
                    return true;
                }
            }
        }
    }

    return false;
}

} // namespace ToneAndBeats
