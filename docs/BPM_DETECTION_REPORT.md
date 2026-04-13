# BPM Detection Problem Report

**Fecha:** 12 de Abril de 2026  
**Proyecto:** Tone & Beats by Hostility  
**Estado:** En desarrollo - Algoritmo de detección BPM

---

## 1. RESUMEN DEL PROBLEMA

El módulo de detección BPM de Tone & Beats presenta fallos sistemáticos en ciertos tipos de audio, especialmente:

| Audio | BPM Esperado | BPM Detectado | Error |
|-------|-------------|---------------|-------|
| `audio11 bpm 82.mp3` | 82 | 164.5 | 100.6% |
| `audio5 bpm 76,665.m4a` | 76.7 | 57.5 | 25.0% |
| `master bpm 152.mp3` | 152 | 101.4 | 33.3% |
| `master bpm 152.wav` | 152 | 76.0 | 50.0% |
| `sin master bpm 152.mp3` | 152 | 76.0 | 50.0% |

**Patrón identificado:** El algoritmo detecta correctamente el tempo alternativo en casi todos los casos (la alternativa muestra el valor correcto). El problema es la selección del BPM primario.

---

## 2. ARQUITECTURA DEL MÓDULO BPM

### 2.1 Flujo de Detección

```
Audio File
    │
    ▼
┌─────────────────┐
│ AudioDataProvider │ → Carga archivo y convierte a mono samples
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ SoundTouch      │ → Estimación rápida de BPM (BpmDetect)
│ (BpmDetect)     │   Output: soundTouchBpm
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Segment Selector│ → Selecciona 60s del audio para análisis
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Transient       │ → Detecta transientes usando filtro High-Pass
│ Enhancement     │   (pre-emphasis 0.95)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ WaveformAnalyzer│ → DetectBpmByTransientGrid()
│ .TransientGrid  │   1. Encuentra transientes en baja y alta frecuencia
│                 │   2. Combina y busca picos candidatos
│                 │   3. Prueba contra beat grid (GridFit)
│                 │   Output: gridBpm, gridConfidence
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ BPM Decision    │ → Selecciona BPM final comparando
│ Algorithm       │   soundTouchBpm vs gridBpm
│ (BpmDetector)   │   Aplica heurísticas de tresillo/half-time
└────────┬────────┘
         │
         ▼
   Final BPM + Alternative BPM
```

### 2.2 Módulos Principales

#### SoundTouch (BpmDetect)
- **Archivo:** SoundTouch.dll (biblioteca native)
- **Función:** `DetectWithSoundTouchFromSamples()` en `BpmDetector.cs:237-261`
- **Método:** Autocorrelación de ondas en mono samples
- **Ventaja:** Rápido, estable para audio comprimido

#### TransientGrid (WaveformAnalyzer)
- **Archivo:** `Services/WaveformAnalyzer.cs` línea 979+
- **Función:** `DetectBpmByTransientGrid()`
- **Método:** 
  1. Filtro high-pass (pre-emphasis 0.95)
  2. Detección de transientes en bandas separadas
  3. Grid fitting con scoring composite

#### BPM Decision Algorithm
- **Archivo:** `Services/BpmDetector.cs` líneas 50-204
- **Función:** `DetectBpmFromSamples()`
- **Método:** Evaluación de heurísticas para seleccionar tempo final

---

## 3. ALGORITMO IMPLEMENTADO (Versión Actual)

### 3.1 Selección de Segmento
```csharp
private float[] SelectAnalysisSegment(float[] monoSamples, int sampleRate, double initialBpm)
{
    double barDuration = 4.0 * (60.0 / initialBpm);
    double targetDuration = Math.Min(60.0, 32 * barDuration);
    // Usa RMS para saltarsi intros silenciosos
    // Selecciona ventana desde que la energía supera threshold
}
```

### 3.2 Filtro de Enhancement (Pre-emphasis)
```csharp
private float[] ApplyTransientEnhancementFilter(float[] samples)
{
    float[] filtered = new float[samples.Length];
    filtered[0] = samples[0];
    // Coeficiente 0.95 elimina frecuencias < ~150Hz
    for (int i = 1; i < samples.Length; i++)
    {
        filtered[i] = samples[i] - 0.95f * samples[i - 1];
    }
    return filtered;
}
```

