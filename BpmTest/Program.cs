using System;
using System.IO;
using AudioAnalyzer.Services;

Console.WriteLine("=== BPM DETECTION TEST ===\n");

var testFiles = new[]
{
    (@"..\Assets\audiotest\Ta Buena Rancha.mp3", 108.0, "Reggaeton"),
    (@"..\Assets\audiotest\sin master.mp3", 152.0, "Trap (sin master)"),
    (@"..\Assets\audiotest\sin master.wav", 152.0, "Trap (sin master WAV)"),
    (@"..\Assets\audiotest\master.mp3", 152.0, "Trap (master MP3)"),
    (@"..\Assets\audiotest\master.wav", 152.0, "Trap (master WAV)"),
};

foreach (var (path, expectedBpm, description) in testFiles)
{
    if (!File.Exists(path))
    {
        Console.WriteLine($"[SKIP] File not found: {path}");
        continue;
    }

    Console.WriteLine($"--- {description} ---");
    Console.WriteLine($"File: {Path.GetFileName(path)}");
    Console.WriteLine($"Expected: {expectedBpm} BPM");

    var bpmDetector = new BpmDetector();
    var (primaryBpm, altBpm) = bpmDetector.DetectBpm(path);

    double error = Math.Abs(primaryBpm - expectedBpm);
    double accuracy = 100 - (error / expectedBpm * 100);
    string status = accuracy >= 99 ? "OK" : accuracy >= 90 ? "WARN" : "GRACEFUL";

    Console.WriteLine($"Detected: {primaryBpm:F1} BPM | Alt: {altBpm:F1} BPM");
    Console.WriteLine($"Error: {error:F1} BPM | Accuracy: {accuracy:F0}%");
    Console.WriteLine($"[ {status} ]\n");
}
