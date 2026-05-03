#include "BpmDetector.h"
#include "DspUtils.h"
#include <BPMDetect.h>
#include <SoundTouch.h>
#include <algorithm>
#include <cmath>
#include <functional>
#include <numeric>

namespace ToneAndBeats {

BpmDetector::BpmDetector() {}
BpmDetector::~BpmDetector() {}

void BpmDetector::setProgressCallback(std::function<void(int)> callback) {
  progressCallback = callback;
}

BpmResult BpmDetector::detect(const std::vector<float> &monoSamples,
                              int sampleRate, BpmRangeProfile profile) {
  if (progressCallback)
    progressCallback(5);
  return detectBpmFromSamples(monoSamples, sampleRate, profile);
}

BpmResult BpmDetector::detect(const juce::File &file, BpmRangeProfile profile) {
  AudioDataProvider provider;
  auto [samples, rate] = provider.loadMono(file);
  return detect(samples, rate, profile);
}

BpmResult BpmDetector::detect(const juce::String &filePath,
                              BpmRangeProfile profile) {
  return detect(juce::File(filePath), profile);
}

double BpmDetector::detectBpm(juce::AudioBuffer<float>& buffer, int sampleRate, 
                             std::function<void(int)> cb) {
    if (cb) progressCallback = cb; // Store it if provided
    auto monoSamples = prepareMonoSignal(buffer);
    if (monoSamples.empty()) return 0.0;

    // --- Phase 1: SoundTouch Global Induction ---
    double stBpm = 0; // soundTouchBpm (placeholder for future direct integration)
    // For now, we still use the SoundTouch estimate as a seed
    try {
        stBpm = detectWithSoundTouch(monoSamples, sampleRate);
    } catch (...) {}

    if (progressCallback) progressCallback(20);

    // --- Phase 2: Multi-Segment Analysis (Beginning, Middle, End) ---
    const int numSegments = 3;
    const double segmentDuration = 30.0; // 30s segments
    double songDuration = (double)monoSamples.size() / sampleRate;
    
    std::vector<double> candidates;
    
    for (int i = 0; i < numSegments; ++i) {
        double startSec = (songDuration * (i + 1)) / (numSegments + 1) - (segmentDuration / 2.0);
        if (startSec < 0) startSec = 0;
        
        auto segment = extractAnalysisSegment(monoSamples, sampleRate, startSec, segmentDuration);
        if (segment.empty()) continue;

        double gridBpm = 0, gridConf = 0;
        std::vector<BpmCandidate> gridCandidates;
        double sfBpm = 0, sfConf = 0;
        std::vector<BpmCandidate> sfCandidates;

        estimateSegmentTempo(segment, sampleRate, gridBpm, gridConf, gridCandidates, sfBpm, sfConf, sfCandidates);
        
        double segmentWinner = voteThreeSources(stBpm, gridBpm, gridConf, sfBpm, sfConf, gridCandidates);
        if (segmentWinner > 0) candidates.push_back(segmentWinner);

        if (progressCallback) progressCallback(20 + (i + 1) * 20);
    }

    // === PROFESSIONAL CONSENSUS & MUSICAL PREFERENCE ===
    std::sort(candidates.begin(), candidates.end());
    double finalBpm = candidates[candidates.size() / 2];

    // Final Octave/Tresillo resolve before refinement
    // If we have a choice between 76/101 and 152, in Urban music we ALWAYS prefer 152.
    if (finalBpm < 110.0) {
        if (stBpm > 140.0 && stBpm < 165.0) finalBpm = stBpm;
        else if (std::abs(finalBpm * 1.5 - 152.0) < 5.0) finalBpm *= 1.5; // Tresillo -> Trap
        else if (finalBpm < 85.0 && finalBpm > 70.0) finalBpm *= 2.0;   // Half-time -> Trap
    }

    // Refine phase/decimals
    auto midSegment = extractAnalysisSegment(monoSamples, sampleRate, songDuration * 0.5 - 15.0, 30.0);
    finalBpm = refineBpmWithGrid(finalBpm, midSegment, sampleRate);

    // Final Safety Normalization
    finalBpm = normalizeTempoRangeAuto(finalBpm);

    if (progressCallback) progressCallback(100);
    return finalBpm;
}

std::vector<float> BpmDetector::prepareMonoSignal(const juce::AudioBuffer<float>& buffer) {
    std::vector<float> mono(buffer.getNumSamples());
    auto numChannels = buffer.getNumChannels();
    for (int i = 0; i < buffer.getNumSamples(); ++i) {
        float sum = 0;
        for (int c = 0; c < numChannels; ++c) sum += buffer.getSample(c, i);
        mono[i] = sum / numChannels;
    }
    return mono;
}

std::vector<float> BpmDetector::extractAnalysisSegment(const std::vector<float>& fullSignal, int sampleRate, double startTimeSec, double durationSec) {
    int startSample = (int)(startTimeSec * sampleRate);
    int numSamples = (int)(durationSec * sampleRate);
    
    if (startSample >= (int)fullSignal.size()) return {};
    if (startSample + numSamples > (int)fullSignal.size()) numSamples = (int)fullSignal.size() - startSample;
    if (numSamples <= 0) return {};

    return std::vector<float>(fullSignal.begin() + startSample, fullSignal.begin() + startSample + numSamples);
}

void BpmDetector::estimateSegmentTempo(const std::vector<float>& segment, int sampleRate, 
                                     double& outGridBpm, double& outGridConf, std::vector<BpmCandidate>& gridCandidates,
                                     double& outSfBpm, double& outSfConf, std::vector<BpmCandidate>& sfCandidates) {
    // Low-band for grid
    auto lowBand = DspUtils::applyLowPassFilter(segment, sampleRate, 250.0f);
    auto lowBandTransient = DspUtils::applyTransientEnhancementFilter(lowBand);
    detectBpmByTransientGrid(lowBandTransient, sampleRate, outGridBpm, outGridConf, gridCandidates);

    // High-band for flux
    auto highBand = DspUtils::applyBandPassFilter(segment, sampleRate, 5000.0f, 6000.0f);
    detectBpmBySpectralFlux(highBand, sampleRate, outSfBpm, outSfConf, sfCandidates);
}

double BpmDetector::refineBpmWithGrid(double candidateBpm, const std::vector<float>& segment, int sampleRate) {
    if (candidateBpm <= 0) return candidateBpm;

    auto allTransients = detectTransients(segment, sampleRate);
    double segmentDur = (double)segment.size() / sampleRate;

    if (allTransients.empty()) return candidateBpm;

    double bestBpm = candidateBpm;
    auto baseGrid = scoreBeatGrid(allTransients, candidateBpm, segmentDur);
    double bestComp = baseGrid.composite;
    
    const double harmonicRatios[] = { 0.5, 2.0, 0.667, 1.5, 0.75, 1.333, 0.8, 1.25 };
    for (double r : harmonicRatios) {
        double testBpm = candidateBpm * r;
        if (testBpm < 50.0 || testBpm > 210.0) continue;
        
        auto testGrid = scoreBeatGrid(allTransients, testBpm, segmentDur);
        // We require a significant improvement (+0.3) to justify a jump
        if (testGrid.composite > bestComp + 0.3) {
            bestBpm = testBpm;
            bestComp = testGrid.composite;
        }
    }

    return normalizeTempoRangeAuto(bestBpm);
}

BpmResult
BpmDetector::detectBpmFromSamples(const std::vector<float> &monoSamples,
                                  int sampleRate, BpmRangeProfile profile) {
  BpmResult result = {0, 0};

  try {
    if (monoSamples.size() < (size_t)(sampleRate * 5))
      return result;

    if (progressCallback)
      progressCallback(10);

    // --- Step 1: SoundTouch quick estimate ---
    double soundTouchBpm = detectWithSoundTouch(monoSamples, sampleRate);
    juce::Logger::writeToLog("BpmDetector: SoundTouch estimate: " +
                             juce::String(soundTouchBpm));

    if (progressCallback)
      progressCallback(25);

    // --- Step 2: Select analysis segment (Intro skip, 60s max) ---
    double initialBpm = soundTouchBpm > 0 ? soundTouchBpm : 120.0;
    auto segment = selectAnalysisSegment(monoSamples, sampleRate, initialBpm);

    if (progressCallback)
      progressCallback(35);

    // --- Step 3: Core Analysis Pipeline (Dual-Band) ---
    double gridBpm = 0, gridConf = 0;
    std::vector<BpmCandidate> gridCandidates;

    double sfBpm = 0, sfConf = 0;
    std::vector<BpmCandidate> sfCandidates;

    // A. Low-Band Analysis (0 - 250 Hz) for Kicks/Bass
    auto lowBand = DspUtils::applyLowPassFilter(segment, sampleRate, 250.0f);
    auto lowBandTransient = DspUtils::applyTransientEnhancementFilter(lowBand);
    detectBpmByTransientGrid(lowBandTransient, sampleRate, gridBpm, gridConf,
                             gridCandidates);

    if (progressCallback)
      progressCallback(60);

    // B. High-Band Analysis (2000 - 8000 Hz) for Snares/Claps
    auto highBand = DspUtils::applyBandPassFilter(
        segment, sampleRate, 5000.0f, 6000.0f); // Center 5k, BW 6k (2k-8k)
    detectBpmBySpectralFlux(highBand, sampleRate, sfBpm, sfConf, sfCandidates);

    if (progressCallback)
      progressCallback(85);

    // === ST HALF-TIME HYPOTHESIS (Fixes master 152 tracks) ===
    // If SoundTouch falls into the 85-115 BPM zone, evaluate if ST * 1.5 is the real tempo
    if (soundTouchBpm >= 85.0 && soundTouchBpm <= 115.0) {
      double tresilloHypothesis = soundTouchBpm * TRESILLO_RATIO; // 1.5
      bool found = false;
      for (const auto &c : gridCandidates) {
        if (std::abs(c.bpm - tresilloHypothesis) < 4.0 && c.score > -0.1) {
          found = true;
          break;
        }
      }
      if (!found) {
        for (const auto &c : sfCandidates) {
          if (std::abs(c.bpm - tresilloHypothesis) < 4.0 && c.score > -0.1) {
            found = true;
            break;
          }
        }
      }
      if (found) {
        juce::Logger::writeToLog("[ST HalfTime Hyp] " + juce::String(soundTouchBpm, 1) + " -> " + juce::String(tresilloHypothesis, 1) + " confirmed by candidates.");
        soundTouchBpm = tresilloHypothesis;
      }
    }

    // --- Step 4: Voting & Consensus ---
    double finalBpm = voteThreeSources(soundTouchBpm, gridBpm, gridConf, sfBpm,
                                       sfConf, gridCandidates);
    bool heuristicApplied = false;

    // --- NEW: Professional Polyrhythmic Refinement ---
    // This allows snapping from a detected 'ghost' pulse (e.g. 104) to the true pulse (130)
    // by checking if harmonic ratios yield a significantly better grid alignment.
    auto allTransients = detectTransients(segment, sampleRate);
    double segmentDur = (double)segment.size() / sampleRate;

    if (finalBpm > 0 && !allTransients.empty()) {
      double bestBpm = finalBpm;
      auto baseGrid = scoreBeatGrid(allTransients, finalBpm, segmentDur);
      double bestComp = baseGrid.composite;
      
      const double harmonicRatios[] = { 0.5, 2.0, 0.667, 1.5, 0.75, 1.333, 0.8, 1.25 };
      for (double r : harmonicRatios) {
        double testBpm = finalBpm * r;
        if (testBpm < 50.0 || testBpm > 210.0) continue;
        
        auto testGrid = scoreBeatGrid(allTransients, testBpm, segmentDur);
        // We require a significant improvement (+0.25) to justify a jump
        if (testGrid.composite > bestComp + 0.25) {
          juce::Logger::writeToLog("[Refine] Polyrhythmic Snap: " + juce::String(bestBpm,1) + 
                                   " -> " + juce::String(testBpm,1) + " (score " + 
                                   juce::String(bestComp,2) + " -> " + juce::String(testGrid.composite,2) + ")");
          bestBpm = testBpm;
          bestComp = testGrid.composite;
          heuristicApplied = true;
        }
      }
      finalBpm = bestBpm;
    }

    if (!heuristicApplied) {
      finalBpm = normalizeTempoRangeAuto(finalBpm);
    }

    double heuristicAlt = 0;

    // === TRAP / TRESILLO CORRECTION (Fix 2 — ported from C# reference) ===
    if (finalBpm >= 95.0 && finalBpm <= 115.0) {
      // Combine both candidate lists for the guard check
      std::vector<BpmCandidate> combinedCandidates;
      combinedCandidates.insert(combinedCandidates.end(), gridCandidates.begin(), gridCandidates.end());
      combinedCandidates.insert(combinedCandidates.end(), sfCandidates.begin(), sfCandidates.end());

      if (sfConf < 0.6 && shouldApplyTrapHeuristic(finalBpm, combinedCandidates)) {
        double tresilloCandidate = finalBpm * TRESILLO_RATIO;
        juce::Logger::writeToLog(
            "[Trap] Tresillo correction: " + juce::String(finalBpm, 1) +
            " -> " + juce::String(tresilloCandidate, 1) + " BPM");
        heuristicAlt = finalBpm;
        finalBpm = tresilloCandidate;
        heuristicApplied = true;
      } else {
        juce::Logger::writeToLog("[Trap] Cancelled — evidence of real tempo or high SF confidence.");
      }
    }

    // Ambiguedad 2:1
    if (profile == BpmRangeProfile::Auto) {
      double beforeAmbiguity = finalBpm;
      finalBpm = resolveDoubleTimeAmbiguity(finalBpm, soundTouchBpm, gridBpm);
      if (std::abs(finalBpm - beforeAmbiguity) > 1.0) {
        heuristicApplied = true;
        heuristicAlt = beforeAmbiguity;
      }
    }

    // === HALF-TIME RESCUE (from Report) ===
    // Values in 60-77 BPM are almost never the real musical tempo.
    if (finalBpm >= 60.0 && finalBpm < 77.0 && !heuristicApplied) {
      // NEW: Check for Tresillo first (65 -> 98)
      double tresilloUp = finalBpm * 1.5;
      bool stSupportsTresillo =
          soundTouchBpm > 0 && std::abs(soundTouchBpm - tresilloUp) < 5.0;

      if (stSupportsTresillo) {
        juce::Logger::writeToLog(
            "[Trap] Tresillo Rescue (Low): " + juce::String(finalBpm, 1) +
            " -> " + juce::String(tresilloUp, 1) + " BPM");
        heuristicAlt = finalBpm;
        finalBpm = tresilloUp;
        heuristicApplied = true;
      } else {
        double doubleBpm = finalBpm * 2.0;
        juce::Logger::writeToLog(
            "[HalfTime] Rescue: " + juce::String(finalBpm, 1) + " -> " +
            juce::String(doubleBpm, 1) + " BPM");
        heuristicAlt = finalBpm;
        finalBpm = doubleBpm;
        heuristicApplied = true;
      }
    }

    // Final Range Selection
    finalBpm = selectBestCandidateForProfile(
        finalBpm, soundTouchBpm, gridCandidates, sfCandidates, profile);

    // Final Snap
    finalBpm = snapToInteger(finalBpm);

    result.primaryBpm = finalBpm;

    if (heuristicApplied && heuristicAlt > 0) {
      result.alternativeBpm = snapToInteger(heuristicAlt);
    } else {
      result.alternativeBpm = calculateAlternativeBpm(finalBpm);
    }

    if (progressCallback)
      progressCallback(100);
    juce::Logger::writeToLog(
        "BpmDetector: Final Result: " + juce::String(finalBpm) + " BPM");
  } catch (const std::exception &e) {
    juce::Logger::writeToLog("BpmDetector: Error: " + juce::String(e.what()));
  }

  return result;
}

double BpmDetector::detectWithSoundTouch(const std::vector<float> &monoSamples,
                                         int sampleRate) {
  try {
    soundtouch::BPMDetect bpmDetect(1, sampleRate);
    const int chunkSize = 4096;
    size_t offset = 0;

    while (offset < monoSamples.size()) {
      size_t remaining = monoSamples.size() - offset;
      int count = (int)std::min((size_t)chunkSize, remaining);
      bpmDetect.inputSamples(monoSamples.data() + offset, count);
      offset += count;
    }

    float bpm = bpmDetect.getBpm();
    return bpm > 0 ? std::round(bpm * 10.0) / 10.0 : 0.0;
  } catch (...) {
    return 0.0;
  }
}

std::vector<float>
BpmDetector::selectAnalysisSegment(const std::vector<float> &monoSamples,
                                   int sampleRate, double initialBpm) {
  double barDuration = 4.0 * (60.0 / initialBpm);
  double targetDuration = std::min(60.0, 32.0 * barDuration);
  int targetSamples = (int)(targetDuration * sampleRate);

  if (monoSamples.size() <= (size_t)targetSamples)
    return monoSamples;

  int windowSamples = sampleRate / 2;
  int numWindows = (int)(monoSamples.size() / windowSamples);
  if (numWindows < 4)
    return monoSamples;

  std::vector<double> rmsValues(numWindows);
  for (int w = 0; w < numWindows; ++w) {
    rmsValues[w] = DspUtils::computeRMS(
        monoSamples.data() + (w * windowSamples), windowSamples);
  }

  auto sorted = rmsValues;
  std::sort(sorted.begin(), sorted.end());
  double p75 = sorted[(int)(sorted.size() * 0.75)];
  double threshold = p75 * 0.6;

  int startWindow = 0;
  for (int w = 0; w < numWindows - 2; ++w) {
    if (rmsValues[w] > threshold && rmsValues[w + 1] > threshold &&
        rmsValues[w + 2] > threshold) {
      startWindow = w;
      break;
    }
  }

  if (startWindow == 0 && numWindows > 10)
    startWindow = numWindows / 10;

  int startSample = startWindow * windowSamples;
  if (startSample + targetSamples > (int)monoSamples.size())
    startSample = (int)std::max(0, (int)monoSamples.size() - targetSamples);

  std::vector<float> segment(
      monoSamples.begin() + startSample,
      monoSamples.begin() +
          std::min((size_t)(startSample + targetSamples), monoSamples.size()));

  return segment;
}

double BpmDetector::snapToInteger(double bpm) {
  if (bpm <= 0)
    return 0;
  double rounded = std::round(bpm);
  if (std::abs(bpm - rounded) < 0.4) // Slightly wider for high precision tracks
    return rounded;
  return std::round(bpm * 100.0) / 100.0; // Keep 2 decimals for more precision
}

double BpmDetector::calculateAlternativeBpm(double primaryBpm) {
  if (primaryBpm <= 0)
    return 0;
  double altBpm;

  // Check common harmonic relationships
  if (primaryBpm < 85)
    altBpm = primaryBpm * 2.0;
  else if (primaryBpm > 170)
    altBpm = primaryBpm / 2.0;
  else if (primaryBpm >= 140)
    altBpm = primaryBpm / 1.5; // Tresillo down
  else if (primaryBpm <= 115 && primaryBpm >= 90)
    altBpm = primaryBpm * 1.5; // Tresillo up
  else if (primaryBpm > 130)
    altBpm = primaryBpm / 2.0;
  else
    altBpm = primaryBpm * 2.0;

  return snapToInteger(altBpm);
}

std::pair<double, double>
BpmDetector::getProfileRange(BpmRangeProfile profile) {
  switch (profile) {
  case BpmRangeProfile::Low_50_100:
    return {50, 100};
  case BpmRangeProfile::Mid_75_150:
    return {75, 150};
  case BpmRangeProfile::High_100_200:
    return {100, 200};
  case BpmRangeProfile::VeryHigh_150_300:
    return {150, 300};
  default:
    return {0, 0};
  }
}

double BpmDetector::normalizeTempoRangeAuto(double bpm) {
  if (bpm <= 0) return 0;
  
  // Professional Urban Normalization
  // We respect the 70-75 range for slow Reggaeton.
  // But if the BPM is between 75 and 80, it's likely a half-time Trap/Pop track.
  if (bpm > 70.0 && bpm < 76.0) return bpm; // Keep slow Reggaeton
  
  while (bpm > 170.0) bpm /= 2.0;
  while (bpm < 76.0) bpm *= 2.0; // Force 76+ (Standard Trap/Urban floor)
  
  return bpm;
}

// --- Implementation of Advanced Detectors ---

void BpmDetector::detectBpmByTransientGrid(
    const std::vector<float> &samples, int sampleRate, double &outBpm,
    double &outConfidence, std::vector<BpmCandidate> &outCandidates) {
  // Simplified Transient Grid: Use onset envelope and autocorrelation
  auto onsetEnv = std::vector<float>(samples.size() / 128);
  for (size_t i = 0; i < onsetEnv.size(); ++i) {
    float energy = DspUtils::computeRMS(samples.data() + (i * 128), 128);
    onsetEnv[i] = energy;
  }

  // Difference (Flux-like)
  std::vector<float> flux(onsetEnv.size());
  for (size_t i = 1; i < onsetEnv.size(); ++i)
    flux[i] = std::max(0.0f, onsetEnv[i] - onsetEnv[i - 1]);

  outCandidates = estimateBpmFromOnsetStrength(flux, (double)sampleRate / 128.0, 50, 200);

  // --- NEW: Beat Grid Refinement ---
  auto transients = detectTransients(flux, (double)sampleRate / 128.0);
  double segmentDuration = (double)samples.size() / sampleRate;

  for (auto &c : outCandidates) {
    auto gridScore = scoreBeatGrid(transients, c.bpm, segmentDuration);
    // Hybrid score: 30% ACF (periodicity) + 70% Grid (phase alignment)
    c.score = (c.score * 0.3) + (gridScore.composite * 0.7);
  }

  // Re-sort by final hybrid score (descending)
  std::sort(outCandidates.begin(), outCandidates.end(),
            [](const auto &a, const auto &b) { return a.score > b.score; });

  if (!outCandidates.empty()) {
    outBpm = outCandidates[0].bpm;
    outConfidence = outCandidates[0].score;
  }
}

void BpmDetector::detectBpmBySpectralFlux(
    const std::vector<float> &samples, int sampleRate, double &outBpm,
    double &outConfidence, std::vector<BpmCandidate> &outCandidates) {
  auto flux =
      computeSpectralFlux(samples.data(), (int)samples.size(), sampleRate);
  outCandidates =
      estimateBpmFromOnsetStrength(flux, (double)sampleRate / (double)HOP_SIZE, 50, 200);

  // --- NEW: Beat Grid Refinement ---
  auto transients = detectTransients(flux, (double)sampleRate / (double)HOP_SIZE);
  double segmentDuration = (double)samples.size() / sampleRate;

  for (auto &c : outCandidates) {
    auto gridScore = scoreBeatGrid(transients, c.bpm, segmentDuration);
    // Hybrid score: 30% ACF (periodicity) + 70% Grid (phase alignment)
    c.score = (c.score * 0.3) + (gridScore.composite * 0.7);
  }

  // Re-sort by final hybrid score (descending)
  std::sort(outCandidates.begin(), outCandidates.end(),
            [](const auto &a, const auto &b) { return a.score > b.score; });

  if (!outCandidates.empty()) {
    outBpm = outCandidates[0].bpm;
    outConfidence = outCandidates[0].score;
  }
}

std::vector<float> BpmDetector::computeSpectralFlux(const float *samples,
                                                    int numSamples,
                                                    int /*sampleRate*/) {
  int numFrames = (numSamples - FFT_SIZE) / HOP_SIZE + 1;
  if (numFrames <= 0)
    return {};

  std::vector<float> flux(numFrames);
  std::vector<float> prevMag(FFT_SIZE / 2, 0.0f);

  juce::dsp::FFT fft(static_cast<int>(std::log2(FFT_SIZE)));
  juce::dsp::WindowingFunction<float> window(
      FFT_SIZE, juce::dsp::WindowingFunction<float>::hann);

  std::vector<float> fftBuffer(FFT_SIZE * 2);

  for (int f = 0; f < numFrames; ++f) {
    std::fill(fftBuffer.begin(), fftBuffer.end(), 0.0f);
    std::copy(samples + (f * HOP_SIZE), samples + (f * HOP_SIZE) + FFT_SIZE,
              fftBuffer.begin());

    window.multiplyWithWindowingTable(fftBuffer.data(), FFT_SIZE);
    fft.performFrequencyOnlyForwardTransform(fftBuffer.data());

    float currentFlux = 0.0f;
    for (int i = 0; i < FFT_SIZE / 2; ++i) {
      float mag = fftBuffer[i];
      float diff = mag - prevMag[i];
      if (diff > 0)
        currentFlux += diff * diff;
      prevMag[i] = mag;
    }
    flux[f] = std::sqrt(currentFlux);
  }

  return flux;
}

std::vector<BpmCandidate> BpmDetector::estimateBpmFromOnsetStrength(const std::vector<float>& onsetStrength, double sampleRate, double minBpm, double maxBpm) {
  std::vector<BpmCandidate> candidates;
  if (onsetStrength.size() < 100)
    return candidates;

  int minLag = (int)(sampleRate * 60.0 / maxBpm);
  int maxLag = (int)(sampleRate * 60.0 / minBpm);

  minLag = std::max(2, minLag); // Need at least 2 for interpolation
  maxLag = std::min(maxLag, (int)onsetStrength.size() - 2);

  std::vector<double> acf(maxLag + 2, 0.0);
  for (int lag = minLag - 1; lag <= maxLag + 1; ++lag) {
    if (lag < 0 || lag >= (int)onsetStrength.size()) continue;
    double sum = 0;
    int count = 0;
    for (size_t i = 0; i + lag < onsetStrength.size(); ++i) {
      sum += (double)onsetStrength[i] * (double)onsetStrength[i + lag];
      count++;
    }
    if (count > 0)
      acf[lag] = sum / count;
  }

  std::vector<std::pair<double, double>> peaks; // Use double for refined lag
  for (int lag = minLag; lag <= maxLag; ++lag) {
    if (acf[lag] > acf[lag - 1] && acf[lag] > acf[lag + 1]) {
      // Parabolic Interpolation for sub-sample precision
      double alpha = acf[lag - 1];
      double beta = acf[lag];
      double gamma = acf[lag + 1];

      double denom = alpha - 2.0 * beta + gamma;
      double p = 0;
      if (std::abs(denom) > 1e-10) {
        p = 0.5 * (alpha - gamma) / denom;
      }
      double refinedLag = (double)lag + p;
      peaks.push_back({refinedLag, beta});
    }
  }

  std::sort(peaks.begin(), peaks.end(),
            [](const auto &a, const auto &b) { return b.second < a.second; });

  for (size_t i = 0; i < std::min((size_t)5, peaks.size()); ++i) {
    double bpm = sampleRate * 60.0 / peaks[i].first;
    while (bpm < minBpm)
      bpm *= 2.0;
    while (bpm > maxBpm)
      bpm /= 2.0;

    double score = peaks[i].second / (acf[minLag] > 0 ? acf[minLag] : 1.0);
    candidates.push_back({bpm, std::min(1.0, score), "detector"});
  }

  return candidates;
}

double BpmDetector::voteThreeSources(
    double stBpm, double gridBpm, double gridConf, double sfBpm, double sfConf,
    const std::vector<BpmCandidate> &gridCandidates) {
  const double AGREEMENT_TOL = 5.0;

  // --- NEW: Dynamic Confusion Analysis ---
  double gridSpread = 0;
  if (gridCandidates.size() >= 3) {
    double scoreDiff = gridCandidates[0].score - gridCandidates[2].score;
    if (scoreDiff < 0.1) { // Top 3 are very close in score
      double bpmDiff = std::abs(gridCandidates[0].bpm - gridCandidates[1].bpm);
      if (bpmDiff > 10.0) gridSpread = 0.5; // High confusion
    }
  }

  // Adjust Grid confidence based on confusion
  double effectiveGridConf = gridConf * (1.0 - gridSpread);
  
  juce::Logger::writeToLog("[Vote] ST: " + juce::String(stBpm, 1) + 
                           " | Grid: " + juce::String(gridBpm, 1) + " (conf=" + juce::String(effectiveGridConf, 2) + ")" +
                           " | SF: " + juce::String(sfBpm, 1) + " (conf=" + juce::String(sfConf, 2) + ")");

  // Grid noise guard: if top 5 grid candidates are all in 185-200 and SF is weak
  if (gridBpm > 0 && gridCandidates.size() >= 5 && sfConf < 0.25) {
    bool allAtCeiling = true;
    for (size_t i = 0; i < 5; ++i) {
      if (gridCandidates[i].bpm < 185 || gridCandidates[i].bpm > 200) {
        allAtCeiling = false;
        break;
      }
    }
    if (allAtCeiling) {
      juce::Logger::writeToLog("[Vote] GRID NOISE GUARD: Candidates at ceiling, SF < 0.25. Discarding Grid.");
      gridBpm = 0;
      effectiveGridConf = 0;
    }
  }

  bool gridVsSf = gridBpm > 0 && sfBpm > 0 && std::abs(gridBpm - sfBpm) < AGREEMENT_TOL;
  bool stVsSf   = stBpm > 0 && sfBpm > 0 && std::abs(stBpm - sfBpm) < AGREEMENT_TOL;
  bool stVsGrid = stBpm > 0 && gridBpm > 0 && std::abs(stBpm - gridBpm) < AGREEMENT_TOL;

  // ── Case 1: Direct consensus between two sources ──
  if (gridVsSf) {
    double winner = effectiveGridConf >= sfConf ? gridBpm : sfBpm;
    if (stBpm > 0 && std::abs(stBpm - winner) > AGREEMENT_TOL) {
      double ratio = winner / stBpm;
      bool isHarmonic = std::abs(ratio - 0.5) < 0.06 || std::abs(ratio - 2.0) < 0.06 ||
                        std::abs(ratio - 1.5) < 0.06 || std::abs(ratio - 0.667) < 0.06 ||
                        std::abs(ratio - 1.333) < 0.06 || std::abs(ratio - 0.75) < 0.06 ||
                        std::abs(ratio - 1.25) < 0.06 || std::abs(ratio - 0.8) < 0.06; // Added 4:5 ratio
      if (isHarmonic) {
        juce::Logger::writeToLog("[Vote] CONSENSUS Grid+SF is harmonic of ST. Preferring ST.");
        return stBpm;
      }
      if (std::max(effectiveGridConf, sfConf) < 0.4) {
        juce::Logger::writeToLog("[Vote] CONSENSUS Grid+SF confidence insufficient, ST different. Preferring ST.");
        return stBpm;
      }
    }
    juce::Logger::writeToLog("[Vote] CONSENSUS Grid+SF -> " + juce::String(winner, 1));
    return winner;
  }
  
  if (stVsSf) {
    juce::Logger::writeToLog("[Vote] CONSENSUS ST+SF -> " + juce::String(sfBpm, 1));
    return sfBpm;
  }
  
  if (stVsGrid && effectiveGridConf > 0.3) {
    juce::Logger::writeToLog("[Vote] CONSENSUS ST+Grid -> " + juce::String(gridBpm, 1));
    return gridBpm;
  }

  // ── Case 2: Pulse Guard (ST vs SF) ──
  if (stBpm > 0 && sfBpm > 0) {
    double ratio = sfBpm / stBpm;
    // Tresillo (1.5) or 4:5 (1.25) ratios
    if (std::abs(ratio - 1.5) < 0.08 || std::abs(ratio - 0.667) < 0.08 || 
        std::abs(ratio - 1.25) < 0.08 || std::abs(ratio - 0.8) < 0.08) {
      if (sfConf < 0.7) { 
        juce::Logger::writeToLog("[Vote] PULSE GUARD: Harmonic ratio (" + juce::String(ratio, 2) + ") detected. Winning ST base -> " + juce::String(stBpm, 1));
        return stBpm;
      }
    }
  }

  // ── Case 3: Harmonic agreement (SF vs Grid) ──
  if (sfBpm > 0 && gridBpm > 0) {
    double ratio = sfBpm / gridBpm;
    if (std::abs(ratio - 1.5) < 0.08 || std::abs(ratio - 0.667) < 0.08 ||
        std::abs(ratio - 2.0) < 0.10 || std::abs(ratio - 0.5) < 0.10 ||
        std::abs(ratio - 1.25) < 0.08 || std::abs(ratio - 0.8) < 0.08) {
      double winner = sfConf >= effectiveGridConf ? sfBpm : gridBpm;
      juce::Logger::writeToLog("[Vote] HARMONIC SF/Grid -> " + juce::String(winner, 1));
      return winner;
    }
  }

  // ── Case 4: False half-time guard ──
  if (stBpm > 0 && gridBpm > 0 && std::abs(gridBpm - stBpm * 0.5) < 3.0) {
    juce::Logger::writeToLog("[Vote] HALF-TIME GUARD: Grid ~0.5x ST. Using ST -> " + juce::String(stBpm, 1));
    return stBpm;
  }
  if (stBpm > 0 && sfBpm > 0 && std::abs(sfBpm - stBpm * 0.5) < 3.0 && sfConf < 0.7) {
    juce::Logger::writeToLog("[Vote] HALF-TIME GUARD: SF ~0.5x ST. Using ST -> " + juce::String(stBpm, 1));
    return stBpm;
  }

  // ── Case 5: Priority by confidence ──
  if (sfBpm > 0 && sfConf > 0.3) {
    juce::Logger::writeToLog("[Vote] Priority SF (conf " + juce::String(sfConf, 2) + ") -> " + juce::String(sfBpm, 1));
    return sfBpm;
  }
  if (gridBpm > 0 && effectiveGridConf > 0.4) {
    juce::Logger::writeToLog("[Vote] Priority Grid (conf " + juce::String(effectiveGridConf, 2) + ") -> " + juce::String(gridBpm, 1));
    return gridBpm;
  }

  juce::Logger::writeToLog("[Vote] Priority ST fallback -> " + juce::String(stBpm, 1));
  return stBpm > 0 ? stBpm : (gridBpm > 0 ? gridBpm : sfBpm);
}

double BpmDetector::resolveDoubleTimeAmbiguity(double detectedBpm, double stBpm,
                                                double /*gridBpm*/) {
  double halfTime = detectedBpm / 2.0;
  // Only consider halving for high BPMs where double-time is plausible
  // Threshold aligned with C# reference: 145
  if (detectedBpm <= 145 || halfTime < 60 || halfTime > 105)
    return detectedBpm;

  bool stSupportsFullTime = stBpm > 0 && std::abs(stBpm - detectedBpm) < 8;
  bool stSupportsHalfTime = stBpm > 0 && std::abs(stBpm - halfTime) < 8;

  // If SoundTouch actively supports full-time, keep it
  if (stSupportsFullTime && !stSupportsHalfTime)
    return detectedBpm;

  // Halve if SoundTouch explicitly supports half-time OR if it falls in the natural zone
  // (C# behavior)
  if (stSupportsHalfTime || halfTime >= 70) {
    juce::Logger::writeToLog(
        "[DoubleTime] " + juce::String(detectedBpm, 1) + " -> " +
        juce::String(halfTime, 1) + " BPM (stSupportsHalf:" +
        juce::String(stSupportsHalfTime ? "true" : "false") + ")");
    return halfTime;
  }

  return detectedBpm;
}

double BpmDetector::selectBestCandidateForProfile(
    double currentBpm, double stBpm,
    const std::vector<BpmCandidate> &gridCandidates,
    const std::vector<BpmCandidate> &sfCandidates, BpmRangeProfile profile) {
  if (currentBpm <= 0)
    return 0;
  if (profile == BpmRangeProfile::Auto)
    return normalizeTempoRangeAuto(currentBpm);

  auto [minBpm, maxBpm] = getProfileRange(profile);

  std::vector<BpmCandidate> pool;
  pool.push_back({currentBpm, 1.0, "Winner"});
  if (stBpm > 0)
    pool.push_back({stBpm, 0.5, "ST"});
  for (const auto &c : gridCandidates)
    pool.push_back(c);
  for (const auto &c : sfCandidates)
    pool.push_back(c);

  std::vector<BpmCandidate> inRange;
  for (const auto &c : pool) {
    if (c.bpm >= minBpm && c.bpm <= maxBpm)
      inRange.push_back(c);
  }

  if (!inRange.empty()) {
    std::sort(inRange.begin(), inRange.end(),
              [](const auto &a, const auto &b) { return b.score < a.score; });
    return inRange[0].bpm;
  }

  // Fallback: harmonic adjustment
  double commonMultipliers[] = {2.0, 0.5, 1.5, 0.667, 3.0};
  for (auto mult : commonMultipliers) {
    double adjusted = currentBpm * mult;
    if (adjusted >= minBpm && adjusted <= maxBpm)
      return adjusted;
  }

  return currentBpm;
}

bool BpmDetector::shouldApplyTrapHeuristic(
    double stBpm, const std::vector<BpmCandidate> &candidates) {
  // Ported from C# ShouldApplyTrapHeuristic:
  // Verifies that a candidate with ratio 1.5x or 3.0x exists with sufficient score.
  // Dynamic minScore: when base BPM > 100, require higher confidence to avoid
  // false positives on tracks that are already at their real tempo (e.g. 108 BPM).
  if (candidates.empty())
    return false;

  for (const auto &candidate : candidates) {
    double ratio = candidate.bpm / stBpm;
    // Dynamic threshold: stricter when stBpm > 100
    double minScore = (stBpm > 100) ? -1.0 : -1.8;

    if ((std::abs(ratio - 1.5) < 0.05 || std::abs(ratio - 3.0) < 0.05) &&
        candidate.score > minScore) {
      juce::Logger::writeToLog(
          "[Trap Guard] Candidate " + juce::String(candidate.bpm, 1) +
          " confirms ratio " + juce::String(ratio, 2) + "x with score " +
          juce::String(candidate.score, 3) + " (min=" +
          juce::String(minScore, 1) + ")");
      return true;
    }
  }

  return false;
}

double
BpmDetector::checkBeatPeriodConsistency(const std::vector<float> &onsetStrength,
                                        double bpm, int sampleRate) {
  if (onsetStrength.empty())
    return 0.5;

  double secondsPerBeat = 60.0 / bpm;
  int framesPerBeat =
      (int)(secondsPerBeat *
            sampleRate); // Note: sampleRate here should be frames per second
  if (framesPerBeat < 1)
    framesPerBeat = 1;

  double consistency = 0;
  int count = 0;

  for (int i = framesPerBeat; i < (int)onsetStrength.size(); ++i) {
    double beatPhase = (double)(i % framesPerBeat) / framesPerBeat;
    if (beatPhase < 0.2 || beatPhase > 0.8) {
      consistency += onsetStrength[i];
      count++;
    }
  }

  if (count == 0)
    return 0.5;
  double avgOnset = consistency / count;

  double totalAvg = 0;
  for (float v : onsetStrength)
    totalAvg += v;
  totalAvg /= onsetStrength.size();

  return totalAvg > 0 ? std::min(1.0, avgOnset / totalAvg) : 0.5;
}
std::vector<Transient> BpmDetector::detectTransients(const std::vector<float>& envelope, double sampleRateEnvelope) {
  std::vector<Transient> transients;
  if (envelope.empty()) return transients;
  // Simple adaptive threshold: local average of 0.5s
  int windowSize = (int)(0.5 * sampleRateEnvelope);
  if (windowSize < 3) windowSize = 3;

  for (int i = 1; i < (int)envelope.size() - 1; ++i) {
    if (envelope[i] > envelope[i - 1] && envelope[i] > envelope[i + 1]) {
      // It's a peak, check threshold
      int start = std::max(0, i - windowSize / 2);
      int end = std::min((int)envelope.size(), i + windowSize / 2);
      float localSum = 0;
      for (int j = start; j < end; ++j) localSum += envelope[j];
      float localAvg = localSum / (end - start);

      if (envelope[i] > localAvg * 1.5f + 0.01f) {
        transients.push_back({(double)i / sampleRateEnvelope, envelope[i]});
      }
    }
  }
  return transients;
}

BeatGridScore BpmDetector::scoreBeatGrid(const std::vector<Transient>& transients, double bpm, double segmentDuration) {
  BeatGridScore bestScore = {0, 1.0, 0, -1.0};
  if (transients.empty() || bpm <= 0) return bestScore;

  double period = 60.0 / bpm;
  // Fixed tolerance of 50ms (industry standard for grid hits)
  double tolerance = 0.050; 
  if (tolerance > period * 0.25) tolerance = period * 0.25; // Max 25% of beat

  // Try 50 different offsets to find the best phase
  int numSteps = 50;
  for (int step = 0; step < numSteps; ++step) {
    double phase = (double)step * period / numSteps;
    int hits = 0;
    double totalDev = 0;

    for (const auto& t : transients) {
      double dist = std::fmod(t.position - phase + period * 1000.0, period);
      if (dist > period / 2.0) dist = period - dist;

      if (std::abs(dist) < tolerance) {
        hits++;
        totalDev += std::abs(dist) / tolerance;
      } else {
        double normDist = std::abs(dist) / period;
        // Case A: Half-time/Double-time ghost (0.5)
        if (normDist > 0.45 && normDist < 0.55) {
          totalDev += 0.6; // Strong penalty
        }
        // Case B: Tresillo ghost (0.33 or 0.66)
        else if (normDist > 0.31 && normDist < 0.35) {
          totalDev += 0.4; // Medium penalty
        }
        // Case C: Polyrhythmic ghost (0.2, 0.4, 0.6, 0.8)
        else if (normDist > 0.18 && normDist < 0.22) {
          totalDev += 0.2; 
        }
      }
    }

    double periodCount = segmentDuration / period;
    double hitRate = (double)hits / (periodCount > 0 ? periodCount : 1.0);
    double avgDev = hits > 0 ? (totalDev / hits) : 1.0;
    
    // --- NEW: Professional Pulse Density Bias ---
    // Higher BPMs are significantly favored if they maintain consistency, 
    // as they capture more rhythmic detail (hi-hats, ghost notes).
    double densityBias = std::log2(bpm / 60.0) * 0.18; 
    
    // Reward 'Hit Density': if we have many hits, it's a stronger pulse
    double hitDensityFactor = std::min(1.0, (double)hits / 15.0) * 0.15;

    // Composite: reward hits, penalize deviation/off-grid hits
    double composite = (hitRate * 1.65) + densityBias + hitDensityFactor - (avgDev * 1.0);

    if (composite > bestScore.composite) {
      bestScore = {hitRate, avgDev, phase, composite};
    }
  }

  return bestScore;
}

} // namespace ToneAndBeats