### 3.3 Decisión de BPM (Algoritmo Principal)
```csharp
// Step 4: Select final BPM
double finalBpm;
if (gridBpm > 0 && gridConfidence > MIN_CONFIDENCE_THRESHOLD)
{
    if (soundTouchBpm > 0)
    {
        double ratio = gridBpm / soundTouchBpm;
        bool harmonic = IsHarmonicRatio(ratio);

        if (harmonic || Math.Abs(gridBpm - soundTouchBpm) < 5)
        {
            // Manejo de tresillos (ratio ~1.5 o ~0.667)
            if (Math.Abs(ratio - TRESILLO_RATIO) < TRESILLO_TOLERANCE || 
                Math.Abs(ratio - HALF_TIME_RATIO) < TRESILLO_TOLERANCE)
            {
                // CASO 0: INVERSIÓN - gridBpm alto, soundTouchBpm bajo
                // Solo confiar en gridBpm si NO coincide con tresillo de ST
                if (soundTouchBpm >= 70 && soundTouchBpm <= 130 && gridBpm > 140)
                {
                    double tresilloOfSoundTouch = soundTouchBpm * TRESILLO_RATIO;
                    // Si gridBpm coincide con tresillo de ST → ST está errado
                    if (Math.Abs(gridBpm - tresilloOfSoundTouch) < 5)
                    {
                        finalBpm = gridBpm;  // Usar Grid, ST detectón half-time
                    }
                    else if (Math.Abs(gridBpm - tresilloOfSoundTouch) < 3)
                    {
                        finalBpm = soundTouchBpm;  // ST es correcto
                    }
                }
                // CASO 2: ST > 130, Grid es half-time del real
                else if (soundTouchBpm > 130 && soundTouchBpm <= 160)
                {
                    double expectedGrid = soundTouchBpm / TRESILLO_RATIO;
                    if (Math.Abs(gridBpm - expectedGrid) < 5)
                        finalBpm = soundTouchBpm;
                }
                // CASO 3: Grid ≈ ST/2 → Grid es half-time simple
                else if (Math.Abs(gridBpm - soundTouchBpm / 2) < 3 && soundTouchBpm > 130)
                {
                    finalBpm = soundTouchBpm;
                }
            }
        }
    }
}
```

### 3.4 Normalización de Rango
```csharp
private double NormalizeTempoRange(double bpm, BpmRangeProfile profile)
{
    // Para perfiles específicos (Low, Mid, High, VeryHigh)
    // Busca el multiplicador que encaje en el rango
    double[] multipliers = { 2.0, 0.5, 1.5, 0.667, 3.0, 0.333, 1.25, 0.8 };
}
```

### 3.5 Cálculo de BPM Alternativo
```csharp
private double CalculateAlternativeBpm(double primaryBpm)
{
    if (primaryBpm < 90) altBpm = primaryBpm * 2.0;      // Double-time
    else if (primaryBpm <= 135) altBpm = primaryBpm * 1.5;  // Tresillo
    else altBpm = primaryBpm / 2.0;   // Half-time
}
```

---

## 4. CONSTANTES DE CONFIGURACIÓN

### BpmConstants.cs
```csharp
public static class BpmConstants
{
    // Ratios de detección
    public const double TRESILLO_RATIO = 1.5;
    public const double TRESILLO_TOLERANCE = 0.08;
    public const double HALF_TIME_RATIO = 0.667;

    // Umbrales de confianza
    public const double HIGH_CONFIDENCE_THRESHOLD = 0.85;
    public const double MEDIUM_CONFIDENCE_THRESHOLD = 0.65;
    public const double MIN_CONFIDENCE_THRESHOLD = 0.15;

    // Heurística Trap
    public const int TRAP_MIN_BPM = 98;
    public const int TRAP_MAX_BPM = 105;
    public const int TRAP_GRID_BPM_THRESHOLD = 160;
    public const double TRAP_CORRECTION_MULTIPLIER = 0.75;

    // Umbral inteligente
    public const int SMART_THRESHOLD_BPM = 155;
}
```

---

## 5. LOGS DE TESTS

