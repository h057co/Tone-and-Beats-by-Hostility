/**
 * BPM Test Harness — Command-line tool to batch-test BPM detection accuracy.
 * 
 * Usage:  BpmTestHarness.exe <directory>
 * 
 * Expected filename convention:  "... bpm <number> ..."
 * Example:  "audio1 bpm 98.mp3"  →  expected BPM = 98
 *           "audio5 bpm 76,665.m4a"  →  expected BPM = 76.665
 */

#include <JuceHeader.h>
#include "Core/AudioDataProvider.h"
#include "Core/BpmDetector.h"
#include <iostream>
#include <iomanip>
#include <regex>

struct TestResult
{
    juce::String filename;
    double expectedBpm;
    double detectedBpm;
    double alternativeBpm;
    double errorBpm;
    double errorPercent;
    bool pass;       // within ±2 BPM
    bool closePass;  // within ±5 BPM
    juce::String notes;
};

static double parseExpectedBpm(const juce::String& filename)
{
    // Match "bpm <number>" where number can use comma as decimal separator
    // Examples: "bpm 98", "bpm 76,665", "bpm 98,256"
    auto lower = filename.toLowerCase();
    int bpmIdx = lower.indexOf("bpm ");
    if (bpmIdx < 0) return -1;

    auto afterBpm = filename.substring(bpmIdx + 4).trim();

    // Extract numeric portion (digits, comma, dot)
    juce::String numStr;
    for (int i = 0; i < afterBpm.length(); ++i)
    {
        auto ch = afterBpm[i];
        if (juce::CharacterFunctions::isDigit(ch) || ch == '.' || ch == ',')
            numStr += ch;
        else if (numStr.isNotEmpty())
            break;
    }

    // Replace comma with dot for parsing
    numStr = numStr.replace(",", ".");
    return numStr.getDoubleValue();
}

