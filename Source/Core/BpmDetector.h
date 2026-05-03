#pragma once

#include <JuceHeader.h>
#include "Constants.h"
#include "WaveformData.h"
#include "AudioDataProvider.h"
#include <vector>
#include <functional>
#include <utility>

namespace ToneAndBeats
{

struct BpmCandidate
{
    double bpm;
    double score;
    juce::String source;
};

struct BeatAnalysisResult {
    double primaryBpm;
    double confidence;
    std::vector<BpmCandidate> alternateBpms;
    std::vector<double> beatTimes;
    bool isHalfTime = false;
    bool isDoubleTime = false;
};

struct Transient
{
    double position; // seconds
    float amplitude;
};

struct BeatGridScore
{
    double hitRate;
    double stdDev;
    double phase;    // seconds
    double composite;
};

class BpmDetector
{
public:
    BpmDetector();
    ~BpmDetector();

    /**
     * Main entry point for the professional analysis pipeline.
     */
    double detectBpm(juce::AudioBuffer<float>& buffer, int sampleRate, 
                    std::function<void(int)> callback = nullptr);

    /**
     * Detailed analysis returning rich metadata.
     */
    BeatAnalysisResult analyzeFullTrack(juce::AudioBuffer<float>& buffer, int sampleRate);

    // Legacy public interface for compatibility
    BpmResult detect(const std::vector<float>& monoSamples, int sampleRate, 
                   BpmRangeProfile profile = BpmRangeProfile::Auto);
    BpmResult detect(const juce::File& file, BpmRangeProfile profile = BpmRangeProfile::Auto);
    BpmResult detect(const juce::String& filePath, BpmRangeProfile profile = BpmRangeProfile::Auto);

    void setProgressCallback(std::function<void(int)> callback);

private:
    std::function<void(int)> progressCallback;

    // --- Phase 1: Preprocessing ---
    std::vector<float> prepareMonoSignal(const juce::AudioBuffer<float>& buffer);
    std::vector<float> extractAnalysisSegment(const std::vector<float>& fullSignal, int sampleRate, double startTimeSec, double durationSec);
    double detectWithSoundTouch(const std::vector<float>& monoSamples, int sampleRate);
    std::vector<float> selectAnalysisSegment(const std::vector<float>& monoSamples, int sampleRate, double initialBpm);

    // --- Phase 2: Tempo Induction (Estimation) ---
    void estimateSegmentTempo(const std::vector<float>& segment, int sampleRate, 
                             double& outGridBpm, double& outGridConf, std::vector<BpmCandidate>& gridCandidates,
                             double& outSfBpm, double& outSfConf, std::vector<BpmCandidate>& sfCandidates);

    // --- Phase 3: Global Consensus & Refinement ---
    double refineBpmWithGrid(double candidateBpm, const std::vector<float>& segment, int sampleRate);

    // --- Internal Helpers (Legacy/Core) ---
    BpmResult detectBpmFromSamples(const std::vector<float> &monoSamples, int sampleRate, BpmRangeProfile profile);

    void detectBpmByTransientGrid(const std::vector<float> &samples, int sampleRate, double &outBpm,
                                  double &outConfidence, std::vector<BpmCandidate> &outCandidates);
    void detectBpmBySpectralFlux(const std::vector<float> &samples, int sampleRate, double &outBpm,
                                 double &outConfidence, std::vector<BpmCandidate> &outCandidates);
    
    double voteThreeSources(double stBpm, double gridBpm, double gridConf, double sfBpm, double sfConf, 
                          const std::vector<BpmCandidate>& gridCandidates);

    // --- Heuristics & Post-processing ---
    double resolveDoubleTimeAmbiguity(double detectedBpm, double stBpm, double gridBpm);
    double selectBestCandidateForProfile(double currentBpm, double stBpm, 
                                       const std::vector<BpmCandidate>& gridCandidates,
                                       const std::vector<BpmCandidate>& sfCandidates,
                                       BpmRangeProfile profile);
    bool shouldApplyTrapHeuristic(double stBpm, const std::vector<BpmCandidate>& candidates);
    double snapToInteger(double bpm);
    double calculateAlternativeBpm(double primaryBpm);
    std::pair<double, double> getProfileRange(BpmRangeProfile profile);
    double normalizeTempoRangeAuto(double bpm);

    // --- Signal Processing Helpers ---
    std::vector<float> computeSpectralFlux(const float* samples, int numSamples, int sampleRate);
    std::vector<BpmCandidate> estimateBpmFromOnsetStrength(const std::vector<float>& onsetStrength, double sampleRate, double minBpm, double maxBpm);
    std::vector<Transient> detectTransients(const std::vector<float>& envelope, double sampleRateEnvelope);
    BeatGridScore scoreBeatGrid(const std::vector<Transient>& transients, double bpm, double segmentDuration);
    double checkBeatPeriodConsistency(const std::vector<float>& onsetStrength, double bpm, int sampleRate);

    juce::String lastErrorMessage;
    
    // Constants from C# reference
    static constexpr int FFT_SIZE = 2048;
    static constexpr int HOP_SIZE = 512;
    static constexpr double TRESILLO_RATIO = 1.5;
    static constexpr double MIN_CONFIDENCE_THRESHOLD = 0.2;
};

} // namespace ToneAndBeats