# 🧪 Test de Validación de Optimizaciones - Tone & Beats v1.0.4

**Fecha:** 9 de Abril de 2026  
**Objetivo:** Verificar que las optimizaciones FASE 1, 2 y 3 funcionan correctamente

---

## Test Files Available

```
O:\Tone and Beats\Assets\audiotest\
├── audio1.mp3         (5.4 MB)
├── audio2.flac        (31.8 MB)
├── audio3.wav         (210.9 MB)  ← Large file for RAM stress test
├── audio4.wav         (44.3 MB)
└── audio6.ogg         (3.1 MB)
```

---

## Checklist de Validación

### FASE 1: I/O Centralización

- [ ] **Aplicación inicia sin errores**
  - Verificar que `App.xaml.cs` carga `AudioAnalysisPipeline` correctamente
  - Console/Debug output: Sin exceptions

- [ ] **Cargar archivo pequeño (audio1.mp3, 5.4MB)**
  - Click: Browse → Seleccionar audio1.mp3
  - Click: Analyze
  - **Expectativa:** Análisis completa en ~5-10 segundos
  - **Verificar logs:** `AudioDataProvider.LoadMono` se llama UNA sola vez
  - **Verificar:** BPM, Key, Loudness se detectan correctamente

- [ ] **Cargar archivo mediano (audio4.wav, 44.3MB)**
  - Click: Analyze
  - **Expectativa:** ~15-25 segundos (vs ~30-40 antes)
  - **Verificar logs:** Una sola lectura de archivo

- [ ] **Cargar archivo grande (audio3.wav, 210.9MB)**
  - **Expectativa:** RAM usage estable (~500MB vs ~2GB antes)
  - **Verificar logs:** Sin picos de memoria
  - **Verificar:** Análisis completa sin Out Of Memory

### FASE 2: Arquitectura - Pipeline

- [ ] **Verificar que IAudioAnalysisPipeline se invoca**
  - Logs deben mostrar: `AudioAnalysisPipeline - Starting analysis`
  - Logs deben mostrar: `AudioAnalysisPipeline - Analysis complete`

- [ ] **Verificar retrocompatibilidad**
  - Los métodos antiguos aún funcionan si son llamados directamente
  - Ejemplo: `_bpmDetectorService.DetectBpmAsync(filePath)` debería funcionar

### FASE 3: Pre-asignación de Arrays

- [ ] **Sin GC pauses notables**
  - Análisis debe ser suave sin stuttering
  - Progress bar debe ser fluido

---

## Archivos a Revisar en Logs

Después de ejecutar análisis, revisar: `%LOCALAPPDATA%\ToneAndBeats\app.log`

**Buscar evidencia de optimizaciones:**

```
✅ FASE 1 Success:
"AudioDataProvider.LoadMono - Loaded 12345678 mono samples @ 44100Hz from: C:\path\to\file.mp3"
(Aparece UNA sola vez por análisis)

✅ FASE 2 Success:
"AudioAnalysisPipeline - Starting analysis: ..."
"AudioAnalysisPipeline - Loading audio data..."
"AudioAnalysisPipeline - Analysis complete: BPM=120.0, Key=C/Major, Valid=True"

✅ FASE 3 Success:
"WaveformAnalyzer.AnalyzeFromSamples - 12345678 samples @ 44100Hz"
(Sin errores de capacity)
```

---

## Comandos para Validación Rápida

### Ver logs en tiempo real:
```powershell
Get-Content "$env:LOCALAPPDATA\ToneAndBeats\app.log" -Wait -Tail 50
```

### Monitorear RAM mientras analiza:
```powershell
while($true) { 
  Get-Process ToneAndBeatsByHostility | Select-Object @{N='RAM(MB)';E={[math]::Round($_.WorkingSet/1MB)}} 
  Start-Sleep 1 
}
```

### Verificar que la app está corriendo:
```powershell
Get-Process ToneAndBeatsByHostility -ErrorAction SilentlyContinue | Select-Object ProcessName, Id, @{N='RAM(MB)';E={[math]::Round($_.WorkingSet/1MB)}}
```

---

## Criterios de Éxito

| Criterio | Antes | Después | ✅ |
|----------|-------|---------|-----|
| Archivo 44MB | ~40s | ~20-25s | Si el tiempo es < 30s |
| RAM pico (200MB audio) | ~2GB | ~500MB | Si RAM < 800MB |
| AudioDataProvider calls | 4x | 1x | Si logs muestran 1x |
| GC pressure | Alta | Baja | Si no hay stuttering |

---

## Notas Importantes

1. **Primera ejecución:** Puede ser más lenta (JIT compilation en Release)
2. **FFmpeg:** LoudnessAnalyzer sigue usando FFmpeg (externo) - esto NO cambió
3. **Backward compatibility:** Métodos antiguos siguen funcionando
4. **Logs:** Los logs son detallados para auditoría - ver `app.log` para evidencia completa

---

## Test Case Example - Manual Testing

### Scenario 1: Small File Analysis
```
1. Start app
2. Browse → audio1.mp3 (5.4MB)
3. Click Analyze
4. Expected: BPM ≈ 120, Key ≈ E/Major, Time ≈ 5-10s
5. Verify logs show "AudioDataProvider.LoadMono" once
```

### Scenario 2: Large File RAM Stability
```
1. Start fresh app
2. Browse → audio3.wav (210.9MB)
3. Monitor RAM with PowerShell script
4. Expected: Peak RAM ≈ 500-600MB (not 2000MB+)
5. Click Analyze
6. Verify no Out Of Memory exceptions
7. Check logs for analysis completion
```

### Scenario 3: Queue Processing
```
1. Start analysis on audio1.mp3
2. While analyzing, load audio4.wav
3. Expected: File goes to queue
4. After first analysis completes, audio4.wav auto-analyzes
5. Verify both results correct
```

---

## Success Signatures in Logs

**Log entry that proves FASE 1 worked:**
```
[2026-04-09 15:30:45.123] AudioDataProvider.LoadMono - Loaded 44100000 mono samples @ 44100Hz from: C:\audio.wav
[2026-04-09 15:30:46.456] BpmDetector.DetectBpmFromSamples - 44100000 samples @ 44100Hz
[2026-04-09 15:30:46.789] KeyDetector.DetectKeyFromSamples - Trimmed 44100000 -> 1323000 samples (center segment)
[2026-04-09 15:30:47.012] WaveformAnalyzer.AnalyzeFromSamples - 44100000 samples @ 44100Hz
```

(Note: `AudioDataProvider.LoadMono` appears ONCE, rest use SAME samples)

---

## Build Info

- **Framework:** .NET 8.0
- **Target:** net8.0-windows
- **Configuration:** Release
- **Output:** O:\Tone and Beats\src\bin\Release\net8.0-windows\ToneAndBeatsByHostility.exe

---

**Status:** ✅ Ready for QA Testing