### Test Completo (21 archivos)
```
=== BPM DETECTION TEST ===

Found 21 audio files to test.

--- Testing: audio 17 bpm 90.mp3 ---
Expected BPM: 90,0
Detected: 90,0 BPM | Alt: 135,0 BPM
Error: 0,0 BPM (0,0%)
[ OK ]

--- Testing: audio1 bpm 98,256 .mp3 ---
Expected BPM: 98,3
Detected: 99,5 BPM | Alt: 149,0 BPM
Error: 1,2 BPM (1,3%)
[ WARN ]

--- Testing: audio10 bpm 112 .mp3 ---
Expected BPM: 112,0
Detected: 112,0 BPM | Alt: 168,0 BPM
Error: 0,0 BPM (0,0%)
[ OK ]

--- Testing: audio11 bpm 82.mp3 ---
Expected BPM: 82,0
Detected: 164,5 BPM | Alt: 82,0 BPM
Error: 82,5 BPM (100,6%)
NOTE: Alternative BPM (82,0) is correct! Primary has error.
[ FAIL ]

--- Testing: audio12 bpm 98.mp3 ---
Expected BPM: 98,0
Detected: 98,0 BPM | Alt: 147,0 BPM
Error: 0,0 BPM (0,0%)
[ OK ]

--- Testing: audio13 bpm 102.mp3 ---
Expected BPM: 102,0
Detected: 102,0 BPM | Alt: 153,0 BPM
Error: 0,0 BPM (0,0%)
[ OK ]

--- Testing: audio14 bpm 128.mp3 ---
Expected BPM: 128,0
Detected: 129,0 BPM | Alt: 193,5 BPM
Error: 1,0 BPM (0,8%)
[ WARN ]

--- Testing: audio15 bpm 130.mp3 ---
Expected BPM: 130,0
Detected: 130,0 BPM | Alt: 195,0 BPM
Error: 0,0 BPM (0,0%)
[ OK ]

--- Testing: audio16 bpm 100.mp3 ---
Expected BPM: 100,0
Detected: 100,0 BPM | Alt: 150,0 BPM
Error: 0,0 BPM (0,0%)
[ OK ]

--- Testing: audio2 bpm 90.flac ---
Expected BPM: 90,0
Detected: 90,0 BPM | Alt: 135,0 BPM
Error: 0,0 BPM (0,0%)
[ OK ]

--- Testing: audio4 bpm 79.wav ---
Expected BPM: 79,0
Detected: 79,0 BPM | Alt: 158,0 BPM
Error: 0,0 BPM (0,0%)
[ OK ]

--- Testing: audio5 bpm 76,665.m4a ---
Expected BPM: 76,7
Detected: 57,5 BPM | Alt: 115,0 BPM
Error: 19,2 BPM (25,0%)
[ FAIL ]

--- Testing: audio6 bpm 74.ogg ---
Expected BPM: 74,0
Detected: 74,0 BPM | Alt: 148,0 BPM
Error: 0,0 BPM (0,0%)
[ OK ]

[SKIP] Cannot parse BPM from filename: audio7.wma,bak

--- Testing: audio8 bpm 90.aiff ---
Expected BPM: 90,0
Detected: 90,0 BPM | Alt: 135,0 BPM
Error: 0,0 BPM (0,0%)
[ OK ]

--- Testing: audio9 bpm 110.mp3 ---
Expected BPM: 110,0
Detected: 110,0 BPM | Alt: 165,0 BPM
Error: 0,0 BPM (0,0%)
[ OK ]

--- Testing: master bpm 152.mp3 ---
Expected BPM: 152,0
Detected: 101,4 BPM | Alt: 152,0 BPM
Error: 50,6 BPM (33,3%)
NOTE: Alternative BPM (152,0) is correct! Primary has error.
[ FAIL ]

--- Testing: master bpm 152.wav ---
Expected BPM: 152,0
Detected: 76,0 BPM | Alt: 152,0 BPM
Error: 76,0 BPM (50,0%)
NOTE: Alternative BPM (152,0) is correct! Primary has error.
[ FAIL ]

--- Testing: sin master bpm 152.mp3 ---
Expected BPM: 152,0
Detected: 76,0 BPM | Alt: 152,0 BPM
Error: 76,0 BPM (50,0%)
NOTE: Alternative BPM (152,0) is correct! Primary has error.
[ FAIL ]

--- Testing: sin master bpm 152.wav ---
Expected BPM: 152,0
Detected: 152,0 BPM | Alt: 76,0 BPM
Error: 0,0 BPM (0,0%)
[ OK ]

--- Testing: Ta Buena Rancha bpm 108.mp3 ---
Expected BPM: 108,0
Detected: 108,0 BPM | Alt: 162,0 BPM
Error: 0,0 BPM (0,0%)
[ OK ]

=== SUMMARY ===
Passed: 13 | Warnings: 2 | Failures: 5
Total: 21
```

