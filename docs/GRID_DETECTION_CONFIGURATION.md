# WaveformAnalyzer - Grid Detection Configuration Report

**Fecha:** 12 de Abril de 2026  
**Proyecto:** Tone & Beats by Hostility  
**Archivo:** `Services/WaveformAnalyzer.cs`

---

## 1. RESUMEN EJECUTIVO

El módulo `WaveformAnalyzer` utiliza un sistema de **Grid Detection** basado en transientes para estimar el BPM. Este sistema tiene 5 etapas principales:

1. **Dual-Band Isolation** - Separa frecuencias bajas y altas
2. **Transient Detection** - Detecta golpes/onsets en cada banda
3. **Transient Merging** - Combina transientes de ambas bandas
4. **Autocorrelation** - Encuentra candidatos BPM mediante autocorrelación
5. **Grid Fitting** - Evalúa qué candidato genera el grid más preciso

---

## 2. FLUJO DEL ALGORITMO

```
monoSamples (float[])
    │
    ▼
┌─────────────────────────────┐
│ Step 1: IsolateTransientBands │
│ - Low-pass filter ≤ 150 Hz   │
│ - Band-pass 2000-8000 Hz     │
└─────────────┬───────────────┘
              │
              ▼
┌─────────────────────────────┐
│ Step 2: DetectTransients     │
│ - Low band: threshold x2.0   │
│ - Hi band: threshold x1.8     │
│ - Dead time: 30ms            │
└─────────────┬───────────────┘
              │
              ▼
┌─────────────────────────────┐
│ Step 3: MergeTransients     │
│ - Combina ambas bandas      │
│ - Deduplica en 15ms         │
└─────────────┬───────────────┘
              │
              ▼
┌─────────────────────────────┐
│ Step 4: AutocorrelateTrans  │
│ - Busca periodicidad        │
│ - Rango: 50-200 BPM         │
│ - Top 5 candidatos          │
└─────────────┬───────────────┘
              │
              ▼
┌─────────────────────────────┐
│ Step 5: ScoreBeatGrid       │
│ - Evalúa precisión          │
│ - Tolerance: 20ms           │
│ - Composite = hitRate-2*std │
└─────────────┬───────────────┘
              │
              ▼
        (bpm, confidence)
```

---

## 3. CONFIGURACIÓN DE CONSTANTES

### DspConstants.cs (Services/DspConstants.cs)

```csharp
public static class DspConstants
{
    // FFT Configuration
    public const int FFT_SIZE = 2048;
    public const int FFT_SIZE_KEY_DETECTION = 16384;
    public const int HOP_SIZE = 512;

    // Frequency Cutoffs (Hz)
    public const double LOW_FREQ_CUTOFF = 200.0;
    public const double TRANSIENT_LOW_BAND = 150.0;
    public const double TRANSIENT_HIGH_BAND_MIN = 2000.0;
    public const double TRANSIENT_HIGH_BAND_MAX = 8000.0;

    // Timing (seconds)
    public const double TRANSIENT_DEAD_TIME = 0.030;
    public const double DUPLICATE_THRESHOLD = 0.015;

    // Energy Thresholds
    public const double ENERGY_THRESHOLD_LOW = 0.5;
    public const double ENERGY_THRESHOLD_HIGH = 0.2;

    // BPM Detection
    public const int BPM_RANGE_MIN = 50;
    public const int BPM_RANGE_MAX = 200;
}
```

---

## 4. PARÁMETROS CONFIGURABLES POR ETAPA

### Step 1: Dual-Band Isolation
**Método:** `IsolateTransientBands()` (línea 741)

```csharp
public (float[] lowBand, float[] hiBand) IsolateTransientBands(float[] samples, int sampleRate)
{
    var lowBand = ApplyLowPassFilter(samples, sampleRate, DspConstants.TRANSIENT_LOW_BAND);
    var hiBand = ApplyBandPassFilter(samples, sampleRate, DspConstants.TRANSIENT_HIGH_BAND_MIN, DspConstants.TRANSIENT_HIGH_BAND_MAX);
    return (lowBand, hiBand);
}
```

| Parámetro | Valor Actual | Descripción |
|-----------|--------------|-------------|
| `TRANSIENT_LOW_BAND` | 150 Hz | Frecuencia máxima para banda baja |
| `TRANSIENT_HIGH_BAND_MIN` | 2000 Hz | Inicio de banda alta |
| `TRANSIENT_HIGH_BAND_MAX` | 8000 Hz | Fin de banda alta |

---

### Step 2: Transient Detection
**Método:** `DetectTransients()` (línea 752)

