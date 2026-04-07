using System.IO;
using NAudio.Wave;
using AudioAnalyzer.Interfaces;
using AudioAnalyzer.Models;
using MediaInfo;

namespace AudioAnalyzer.Services;

public class AudioPlayerService : IAudioPlayerService
{
    private IWavePlayer? _waveOut;
    private AudioFileReader? _audioFile;
    private bool _disposed;

    public event EventHandler<PlaybackState>? PlaybackStateChanged;

    public PlaybackState State => _waveOut?.PlaybackState ?? PlaybackState.Stopped;
    public TimeSpan Position => _audioFile?.CurrentTime ?? TimeSpan.Zero;
    public TimeSpan Duration => _audioFile?.TotalTime ?? TimeSpan.Zero;
    public string? CurrentFile { get; private set; }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        PlaybackStateChanged?.Invoke(this, PlaybackState.Stopped);
    }

    public void LoadFile(string filePath)
    {
        Stop();
        DisposeAudio();

        if (!System.IO.File.Exists(filePath))
            throw new System.IO.FileNotFoundException("Audio file not found", filePath);

        try
        {
            _audioFile = new AudioFileReader(filePath);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioFile);
            _waveOut.PlaybackStopped += OnPlaybackStopped;
            CurrentFile = filePath;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Cannot load audio file: {ex.Message}", ex);
        }
    }

    public void Play()
    {
        _waveOut?.Play();
        PlaybackStateChanged?.Invoke(this, PlaybackState.Playing);
    }

    public void Pause()
    {
        _waveOut?.Pause();
        PlaybackStateChanged?.Invoke(this, PlaybackState.Paused);
    }

    public void Stop()
    {
        _waveOut?.Stop();
        PlaybackStateChanged?.Invoke(this, PlaybackState.Stopped);
    }

    public void UnloadFile()
    {
        Stop();
        DisposeAudio();
        CurrentFile = null;
    }

    public void Seek(TimeSpan position)
    {
        if (_audioFile != null)
        {
            _audioFile.CurrentTime = position;
        }
    }

    public float GetSampleRate()
    {
        return _audioFile?.WaveFormat.SampleRate ?? 44100;
    }

    public int GetChannelCount()
    {
        return _audioFile?.WaveFormat.Channels ?? 2;
    }

    public AudioFileInfo? GetAudioFileInfo()
    {
        try
        {
            LoggerService.Log("GetAudioFileInfo() - Iniciando");

            if (_audioFile == null)
            {
                LoggerService.Log("GetAudioFileInfo() - _audioFile es NULL");
                return null;
            }

            var format = _audioFile.WaveFormat;
            string fileType = Path.GetExtension(CurrentFile)?.ToUpper().TrimStart('.') ?? "Unknown";

            var info = new AudioFileInfo
            {
                FileType = fileType,
                SampleRate = format.SampleRate,
                BitDepth = format.BitsPerSample,
                Channels = format.Channels
            };

            LoggerService.Log($"GetAudioFileInfo() - FileType: {info.FileType}");
            LoggerService.Log($"GetAudioFileInfo() - SampleRate: {info.SampleRate}");
            LoggerService.Log($"GetAudioFileInfo() - BitDepth: {info.BitDepth}");
            LoggerService.Log($"GetAudioFileInfo() - Channels: {info.Channels}");

            if (!string.IsNullOrEmpty(CurrentFile))
            {
                try
                {
                    var mediaInfo = new MediaInfoWrapper(CurrentFile);
                    if (mediaInfo.Success && mediaInfo.AudioStreams.Any())
                    {
                        var audioStream = mediaInfo.AudioStreams.First();
                        info.Bitrate = (int)(audioStream.Bitrate / 1000);
                        info.BitrateMode = audioStream.BitrateMode.ToString();
                        LoggerService.Log($"GetAudioFileInfo() - Bitrate: {info.Bitrate} kbps");
                        LoggerService.Log($"GetAudioFileInfo() - BitrateMode: {info.BitrateMode}");
                    }
                }
                catch (Exception ex)
                {
                    LoggerService.Log($"GetAudioFileInfo() - MediaInfo error: {ex.Message}");
                }
            }

            return info;
        }
        catch (Exception ex)
        {
            LoggerService.Log($"GetAudioFileInfo() - ERROR: {ex.Message}");
            return null;
        }
    }

    private void DisposeAudio()
    {
        if (_waveOut != null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Dispose();
            _waveOut = null;
        }
        
        _audioFile?.Dispose();
        _audioFile = null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DisposeAudio();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}