int main(int argc, char* argv[])
{
    // Initialize JUCE
    juce::ScopedJuceInitialiser_GUI juceInit;

    juce::String testDir = "O:\\Desarrollos\\Tone and Beats\\audiotest";
    if (argc > 1)
        testDir = argv[1];

    juce::File dir(testDir);
    if (!dir.isDirectory())
    {
        std::cerr << "ERROR: Directory not found: " << testDir.toStdString() << std::endl;
        return 1;
    }

    // Collect test files
    auto files = dir.findChildFiles(juce::File::findFiles, false);
    files.sort();

    std::vector<TestResult> results;
    int totalFiles = 0;
    int passCount = 0;
    int closePassCount = 0;
    int failCount = 0;
    int skippedCount = 0;

    auto supportedExts = AudioDataProvider::getSupportedExtensions();

    std::cout << "================================================================" << std::endl;
    std::cout << "  TONE & BEATS — BPM Detection Test Harness" << std::endl;
    std::cout << "  Directory: " << testDir.toStdString() << std::endl;
    std::cout << "  Files found: " << files.size() << std::endl;
    std::cout << "================================================================" << std::endl;
    std::cout << std::endl;

    ToneAndBeats::BpmDetector detector;

    for (auto& file : files)
    {
        auto ext = file.getFileExtension().toLowerCase();
        
        // Skip unsupported / backup files
        if (ext == ".bk" || ext == ",bak" || ext == ".bak")
        {
            skippedCount++;
            continue;
        }

        double expectedBpm = parseExpectedBpm(file.getFileNameWithoutExtension());
        if (expectedBpm <= 0)
        {
            std::cout << "  [SKIP] " << file.getFileName().toStdString()
                      << " — No BPM in filename" << std::endl;
            skippedCount++;
            continue;
        }

        // Check if format is supported
        bool supported = false;
        for (auto& s : supportedExts)
            if (ext.equalsIgnoreCase(s) || ext.equalsIgnoreCase("." + s))
                supported = true;
        
        if (!supported)
        {
            std::cout << "  [SKIP] " << file.getFileName().toStdString()
                      << " — Unsupported format (" << ext.toStdString() << ")" << std::endl;
            skippedCount++;
            continue;
        }

        totalFiles++;
        std::cout << "  [" << totalFiles << "] Analyzing: " << file.getFileName().toStdString()
                  << "  (expected: " << expectedBpm << " BPM) ..." << std::flush;

        auto result = detector.detect(file);

        TestResult tr;
        tr.filename = file.getFileName();
        tr.expectedBpm = expectedBpm;
        tr.detectedBpm = result.primaryBpm;
        tr.alternativeBpm = result.alternativeBpm;
        tr.errorBpm = std::abs(result.primaryBpm - expectedBpm);
        tr.errorPercent = expectedBpm > 0 ? (tr.errorBpm / expectedBpm) * 100.0 : 0;
        tr.pass = tr.errorBpm <= 2.0;
        tr.closePass = tr.errorBpm <= 5.0;

        // Check if alternative BPM matches
        double altError = std::abs(result.alternativeBpm - expectedBpm);
        if (!tr.pass && altError <= 2.0)
            tr.notes = "ALT_MATCH (alt=" + juce::String(result.alternativeBpm, 1) + ")";
        
        // Check for harmonic relationship
        if (!tr.pass)
        {
            double ratio = result.primaryBpm / expectedBpm;
            if (std::abs(ratio - 2.0) < 0.05) tr.notes = "DOUBLE_TIME";
            else if (std::abs(ratio - 0.5) < 0.05) tr.notes = "HALF_TIME";
            else if (std::abs(ratio - 1.5) < 0.05) tr.notes = "TRESILLO_UP";
            else if (std::abs(ratio - 0.667) < 0.05) tr.notes = "TRESILLO_DOWN";
        }

        results.push_back(tr);

        if (tr.pass)
        {
            passCount++;
            std::cout << "  PASS (" << result.primaryBpm << " BPM, err=" << std::fixed << std::setprecision(1) << tr.errorBpm << ")" << std::endl;
        }
        else if (tr.closePass)
        {
            closePassCount++;
            std::cout << "  CLOSE (" << result.primaryBpm << " BPM, err=" << std::fixed << std::setprecision(1) << tr.errorBpm << ")" << std::endl;
        }
        else
        {
            failCount++;
            std::cout << "  FAIL (" << result.primaryBpm << " BPM, err=" << std::fixed << std::setprecision(1) << tr.errorBpm;
            if (tr.notes.isNotEmpty())
                std::cout << ", " << tr.notes.toStdString();
            std::cout << ")" << std::endl;
        }
    }

    // === Summary Report ===
    std::cout << std::endl;
    std::cout << "================================================================" << std::endl;
    std::cout << "  RESULTS SUMMARY" << std::endl;
    std::cout << "================================================================" << std::endl;
    std::cout << std::endl;

    // Detailed table
    std::cout << std::left << std::setw(40) << "FILE"
              << std::right << std::setw(8) << "EXPECT"
              << std::setw(8) << "DETECT"
              << std::setw(8) << "ALT"
              << std::setw(8) << "ERR"
              << std::setw(8) << "ERR%"
              << std::setw(10) << "STATUS"
              << "  NOTES" << std::endl;
    std::cout << std::string(100, '-') << std::endl;

    for (auto& r : results)
    {
        std::string status = r.pass ? "PASS" : (r.closePass ? "CLOSE" : "FAIL");
        
        std::cout << std::left << std::setw(40) << r.filename.toStdString().substr(0, 39)
                  << std::right << std::fixed << std::setprecision(1)
                  << std::setw(8) << r.expectedBpm
                  << std::setw(8) << r.detectedBpm
                  << std::setw(8) << r.alternativeBpm
                  << std::setw(8) << r.errorBpm
                  << std::setw(7) << r.errorPercent << "%"
                  << std::setw(10) << status
                  << "  " << r.notes.toStdString() << std::endl;
    }

    std::cout << std::string(100, '-') << std::endl;
    std::cout << std::endl;

    std::cout << "  Total analyzed:   " << totalFiles << std::endl;
    std::cout << "  Skipped:          " << skippedCount << std::endl;
    std::cout << "  PASS (±2 BPM):    " << passCount << " / " << totalFiles
              << " (" << (totalFiles > 0 ? (passCount * 100 / totalFiles) : 0) << "%)" << std::endl;
    std::cout << "  CLOSE (±5 BPM):   " << (passCount + closePassCount) << " / " << totalFiles
              << " (" << (totalFiles > 0 ? ((passCount + closePassCount) * 100 / totalFiles) : 0) << "%)" << std::endl;
    std::cout << "  FAIL (>5 BPM):    " << failCount << " / " << totalFiles << std::endl;

    // Average error
    double totalError = 0;
    for (auto& r : results) totalError += r.errorBpm;
    double avgError = totalFiles > 0 ? totalError / totalFiles : 0;
    std::cout << "  Average error:    " << std::fixed << std::setprecision(2) << avgError << " BPM" << std::endl;
    std::cout << std::endl;

    // List failures with diagnostic hints
    if (failCount > 0)
    {
        std::cout << "================================================================" << std::endl;
        std::cout << "  FAILURE ANALYSIS" << std::endl;
        std::cout << "================================================================" << std::endl;
        for (auto& r : results)
        {
            if (!r.closePass)
            {
                std::cout << std::endl;
                std::cout << "  FILE:     " << r.filename.toStdString() << std::endl;
                std::cout << "  Expected: " << r.expectedBpm << " BPM" << std::endl;
                std::cout << "  Detected: " << r.detectedBpm << " BPM (alt: " << r.alternativeBpm << ")" << std::endl;
                std::cout << "  Error:    " << std::fixed << std::setprecision(1) << r.errorBpm << " BPM (" << r.errorPercent << "%)" << std::endl;
                if (r.notes.isNotEmpty())
                    std::cout << "  Pattern:  " << r.notes.toStdString() << std::endl;
                
                // Diagnostic hints
                double ratio = r.detectedBpm / r.expectedBpm;
                std::cout << "  Ratio:    " << std::fixed << std::setprecision(3) << ratio << std::endl;
                
                if (std::abs(ratio - 2.0) < 0.1)
                    std::cout << "  HINT:     Engine detected DOUBLE-TIME. Need stricter resolveDoubleTimeAmbiguity()." << std::endl;
                else if (std::abs(ratio - 0.5) < 0.1)
                    std::cout << "  HINT:     Engine detected HALF-TIME. Need normalizeTempoRangeAuto() adjustment." << std::endl;
                else if (std::abs(ratio - 1.5) < 0.1)
                    std::cout << "  HINT:     Engine detected TRESILLO (x1.5). Need Trap heuristic guard." << std::endl;
                else if (std::abs(ratio - 0.667) < 0.1)
                    std::cout << "  HINT:     Engine detected TRESILLO (/1.5). Need Trap heuristic guard." << std::endl;
                else
                    std::cout << "  HINT:     Non-harmonic error. May require tuning onset detection or voting weights." << std::endl;
            }
        }
        std::cout << std::endl;
    }

    return 0;
}