```csharp
public List<(double position, double amplitude)> DetectTransients(
    float[] signal, int sampleRate, double thresholdMultiplier = 2.0)
{
    int windowSize = Math.Max(1, sampleRate / 100);  // ~10ms
    int hopSize = windowSize / 2;
    double deadTimeSec = DspConstants.TRANSIENT_DEAD_TIME;  // 30ms
    int thresholdWindowFrames = Math.Max(3, (int)(0.200 / (hopSize / (double)sampleRate)));
    
    // Detección: rise > 0 && energy[f] > localAvg * thresholdMultiplier
}
```

| Parámetro | Valor Actual | Efecto |
|-----------|--------------|--------|
| `thresholdMultiplier` (low) | **2.0** | Más selectivo, menos transientes |
| `thresholdMultiplier` (hi) | **1.8** | Menos selectivo, más transientes |
| `windowSize` | sampleRate/100 (~10ms) | Granularidad de detección |
| `deadTimeSec` | 30ms | Mínimo tiempo entre transientes |
| `thresholdWindowFrames` | ~200ms | Ventana para promedio local |

---

### Step 3: Transient Merging
**Método:** `MergeTransients()` (línea 810)

| Parámetro | Valor Actual | Efecto |
|-----------|--------------|--------|
| `DUPLICATE_THRESHOLD` | 15ms | Si dos transientes < 15ms, se mergean |

---

### Step 4: Autocorrelation
**Método:** `AutocorrelateTransients()` (línea 845)

```csharp
var candidates = AutocorrelateTransients(allTransients, 50, 200);
```

| Parámetro | Valor Actual | Efecto |
|-----------|--------------|--------|
| Rango mínimo BPM | 50 | No busca por debajo de 50 |
| Rango máximo BPM | 200 | No busca por encima de 200 |
| Top candidatos | 5 | Solo los 5 mejores se evalúan en Grid Fit |

---

### Step 5: Grid Fitting (CRÍTICO)
**Método:** `ScoreBeatGrid()` (línea 909)

```csharp
public (double stdDev, double hitRate, double bestDownbeat) ScoreBeatGrid(
    List<(double position, double amplitude)> transients, double candidateBpm, double segmentDuration)
{
    // Tolerance: 20ms para considerar "hit"
    if (minDist < 0.020) hits++;  // 20ms tolerance
    
    // Composite score
    double composite = hitRate - (stdDev * 2.0);
}
```

| Parámetro | Valor Actual | Efecto |
|-----------|--------------|--------|
| `hitTolerance` | 20ms | Si transient está a <20ms del grid, cuenta como hit |
| `compositeFormula` | `hitRate - (stdDev * 2.0)` | Ponderación de scoring |
| `minTransients` | 8 | Mínimo de transientes para evaluar |

---

## 5. LOGS DE DEBUG REALES

### Caso: master bpm 152.mp3 (FALLO)
```
[2026-04-12 14:45:11.615] WaveformAnalyzer.TransientGrid - Low-band transients: 96, Hi-band transients: 298
[2026-04-12 14:45:11.616] WaveformAnalyzer.TransientGrid - Merged transients: 380
[2026-04-12 14:45:11.628] WaveformAnalyzer.TransientGrid - Top candidates: 152,0(0,19), 195,5(0,14), 196,0(0,13), 171,0(0,13), 182,5(0,13)
[2026-04-12 14:45:11.628] WaveformAnalyzer.GridFit - BPM 152,0: stdDev=0,0248, hitRate=0,05, composite=-0,002
[2026-04-12 14:45:11.629] WaveformAnalyzer.GridFit - BPM 195,5: stdDev=0,0345, hitRate=0,30, composite=0,232
[2026-04-12 14:45:11.630] WaveformAnalyzer.GridFit - BPM 196,0: stdDev=0,0330, hitRate=0,36, composite=0,295
[2026-04-12 14:45:11.630] WaveformAnalyzer.GridFit - BPM 171,0: stdDev=0,0332, hitRate=0,29, composite=0,219
[2026-04-12 14:45:11.631] WaveformAnalyzer.GridFit - BPM 182,5: stdDev=0,0378, hitRate=0,23, composite=0,154
[2026-04-12 14:45:11.631] WaveformAnalyzer.TransientGrid - Best: 171,0 BPM (stdDev=0,0356, hitRate=0,41, composite=0,343)
```

**Análisis:** El candidato 152 tiene `hitRate=0.05` (¡muy bajo!) pero `stdDev=0.0248` (aceptable). Composite = **-0.002** (negativo).

---

