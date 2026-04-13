# Instrucciones de Ejecución - Test de Detección de BPM

## Reproducir los Resultados

### Requisitos
- .NET 8.0 SDK
- Git Bash o PowerShell 7+
- Directorio de trabajo: `O:\Tone and Beats\`

### Paso 1: Compilación del Proyecto

```bash
cd O:\Tone and Beats\BpmTest
dotnet build --no-restore -c Release
```

**Salida esperada:**
```
BpmTest -> O:\Tone and Beats\BpmTest\bin\Release\net8.0-windows\BpmTest.dll
    5 Advertencia(s)
    0 Errores
```

### Paso 2: Limpieza de Logs Anteriores

```bash
# Los logs se guardan en:
# %LOCALAPPDATA%\ToneAndBeats\app.log

# Opcional: limpiar log anterior (PowerShell)
Remove-Item "$env:LOCALAPPDATA\ToneAndBeats\app.log" -Force -ErrorAction SilentlyContinue
```

### Paso 3: Ejecución del Test

```bash
cd O:\Tone and Beats\BpmTest
dotnet run --no-build -c Release 2>&1
```

**Tiempo estimado:** 3-4 minutos (20 archivos)

**Salida esperada:**
- Tabla con 20 filas (1 por archivo)
- Columnas: Archivo | Esperado | Primary | Alt | Status
- Resumen estadístico final con tasa de éxito

### Paso 4: Revisión de Logs Detallados

```bash
# Ver últimas 100 líneas de logs
Get-Content "$env:LOCALAPPDATA\ToneAndBeats\app.log" | Select-Object -Last 100

# O guardar en archivo
Get-Content "$env:LOCALAPPDATA\ToneAndBeats\app.log" | Out-File "logs_analisis.txt"
```

---

## Interpretar Resultados

### Status Codes

```
MATCH      = Primary BPM coincide con esperado (±1 BPM)
ALT_MATCH  = Primary falla, pero Alt BPM coincide (±1 BPM)
FAIL       = Ni Primary ni Alt coinciden
ERROR      = Excepción durante detección
```

### Logs - Secciones Importantes

**Identidad del Detector:**
```
[TransientGrid] XXX.X BPM (conf: X.XX)
[SpectralFlux] XXX.X BPM (conf: X.XX)
```

**Votación:**
```
[Vote] CONSENSO ST+Grid → XXX.X BPM
[Vote] GUARDIA DE PULSO: Relación X.XX detectada. Gana SoundTouch
[Vote] SF gana por confianza (X.XX) → XXX.X BPM
```

**Decisión Final:**
```
[Decision] Final antes de post-processing: XXX.X BPM (ST:XXX, Grid:XXX [X.XX], SF:XXX [X.XX])
Final BPM: XXX
Alternativo: XXX
```

---

## Modificar Configuración y Re-test

### Para cambiar Thresholds de Transientes

**Archivo:** `src/Services/DspConstants.cs`

```csharp
public const double TRANSIENT_THRESHOLD_LOW = 1.0;    // Aumentar para menos falsos positivos
public const double TRANSIENT_THRESHOLD_HI = 0.4;     // Aumentar para menos ruido
```

Luego:
```bash
dotnet build --no-restore -c Release
dotnet run --no-build -c Release 2>&1
```

### Para cambiar Rango Trap

**Archivo:** `src/Services/BpmConstants.cs`

```csharp
public const int TRAP_MIN_BPM = 95;
public const int TRAP_MAX_BPM = 110;
public const int TRAP_GRID_BPM_THRESHOLD = 150;
public const double TRAP_CORRECTION_MULTIPLIER = 1.5;
```

### Para cambiar Lógica de Votación

**Archivo:** `src/Services/BpmDetector.cs` - Método `VoteThreeSources()`

Líneas ~467-541

---

## Archivos de Test

**Ubicación:** `O:\Tone and Beats\Assets\audiotest\`

**Lista (20 archivos):**

| Nro | Archivo | BPM | Formato | Propósito |
|-----|---------|-----|---------|-----------|
| 1 | audio 17 bpm 90.mp3 | 90 | MP3 | Hip-hop bajo |
| 2 | audio1 bpm 98,256.mp3 | 98 | MP3 | Reggaeton con decimal |
| 3 | audio10 bpm 112.mp3 | 112 | MP3 | Dance medio |
| 4 | audio11 bpm 82.mp3 | 82 | MP3 | Ballad |
| 5 | audio12 bpm 98.mp3 | 98 | MP3 | Reggaeton |
| 6 | audio13 bpm 102.mp3 | 102 | MP3 | Pop |
| 7 | audio14 bpm 128.mp3 | 128 | MP3 | EDM |
| 8 | audio15 bpm 130.mp3 | 130 | MP3 | EDM alto |
| 9 | audio16 bpm 100.mp3 | 100 | MP3 | Pop |
| 10 | audio2 bpm 90.flac | 90 | FLAC | Hip-hop (sin compresión) |
| 11 | audio4 bpm 79.wav | 79 | WAV | Slow (no comprimido) |
| 12 | audio5 bpm 76,665.m4a | 76.7 | M4A | Slow con decimal |
| 13 | audio6 bpm 74.ogg | 74 | OGG | Slow (OGG) |
| 14 | audio8 bpm 90.aiff | 90 | AIFF | Hip-hop (AIFF) |
| 15 | audio9 bpm 110.mp3 | 110 | MP3 | Dance |
| 16 | master bpm 152.mp3 | 152 | MP3 | Trap masterizado (MP3) |
| 17 | master bpm 152.wav | 152 | WAV | Trap masterizado (WAV) |
| 18 | sin master bpm 152.mp3 | 152 | MP3 | Trap sin master (MP3) |
| 19 | sin master bpm 152.wav | 152 | WAV | Trap sin master (WAV) |
| 20 | Ta Buena Rancha bpm 108.mp3 | 108 | MP3 | Reggaeton (sample real) |

---

## Estructura de Directorios

```
O:\Tone and Beats\
├── src\
│   ├── Services\
│   │   ├── BpmDetector.cs
│   │   ├── BpmConstants.cs
│   │   ├── DspConstants.cs
│   │   ├── WaveformAnalyzer.cs
│   │   ├── LoggerService.cs
│   │   └── ...
│   ├── AudioAnalyzer.csproj
│   └── ...
├── BpmTest\
│   ├── Program.cs                    ← Test principal
│   ├── BpmTest.csproj
│   └── bin\Release\net8.0-windows\
│       └── BpmTest.dll
├── Assets\audiotest\
│   ├── audio 17 bpm 90.mp3
│   ├── audio1 bpm 98,256 .mp3
│   ├── ... (18 más)
│   └── Ta Buena Rancha bpm 108.mp3
└── REPORTE_TECNICO_BPM_DETECTION.md  ← Este reporte
```

---

## Salida Esperada - Ejemplo

```
╔════════════════════════════════════════════════════════════════════════════════╗
║          BPM DETECTION TEST - FASE 1 BASELINE (20 archivos de test)           ║
╚════════════════════════════════════════════════════════════════════════════════╝

