# CODE_AUDIT_REPORT.md

## Tone & Beats by Hostility - Auditoría Técnica de Código

**Fecha:** 12 de Abril de 2026  
**Auditor:** AI Assistant  
**Versión Analizada:** v1.0.6 + Refactoring Fases 1-3  

---

## 1. RESUMEN EJECUTIVO

| Métrica | Valor |
|---------|-------|
| **Total archivos .cs** | 39 |
| **Total líneas de código** | ~10,000+ |
| **Spaghetti Code** | Moderado |
| **Violaciones MVVM** | 5 encontradas |
| **DRY Violations** | 10+ encontradas |
| **Memory Leaks** | 4 encontrados |
| **Magic Numbers** | 100+ hardcoded |
| **Threading Issues** | 2 encontrados |

**Salud General:** ⚠️ **6/10** - El código funciona pero tiene deuda técnica significativa acumulada

---

## 2. HALLAZGOS CRÍTICOS (Spaghetti Code)

### 2.1 God Class: MainViewModel.cs

| Métrica | Valor |
|---------|-------|
| **Total líneas** | 972 |
| **Campos** | 51 |
| **Métodos** | ~40 |

**Métodos que exceden 100 líneas:**

| Método | Líneas | Complejidad |
|--------|--------|-------------|
| `ExecuteAnalyze()` | 134 | Alta |
| `LoadAudioFile()` | 128 | Alta |
| `AutocorrelateTransients()` (WaveformAnalyzer) | 200 | Muy Alta |

**Violaciones:**
- 51 campos mezclando: playback state, file management, análisis results, UI state, BPM modification state, metadata handling
- El pipeline `AudioAnalysisPipeline` existe (115 líneas) pero **NO se usa** - MainViewModel duplica su lógica

---

### 2.2 Nested Conditionals Profundos: BpmDetector.cs

**Líneas 85-200 - Select Final BPM:**

```
Nivel 1: if (gridBpm > 0 && gridConfidence > 0.15)
  Nivel 2: if (soundTouchBpm > 0)
    Nivel 3: if (harmonic || Math.Abs(...) < 5)
      Nivel 4: if (Math.Abs(ratio - 1.5) < 0.08 ...)
        Nivel 5: if (gridConfidence >= 0.85)
          → finalBpm = gridBpm
```

**Complejidad Ciclomática estimada:** 15-18 puntos de decisión en 87 líneas

---

### 2.3 Código Duplicado: WaveformAnalyzer.cs

| Duplicación | Ubicación | Estado |
|-------------|-----------|--------|
| Butterworth Filter | Lines 265-301, 672-734 | ✅ ARREGLADO (Fase 2) |
| Onset Detection Structure | Lines 299-394 | ❌ Pendiente |
| Frame Count Calculation | Lines 301, 331, 364 | ❌ Pendiente |
| RMS Energy Calculation | WaveformAnalyzer vs BpmDetector | ❌ Pendiente |

---

### 2.4 Violaciones MVVM

| Archivo | Líneas | Problema | Severidad |
|---------|--------|----------|-----------|
| `MainWindow.xaml.cs` | 48 | `ThemeManager.CycleTheme()` en code-behind | 🔴 Alta |
| `MainWindow.xaml.cs` | 52-71 | `UpdateLogoForTheme()` - lógica de negocio en code-behind | 🔴 Alta |
| `MainWindow.xaml.cs` | 73-88 | `Hyperlink_RequestNavigate()` - lógica de URL en code-behind | 🟠 Media |
| `AboutWindow.xaml.cs` | 29, 39 | `Process.Start()` para URL launching | 🟠 Media |
| `AboutWindow.xaml.cs` | 53-68 | `LoadEmbeddedImages()` - lógica de imagen en code-behind | 🟠 Media |

---

## 3. LISTA DE "QUÉ EVITAR"

### Code Smells Identificados en Este Proyecto

```
❌ NO hacer: Lógica de negocio en code-behind (.xaml.cs)
   → MainWindow.xaml.cs:ThemeManager.CycleTheme()
   → AboutWindow.xaml.cs:Process.Start()

❌ NO hacer: Campos públicos en ViewModels
   → MainViewModel tiene 51 campos privados - OK
   → Pero deberían separarse en clases de estado

❌ NO hacer: Métodos > 100 líneas
   → ExecuteAnalyze() 134 líneas → debería extraerse
   → AutocorrelateTransients() 200 líneas → debería extraerse

❌ NO hacer: Números mágicos hardcoded
   → 100+ encontrados en el proyecto
   → Ya parcialmente arreglado con BpmConstants.cs

❌ NO hacer: No desuscribir eventos
   → MainViewModel: PlaybackStateChanged no desuscrito
   → MainWindow: SeekRequested no desuscrito

❌ NO hacer: Parallel.Invoke blocking
   → WaveformAnalyzer.cs:236-240
   → Preferir Task.WhenAll para async

❌ NO hacer: Flag _isAnalyzingInProgress sin sincronización
   → MainViewModel: acceso no atómico desde múltiples threads
```