### Logs de Debug (master bpm 152.mp3)
```
[2026-04-12 14:45:10.351] BpmDetector.DetectBpmFromSamples - 10231579 samples @ 48000Hz
[2026-04-12 14:45:11.465] BpmDetector - SoundTouch quick estimate: 101,4
[2026-04-12 14:45:11.485] BpmDetector.SelectSegment - Start: 13,0s, Duration: 60,0s (32 bars @ 101 BPM)
[2026-04-12 14:45:11.498] BpmDetector - Analysis segment: 2880000 samples (60,0s)
[2026-04-12 14:45:11.499] BpmDetector - Aplicando filtro High-Pass al segmento para el análisis de transientes...
[2026-04-12 14:45:11.615] WaveformAnalyzer.TransientGrid - Low-band transients: 96, Hi-band transients: 298
[2026-04-12 14:45:11.616] WaveformAnalyzer.TransientGrid - Merged transients: 380
[2026-04-12 14:45:11.628] WaveformAnalyzer.TransientGrid - Top candidates: 152,0(0,19), 195,5(0,14), 196,0(0,13), 171,0(0,13), 182,5(0,13)
[2026-04-12 14:45:11.628] WaveformAnalyzer.GridFit - BPM 152,0: stdDev=0,0248, hitRate=0,05, composite=-0,002
[2026-04-12 14:45:11.629] WaveformAnalyzer.GridFit - BPM 195,5: stdDev=0,0345, hitRate=0,30, composite=0,232
[2026-04-12 14:45:11.630] WaveformAnalyzer.GridFit - BPM 196,0: stdDev=0,0330, hitRate=0,36, composite=0,295
[2026-04-12 14:45:11.630] WaveformAnalyzer.GridFit - BPM 171,0: stdDev=0,0332, hitRate=0,29, composite=0,219
[2026-04-12 14:45:11.631] WaveformAnalyzer.GridFit - BPM 182,5: stdDev=0,0378, hitRate=0,23, composite=0,154
[2026-04-12 14:45:11.631] WaveformAnalyzer.TransientGrid - Best: 171,0 BPM (stdDev=0,0356, hitRate=0,41, composite=0,343)
[2026-04-12 14:45:11.631] BpmDetector - TransientGrid result: 171,0 BPM (conf: 0,41)
[2026-04-12 14:45:11.632] BpmDetector - Decision point: gridBpm=171,0, soundTouchBpm=101,4, ratio=1,686, gridConfidence=0,41
[2026-04-12 14:45:11.632] BpmDetector - Tresillo check: abs(ratio-1.5)=0,186, abs(ratio-0.667)=1,019
[2026-04-12 14:45:11.632] BpmDetector - Coincidencia armónica pero confianza de Grid muy baja (0,41). Rechazando Grid y usando SoundTouch: 101,4
[2026-04-12 14:45:11.633] BpmDetector - Heurística Trap Masterizado: Falso positivo 101,4 corregido a 76,1 BPM (Grid sugería 171,0)
[2026-04-12 14:45:11.633] BpmDetector.SnapToInteger - 76,1 -> 76
[2026-04-12 14:45:11.634] BpmDetector - Final BPM: 76
[2026-04-12 14:45:11.634] BpmDetector.SnapToInteger - 152,0 -> 152
[2026-04-12 14:45:11.635] BpmDetector - Resultado Final: 76 BPM (Alternativo: 152 BPM)
```

### Logs de Debug (sin master bpm 152.wav - CASO CORRECTO)
```
[2026-04-12 14:45:16.840] WaveformAnalyzer.TransientGrid - Top candidates: 152,0(0,23), 193,0(0,13), 182,5(0,12), 195,0(0,12), 196,0(0,12)
[2026-04-12 14:45:16.848] WaveformAnalyzer.GridFit - BPM 152,0: stdDev=0,0042, hitRate=1,00, composite=0,992
[2026-04-12 14:45:16.849] WaveformAnalyzer.TransientGrid - Best: 152,0 BPM (stdDev=0,0042, hitRate=1,00, composite=0,992)
[2026-04-12 14:45:16.850] BpmDetector - TransientGrid result: 152,0 BPM (conf: 1,00)
[2026-04-12 14:45:16.850] BpmDetector - Decision point: gridBpm=152,0, soundTouchBpm=101,4, ratio=1,499, gridConfidence=1,00
[2026-04-12 14:45:16.850] BpmDetector - Tresillo check: abs(ratio-1.5)=0,001, abs(ratio-0.667)=0,832
[2026-04-12 14:45:16.850] BpmDetector - ENTERED tresillo handling block
[2026-04-12 14:45:16.851] BpmDetector - Ratio de tresillo detectado. TransientGrid tiene confianza muy alta (1,00). Forzando TransientGrid: 152,0
[2026-04-12 14:45:16.851] BpmDetector.SnapToInteger - 152,0 -> 152
[2026-04-12 14:45:16.851] BpmDetector - Final BPM: 152
[2026-04-12 14:45:16.851] BpmDetector - Resultado Final: 152 BPM (Alternativo: 76 BPM)
```

---