Total de archivos de test: 20

[ 1/20] audio 17 bpm 90.mp3 (esperado: 90 BPM)... ✓ Primary: 90,0 | Alt: 135,0 | [MATCH]
[ 2/20] audio1 bpm 98,256 .mp3 (esperado: 98 BPM)... ✓ Primary: 99,0 | Alt: 148,5 | [MATCH]
...
[20/20] Ta Buena Rancha bpm 108.mp3 (esperado: 108 BPM)... ✓ Primary: 108,0 | Alt: 162,0 | [MATCH]

╔════════════════════════════════════════════════════════════════════════════════╗
║                        TABLA DE RESULTADOS                                    ║
╠════════════════════════════════════════════════════════════════════════════════╣
║ Archivo                              │ Esperado │ Primary │ Alt      │ Status  ║
...
╚════════════════════════════════════════════════════════════════════════════════╝

╔════════════════════════════════════════════════════════════════════════════════╗
║                           RESUMEN ESTADÍSTICO                                 ║
╠════════════════════════════════════════════════════════════════════════════════╣
║ Total de tests:                   20                                        ║
║ MATCH (Primary correcto):          9  ( 45,0%)                          ║
║ ALT_MATCH (Alternativo correcto):    4  ( 20,0%)                          ║
║ FAIL (Ninguno coincide):           7  ( 35,0%)                          ║
║ ERROR (Excepción):                 0  (  0,0%)                          ║
╠════════════════════════════════════════════════════════════════════════════════╣
║ ✓ TASA DE ÉXITO TOTAL:           65,0%                                      ║
╚════════════════════════════════════════════════════════════════════════════════╝
```

---

## Troubleshooting

### Error: "El nombre 'DspConstants' no existe en el contexto"

**Solución:**
```bash
cd O:\Tone and Beats\BpmTest
rm -r bin obj
dotnet clean
dotnet build --no-restore -c Release
```

### Error: "File not found" en logs

**Causa:** LoggerService intenta crear carpeta en `%LOCALAPPDATA%\ToneAndBeats`

**Solución:**
```bash
# PowerShell
New-Item -Path "$env:LOCALAPPDATA\ToneAndBeats" -ItemType Directory -Force
```

### Resultado diferente en cada ejecución

**Nota:** Los resultados deben ser consistentes (±0.1 BPM variación).  
Si varían significativamente:
- Verificar que no hay procesos usando los archivos
- Reintentar con logs limpios
- Verificar configuración de DspConstants

---

## Documentación Relacionada

- `REPORTE_TECNICO_BPM_DETECTION.md` - Análisis completo
- `RESULTADOS_TEST_BPM.csv` - Tabla de resultados en formato CSV
- `docs/ARCHITECTURE.md` - Arquitectura del sistema

---

**Versión:** 1.0  
**Última actualización:** 12 de Abril de 2026