---

## 4. PLAN DE ACCIÓN Y CORRECCIONES

### FASE 1: Memory Leaks (Alta Prioridad)

| # | Problema | Archivo | Línea | Acción |
|---|----------|---------|-------|--------|
| 1 | `PlaybackStateChanged` no desuscrito | MainViewModel.cs | 85, 968-971 | Agregar ` -= OnPlaybackStateChanged` en Cleanup() |
| 2 | `SeekRequested` no desuscrito | MainWindow.xaml.cs | 26, 162-169 | Agregar en OnClosed() |

**Código a agregar en MainViewModel.cs:Cleanup():**
```csharp
public void Cleanup()
{
    _audioPlayerService.PlaybackStateChanged -= OnPlaybackStateChanged; // AGREGAR
    _audioPlayerService.Dispose();
}
```

**Código a agregar en MainWindow.xaml.cs:OnClosed():**
```csharp
WaveformDisplay.SeekRequested -= WaveformDisplay_SeekRequested; // AGREGAR
```

---

### FASE 2: Constantes DSP (Alta Prioridad)

| # | Problema | Archivo | Acción |
|---|----------|---------|--------|
| 1 | 100+ magic numbers | BpmDetector.cs, WaveformAnalyzer.cs | Crear `DspConstants.cs` |
| 2 | FFT window sizes hardcoded | KeyDetector.cs, WaveformAnalyzer.cs | Extraer a constantes |
| 3 | Chroma profiles hardcoded | KeyDetector.cs:7-8 | Crear `KeyProfiles.cs` |

**Estructura propuesta para `DspConstants.cs`:**
```csharp
public static class DspConstants
{
    // FFT
    public const int FFT_SIZE = 2048;
    public const int FFT_SIZE_KEY_DETECTION = 16384;
    public const int HOP_SIZE = 512;
    
    // Frequencies
    public const double LOW_FREQ_CUTOFF = 200.0;
    public const double TRANSIENT_LOW_BAND = 150.0;
    public const double TRANSIENT_HIGH_BAND_MIN = 2000.0;
    public const double TRANSIENT_HIGH_BAND_MAX = 8000.0;
    
    // Timing (seconds)
    public const double TRANSIENT_DEAD_TIME = 0.030;
    public const double TRANSIENT_WINDOW = 0.200;
    public const double DUPLICATE_THRESHOLD = 0.015;
    
    // Energy
    public const double ENERGY_THRESHOLD_LOW = 0.5;
    public const double ENERGY_THRESHOLD_HIGH = 0.2;
}
```

---

### FASE 3: Extraer AnálisisOrchestrator (Media Prioridad)

**YA EXISTE:** `AudioAnalysisPipeline.cs` (115 líneas) - ¡no reinventar!

| Pasos | Acción |
|-------|--------|
| 1 | Conectar `IAudioAnalysisPipeline` a `MainViewModel` |
| 2 | Reemplazar lógica duplicada en `ExecuteAnalyze()` (líneas 661-764) |
| 3 | Añadir `AlternativeBpm` al `AudioAnalysisReport` |
| 4 | Añadir parámetro `BpmRangeProfile` al pipeline |

**Cambio estimado:** ~30 líneas modificadas en MainViewModel

---

### FASE 4: Reorganizar Code-Behind (Media Prioridad)

| Archivo | Problema | Solución |
|---------|----------|----------|
| MainWindow.xaml.cs:48 | Theme cycling en code-behind | Crear `CycleThemeCommand` en ViewModel |
| MainWindow.xaml.cs:52-71 | UpdateLogoForTheme() | Bindear `LogoSource` como propiedad del ViewModel |
| AboutWindow.xaml.cs:29,39 | Process.Start() | Crear `OpenUrlCommand` en ViewModel |

---

### FASE 5: Thread Safety (Baja Prioridad)

| Problema | Archivo | Solución |
|----------|---------|----------|
| `_isAnalyzingInProgress` no thread-safe | MainViewModel.cs:41 | Usar `Interlocked.Exchange()` |
| `Parallel.Invoke` blocking | WaveformAnalyzer.cs:236 | Reemplazar con `Task.WhenAll` |

