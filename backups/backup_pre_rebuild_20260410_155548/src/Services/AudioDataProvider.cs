namespace AudioAnalyzer.Services;

/// <summary>
/// Centralizes audio file loading to eliminate redundant I/O.
/// Loads the file ONCE and provides mono float[] samples to all analyzers.
/// </summary>
public sealed class AudioDataProvider
{
    /// <summary>
    /// Loads an audio file and converts it to mono float[] samples.
    /// This should be called ONCE per analysis, and the result shared across all services.
    /// </summary>
    public (float[] MonoSamples, int SampleRate) LoadMono(string filePath)
    {
        var (sampleProvider, waveStream) = AudioReaderFactory.CreateReader(filePath);
        using (waveStream)
        {
            var sampleRate = waveStream.WaveFormat.SampleRate;
            var channels = waveStream.WaveFormat.Channels;

            // Pre-allocate with exact capacity to avoid List<T> resizing
            var totalFloats = (int)(waveStream.Length / sizeof(float));
            var estimatedMono = totalFloats / channels;
            var samples = new List<float>(estimatedMono);

            var buffer = new float[totalFloats];
            int read = sampleProvider.Read(buffer, 0, buffer.Length);

            for (int i = 0; i < read; i += channels)
            {
                float sum = 0;
                for (int c = 0; c < channels && i + c < read; c++)
                    sum += buffer[i + c];
                samples.Add(sum / channels);
            }

            LoggerService.Log($"AudioDataProvider.LoadMono - Loaded {samples.Count} mono samples @ {sampleRate}Hz from: {filePath}");
            return (samples.ToArray(), sampleRate);
        }
    }
}
