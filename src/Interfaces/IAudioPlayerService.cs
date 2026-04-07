using AudioAnalyzer.Models;

namespace AudioAnalyzer.Interfaces;

public interface IAudioPlayerService : IDisposable
{
    event EventHandler<NAudio.Wave.PlaybackState>? PlaybackStateChanged;
    NAudio.Wave.PlaybackState State { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    string? CurrentFile { get; }
    
    void LoadFile(string filePath);
    void Play();
    void Pause();
    void Stop();
    void UnloadFile();
    void Seek(TimeSpan position);
    float GetSampleRate();
    int GetChannelCount();
    AudioFileInfo? GetAudioFileInfo();
}