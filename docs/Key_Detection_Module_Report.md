# Reporte de Configuración: Módulo de Detección de Tonalidad (KeyDetector)

Este documento detalla el funcionamiento interno, la configuración técnica y los algoritmos utilizados por el módulo `KeyDetector` en el proyecto **Tone and Beats**.

## 1. Metodología de Detección
El sistema utiliza un algoritmo basado en **Perfiles de Clase de Altura (Pitch Class Profile - PCP)**, también conocido como Chromagram. El método compara el contenido espectral del audio con plantillas predefinidas de escalas mayores y menores.

### Proceso Paso a Paso:
1.  **Selección de Segmento**: Se extrae un segmento de **30 segundos** del centro del archivo de audio para evitar introducciones o finales silenciosos/atípicos.
2.  **Análisis Espectral (FFT)**: Se aplica una Transformada Rápida de Fourier para convertir el audio del dominio del tiempo al de la frecuencia.
3.  **Generación de Chromagram**: Se calcula la energía acumulada para cada una de las 12 notas de la escala cromática (C, C#, D, etc.).
4.  **Suma Armónica**: Para cada nota, se analiza no solo su frecuencia fundamental, sino también hasta **8 armónicos**, lo que mejora la precisión en instrumentos con timbres complejos.
5.  **Correlación de Plantillas**: El perfil obtenido se compara (mediante rotación y correlación estadística) con perfiles ideales de escalas Mayores y Menores.

## 2. Configuración Técnica y Umbrales

| Parámetro | Valor | Descripción |
| :--- | :--- | :--- |
| **Tamaño de FFT** | 16,384 muestras | Alta resolución frecuencial para distinguir semitonos. |
| **Tamaño de Salto (Hop Size)** | 8,192 muestras | Solapamiento del 50% entre ventanas de análisis. |
| **Ventana de Suavizado** | Hann | Reduce el "spectral leakage" en los bordes de la ventana. |
| **Rango de Análisis** | 30 segundos | Tiempo máximo de procesamiento por archivo. |
| **Armónicos** | 8 | Cantidad de sobretonos sumados a la fundamental. |
| **Frecuencia Base (A4)** | 440.0 Hz | Referencia para el cálculo de bins de frecuencia. |

## 3. Perfiles de Tonalidad (Templates)
El sistema utiliza perfiles de peso para cada grado de la escala, basados en la importancia estadística de las notas en la música occidental:

*   **Perfil Mayor**: `{ 6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88 }`
*   **Perfil Menor**: `{ 6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17 }`

## 4. Filtros y Refinamiento
*   **Interpolación Espectral**: Al calcular la energía de una nota, el sistema promedia la magnitud del bin central con sus vecinos inmediatos (`magnitudes[freqBin] + 0.5 * (magnitudes[freqBin-1] + magnitudes[freqBin+1])`) para mitigar errores de cuantización de frecuencia.
*   **Normalización**: El perfil PCP resultante se normaliza por la energía total antes de la comparación, haciendo que el sistema sea independiente del volumen del audio.
*   **Cálculo de Confianza**: Se deriva de la correlación de Pearson. Un valor cercano a 1.0 indica una coincidencia casi perfecta con la plantilla.

---

## 5. Código Fuente del Módulo (`KeyDetector.cs`)

```csharp
using AudioAnalyzer.Interfaces;

namespace AudioAnalyzer.Services;

public class KeyDetector : IKeyDetectorService
{
    private static readonly double[] MajorProfile = { 6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88 };
    private static readonly double[] MinorProfile = { 6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17 };

    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public async Task<(string Key, string Mode, double Confidence)> DetectKeyAsync(string filePath, IProgress<int>? progress = null)
    {
        return await Task.Run(() => DetectKey(filePath, progress));
    }

    public async Task<(string Key, string Mode, double Confidence)> DetectKeyAsync(float[] monoSamples, int sampleRate, IProgress<int>? progress = null)
    {
        return await Task.Run(() => DetectKeyFromSamples(monoSamples, sampleRate, progress));
    }

    public (string Key, string Mode, double Confidence) DetectKey(string filePath, IProgress<int>? progress = null)
    {
        var (monoSamples, sampleRate) = new AudioDataProvider().LoadMono(filePath);
        return DetectKeyFromSamples(monoSamples, sampleRate, progress);
    }

    private (string Key, string Mode, double Confidence) DetectKeyFromSamples(float[] monoSamples, int sampleRate, IProgress<int>? progress = null)
    {
        const int MaxAnalysisSeconds = 30;

        try
        {
            if (monoSamples.Length < sampleRate)
                return ("Unknown", "Unknown", 0);

            // Limit to MaxAnalysisSeconds from center of audio for key detection
            int maxSamples = MaxAnalysisSeconds * sampleRate;
            float[] analysisData;
            if (monoSamples.Length > maxSamples)
            {
                int startOffset = (monoSamples.Length - maxSamples) / 2;
                analysisData = monoSamples.AsSpan(startOffset, maxSamples).ToArray();
            }
            else
            {
                analysisData = monoSamples;
            }

            progress?.Report(20);
            var pcp = ComputePitchClassProfile(analysisData, sampleRate);
            
            progress?.Report(50);
            var (keyIndex, mode, correlation) = FindBestKey(pcp);
            
            progress?.Report(100);
            return (NoteNames[keyIndex], mode == 0 ? "Major" : "Minor", correlation);
        }
        catch (Exception ex)
        {
            return ("Error", "Error", 0);
        }
    }

    private double[] ComputePitchClassProfile(float[] samples, int sampleRate)
    {
        const int fftSize = 16384; // DspConstants.FFT_SIZE_KEY_DETECTION
        const int hopSize = 8192;
        const int numBins = 12;
        const double a4Freq = 440.0;

        var pcp = new double[numBins];
        int numFrames = (samples.Length - fftSize) / hopSize + 1;
        if (numFrames <= 0) numFrames = 1;

        var magnitudes = new double[fftSize / 2];

        for (int frame = 0; frame < numFrames; frame++)
        {
            var frameStart = frame * hopSize;
            if (frameStart + fftSize > samples.Length) break;

            Array.Clear(magnitudes, 0, magnitudes.Length);
            var window = samples.AsSpan(frameStart, fftSize);
            ComputeFFTMagnitudes(window, magnitudes, sampleRate);

            for (int pitchClass = 0; pitchClass < numBins; pitchClass++)
            {
                double pitchEnergy = 0;
                double c0Freq = a4Freq * Math.Pow(2, (pitchClass - 9) / 12.0);

                for (int harmonic = 1; harmonic <= 8; harmonic++)
                {
                    double harmonicFreq = c0Freq * harmonic;
                    if (harmonicFreq > sampleRate / 2) break;

                    int freqBin = (int)(harmonicFreq * fftSize / sampleRate);
                    if (freqBin > 0 && freqBin < magnitudes.Length - 1)
                    {
                        double interpolatedMag = magnitudes[freqBin] + 0.5 * (magnitudes[freqBin - 1] + magnitudes[freqBin + 1]);
                        pitchEnergy += interpolatedMag / harmonic;
                    }
                }
                pcp[pitchClass] += pitchEnergy;
            }
        }

        double totalEnergy = pcp.Sum();
        if (totalEnergy > 0)
        {
            for (int i = 0; i < numBins; i++)
                pcp[i] /= totalEnergy;
        }

        return pcp;
    }

    private void ComputeFFTMagnitudes(Span<float> window, double[] magnitudes, int sampleRate)
    {
        int n = magnitudes.Length * 2;
        var complex = new System.Numerics.Complex[n];

        for (int i = 0; i < n; i++)
        {
            double hann = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (n - 1)));
            complex[i] = new System.Numerics.Complex(i < window.Length ? window[i] * hann : 0, 0);
        }

        FftHelper.FFT(complex);

        for (int i = 0; i < magnitudes.Length; i++)
        {
            magnitudes[i] = Math.Sqrt(complex[i].Real * complex[i].Real + complex[i].Imaginary * complex[i].Imaginary);
        }
    }

    private (int keyIndex, int mode, double correlation) FindBestKey(double[] pcp)
    {
        double bestCorrelation = -1;
        int bestKey = 0;
        int bestMode = 0;

        for (int root = 0; root < 12; root++)
        {
            var rotatedPcp = RotateArray(pcp, root);

            double majorCorr = ComputeCorrelation(rotatedPcp, MajorProfile);
            if (majorCorr > bestCorrelation)
            {
                bestCorrelation = majorCorr;
                bestKey = root;
                bestMode = 0;
            }

            double minorCorr = ComputeCorrelation(rotatedPcp, MinorProfile);
            if (minorCorr > bestCorrelation)
            {
                bestCorrelation = minorCorr;
                bestKey = root;
                bestMode = 1;
            }
        }

        double normalizedConfidence = Math.Min(1.0, Math.Max(0, (bestCorrelation + 1) / 2));
        return (bestKey, bestMode, normalizedConfidence);
    }

    private double[] RotateArray(double[] arr, int shift)
    {
        var result = new double[arr.Length];
        for (int i = 0; i < arr.Length; i++)
            result[i] = arr[(i + shift) % arr.Length];
        return result;
    }

    private double ComputeCorrelation(double[] a, double[] b)
    {
        double sumAB = 0, sumA2 = 0, sumB2 = 0;
        for (int i = 0; i < a.Length; i++)
        {
            sumAB += a[i] * b[i];
            sumA2 += a[i] * a[i];
            sumB2 += b[i] * b[i];
        }

        double denominator = Math.Sqrt(sumA2) * Math.Sqrt(sumB2);
        return denominator > 0 ? sumAB / denominator : 0;
    }
}
```
