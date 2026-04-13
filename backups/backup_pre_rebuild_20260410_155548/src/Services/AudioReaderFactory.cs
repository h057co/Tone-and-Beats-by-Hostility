using System;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AudioAnalyzer.Services;

public static class AudioReaderFactory
{
    public static (ISampleProvider sampleProvider, WaveStream waveStream) CreateReader(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        
        WaveStream waveStream;
        
        if (extension == ".ogg")
        {
            waveStream = new NAudio.Vorbis.VorbisWaveReader(filePath);
        }
        else
        {
            waveStream = new AudioFileReader(filePath);
        }
        
        var sampleProvider = waveStream.ToSampleProvider();
        
        return (sampleProvider, waveStream);
    }
}