---

## 5. COMPARATIVA DE COMPLEJIDAD

### Métodos Más Complejos

| Archivo | Método | Líneas | CC | Prioridad |
|---------|--------|--------|----|----------|
| WaveformAnalyzer.cs | AutocorrelateTransients | 200 | Alta | 🔴 Arreglar |
| BpmDetector.cs | DetectBpmFromSamples (Step 4) | 87 | 15-18 | 🔴 Arreglar |
| MainViewModel.cs | ExecuteAnalyze | 134 | Media | 🟠 Considerar |
| MainViewModel.cs | LoadAudioFile | 128 | Media | 🟠 Considerar |
| WaveformAnalyzer.cs | DetectBpmByTransientGrid | 64 | Media | 🟡 Monitorear |
| WaveformAnalyzer.cs | ScoreBeatGrid | 63 | Media | 🟡 Monitorear |

### Archivos Más Grandes

| Archivo | Líneas | Clases | Responsabilidad |
|---------|--------|--------|----------------|
| WaveformAnalyzer.cs | 1043 | 1 | DSP/Audio Analysis |
| MainViewModel.cs | 972 | 1 | Coordinator/State |
| BpmDetector.cs | 402 | 1 | BPM Detection |
| AudioAnalysisPipeline.cs | 115 | 1 | Orchestration (unused!) |

---

## 6. RESUMEN DE HALLASGOS

### Por Categoría

| Categoría | Count | Alta Prioridad |
|----------|-------|----------------|
| Memory Leaks | 4 | 1 |
| MVVM Violations | 5 | 2 |
| DRY Violations | 10+ | 3 |
| Magic Numbers | 100+ | 10+ |
| Threading Issues | 2 | 0 |
| God Methods | 3 | 1 |

### Estado Actual Post-Refactoring

| Fase | Estado | Fecha |
|------|--------|-------|
| Fase 1: BpmConstants | ✅ Completada | 2026-04-12 |
| Fase 2: Butterworth Filter | ✅ Completada | 2026-04-12 |
| Fase 3: Empty Catch Blocks | ✅ Completada | 2026-04-12 |
| Fase 4: Memory Leaks | ✅ Completada | 2026-04-12 |
| Fase 5: DSP Constants | ✅ Completada | 2026-04-12 |
| Fase 6: Pipeline Connection | ✅ Completada | 2026-04-12 |
| Fase 7: Code-Behind MVVM | ✅ Completada (CycleThemeCommand, OpenUrlCommand) | 2026-04-12 |
| Fase 8: Threading Fixes | ❌ Pendiente | - |

### Esfuerzo de Corrección Estimado (Restante)

| Fase | Cambio | Tiempo |
|------|--------|--------|
| Fase 4: Memory Leaks | 2 archivos | 15 min |
| Fase 5: DSP Constants | 1 archivo nuevo | 1 hora |
| Fase 6: Pipeline Connection | MainViewModel | 30 min |
| Fase 7: Code-Behind | 2-3 archivos | 1-2 horas |
| Fase 8: Threading | 2 archivos | 30 min |

**Total estimado restante:** 4-5 horas de refactoring

---

## 7. RECOMENDACIONES INMEDIATAS

1. **Fase 6 (Pipeline)** - Conectar AudioAnalysisPipeline existente (Pendiente)
2. **Fase 8 (Threading)** - Fix Interlocked para _isAnalyzingInProgress flag
3. **Nota:** Fases 1-5 y 7 completadas ✅

---

## 8. FUENTES DEL ANÁLISIS

### Archivos Analizados

```
src/
├── ViewModels/
│   └── MainViewModel.cs (972 líneas)
├── Services/
│   ├── AudioAnalysisPipeline.cs (115 líneas)
│   ├── BpmDetector.cs (402 líneas)
│   ├── WaveformAnalyzer.cs (1043 líneas)
│   ├── KeyDetector.cs
│   ├── LoudnessAnalyzer.cs
│   ├── AudioDataProvider.cs
│   ├── AudioPlayerService.cs
│   └── BpmConstants.cs (19 líneas) - NUEVO
├── Controls/
│   └── WaveformControl.xaml.cs (353 líneas)
├── Infrastructure/
│   ├── CornerResizeBehavior.cs (163 líneas)
│   └── ...
└── MainWindow.xaml.cs (170 líneas)
```

---

*Reporte generado: 2026-04-12*  
*Auditoría realizada con asistencia de IA*  
*Para preguntas o clarificaciones, consultar JOURNAL.md*