## 6. ANÁLISIS DE FALLOS

### 6.1 Caso `master bpm 152.mp3` (FALLO)

| Etapa | Valor |
|-------|-------|
| SoundTouch | 101.4 BPM |
| Grid candidatos | 152, 195.5, 196, 171, 182.5 |
| Grid best | 171.0 (conf: 0.41) |
| Ratio | 171/101.4 = 1.686 |

**Problema:** 
- SoundTouch detecta 101.4 (posiblemente half-time de 152)
- Grid detecta 171.0 (posiblemente el tresillo de ~114, no de 152)
- Confianza del Grid (0.41) < umbral alto (0.85) → se rechaza Grid
- Se usa SoundTouch 101.4
- Heurística Trap fuerza 101.4 → 76.05 (por estar en rango 98-105)
- **Resultado:** 76 BPM (incorrecto)

### 6.2 Caso `sin master bpm 152.wav` (ÉXITO)

| Etapa | Valor |
|-------|-------|
| SoundTouch | 101.4 BPM |
| Grid best | 152.0 (conf: 1.00!) |
| Ratio | 152/101.4 = 1.499 ≈ 1.5 |

**Por qué funciona:**
- Grid confidence = 1.00 (perfecto)
- Ratio ≈ 1.5 → entra en manejo de tresillo
- gridConfidence >= 0.85 → se fuerza Grid
- **Resultado:** 152 BPM (correcto)

### 6.3 Caso `audio11 bpm 82.mp3` (FALLO)

| Etapa | Valor |
|-------|-------|
| Detected | 164.5 BPM |
| Expected | 82.0 BPM |
| Alternative | 82.0 ✓ |

**Problema:** El algoritmo prefiere el tempo alto (164.5) sobre el bajo (82).

---

## 7. ESTRATEGIA DE SOLUCIÓN PROPUESTA

### Análisis de Patrones

1. **SoundTouch puede detectar half-time** cuando el audio tiene subs dominantes
2. **Grid puede detectar tresillos** cuando hay patrones rítmicos claros
3. **La heurística de tresillo fuerza tempo alto** cuando maxBpm <= 155

### Solución Recomendada

Crear un **sistema de voto ponderado** donde:

1. **Si gridConfidence >= 0.85:** Usar gridBpm (confianza alta)
2. **Si SoundTouch está en rango válido (70-130) Y gridBpm ≈ soundTouchBpm * 1.5:**
   - Verificar cuál tiene mejor score de grid fit
   - Si gridBpm tiene composite score > 0.5, usar gridBpm
   - Si no, confiar en SoundTouch
3. **Si audio es > 3 minutos:** Considerar analizar más segmentos para validar

### Algoritmo Propuesto (Pseudo-código)

```
if (gridBpm > 0 && gridConfidence > MIN_CONFIDENCE)
{
    if (soundTouchBpm > 0)
    {
        ratio = gridBpm / soundTouchBpm
        
        // CASO 1: Tresillo claro (ratio ≈ 1.5)
        if (abs(ratio - 1.5) < 0.08)
        {
            if (gridConfidence >= 0.85)
                return gridBpm;
            
            // Verificar si soundTouch podría ser half-time
            expectedSTFromGrid = gridBpm / 1.5
            if (abs(soundTouchBpm - expectedSTFromGrid) < 3)
            {
                // SoundTouch parece ser half-time, grid es real
                return gridBpm;
            }
            else if (soundTouchBpm >= 70 && soundTouchBpm <= 130)
            {
                // SoundTouch parece real, grid es tresillo
                return soundTouchBpm;
            }
        }
        
        // CASO 2: Desacuerdo (ratio no armónico)
        else
        {
            if (gridConfidence >= 0.65)  // MEDIUM_CONFIDENCE
                return gridBpm;
            else
                return soundTouchBpm;
        }
    }
}
```

---

## 8. PRÓXIMOS PASOS

1. [ ] Implementar el algoritmo de voto ponderado
2. [ ] Añadir validación multi-segmento para audio > 3 min
3. [ ] Ajustar umbrales de confianza basados en más tests
4. [ ] Probar con el set completo de audios
5. [ ] Verificar que no se introduzcan regresiones en audios que ya funcionan

---

## 9. ARCHIVOS MODIFICADOS

| Archivo | Descripción |
|---------|-------------|
| `Services/BpmDetector.cs` | Algoritmo de decisión BPM |
| `Services/BpmConstants.cs` | Constantes de configuración |
| `BpmTest/Program.cs` | Programa de test |

---

*Reporte generado: 2026-04-12*  
*Auditoría realizada con asistencia de IA*