### Caso: sin master bpm 152.wav (ÉXITO)
```
[2026-04-12 14:45:16.848] WaveformAnalyzer.GridFit - BPM 152,0: stdDev=0,0042, hitRate=1,00, composite=0,992
[2026-04-12 14:45:16.850] WaveformAnalyzer.TransientGrid - Best: 152,0 BPM (stdDev=0,0042, hitRate=1,00, composite=0,992)
```

**Análisis:** El candidato 152 tiene `hitRate=1.00` (perfecto) y `stdDev=0.0042` (excelente). Composite = **0.992**.

---

## 6. ANÁLISIS DEL PROBLEMA

| Caso | BPM Esperado | Grid Best | hitRate | stdDev | Composite | Resultado |
|------|-------------|-----------|---------|--------|-----------|-----------|
| master 152.mp3 | 152 | 171.0 | 0.41 | 0.0356 | 0.343 | 171 (FAIL) |
| sin master 152.wav | 152 | 152.0 | 1.00 | 0.0042 | 0.992 | 152 (OK) |

**Hallazgo clave:** En master 152.mp3, el candidato 152 tenía `hitRate=0.05` (5%). La masterización (compresión/limiting) destruye la detección de transientes, causando que el grid no pueda alinear correctamente los beats para 152 BPM.

---

## 7. AJUSTES PROPUESTOS

| # | Parámetro | Archivo | Línea | Actual | Propuesto | Efecto |
|---|-----------|---------|-------|--------|-----------|--------|
| 1 | `thresholdMultiplier` (low) | WaveformAnalyzer.cs | 989 | 2.0 | **1.5** | Más transientes detectados |
| 2 | `thresholdMultiplier` (hi) | WaveformAnalyzer.cs | 990 | 1.8 | **1.3** | Más transientes detectados |
| 3 | `hitTolerance` | WaveformAnalyzer.cs | 954 | 20ms | **30ms** | Más beats cuentan como hits |
| 4 | `compositeFormula` | WaveformAnalyzer.cs | 1025 | `hitRate - stdDev*2` | `(hitRate * 1.5) - stdDev` | Más peso a hitRate |
| 5 | `bpmRangeMax` | DspConstants.cs | 26 | 200 | **250** | Para trap/trap duro |

---

## 8. CÓDIGO DE DETECCIÓN ACTUAL

### DetectBpmByTransientGrid() - Método Principal

```csharp
public (double bpm, double confidence) DetectBpmByTransientGrid(float[] monoSamples, int sampleRate)
{
    if (monoSamples.Length < sampleRate * 5) return (0, 0);

    double segmentDuration = monoSamples.Length / (double)sampleRate;

    // Step 1: Dual-band isolation
    var (lowBand, hiBand) = IsolateTransientBands(monoSamples, sampleRate);

    // Step 2: Detect transients in each band
    var lowTransients = DetectTransients(lowBand, sampleRate, 2.0);  // THRESHOLD 2.0
    var hiTransients = DetectTransients(hiBand, sampleRate, 1.8);   // THRESHOLD 1.8

    // Step 3: Merge transients
    var allTransients = MergeTransients(lowTransients, hiTransients);

    if (allTransients.Count < 15) return (0, 0);

    // Step 4: Autocorrelation on transient positions → top candidates
    var candidates = AutocorrelateTransients(allTransients, 50, 200);  // RANGO 50-200
    if (candidates.Count == 0) return (0, 0);

    // Step 5: Beat Grid Fitting for top candidates
    double bestBpm = 0;
    double bestComposite = -1;

    foreach (var candidate in candidates.Take(5))
    {
        var (stdDev, hitRate, _) = ScoreBeatGrid(allTransients, candidate.bpm, segmentDuration);

        // Composite score: hitRate weighted heavily, penalize high stdDev
        double composite = hitRate - (stdDev * 2.0);  // FÓRMULA ACTUAL

        if (composite > bestComposite)
        {
            bestComposite = composite;
            bestBpm = candidate.bpm;
        }
    }

    double confidence = Math.Min(1.0, bestHitRate);
    return (bestBpm, confidence);
}
```

---

## 9. PRÓXIMOS PASOS

1. [ ] Reducir `thresholdMultiplier` a 1.5/1.3 para audio masterizado
2. [ ] Aumentar `hitTolerance` a 30ms
3. [ ] Ajustar fórmula composite para dar más peso a hitRate
4. [ ] Ampliar rango BPM máximo a 250
5. [ ] Re-testear con el set de 21 audios
6. [ ] Verificar que no se introduzcan regresiones

---

*Reporte generado: 2026-04-12*  
*Análisis basado en logs de ejecución real*