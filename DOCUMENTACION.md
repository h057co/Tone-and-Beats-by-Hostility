# Tone & Beats by Hostility - Documentación Técnica

**Versión:** 1.0.11  
**Fecha:** 13 de Abril de 2026  
**Framework:** .NET 8.0 + WPF  
**Estado:** Release (Donationware)
**Licencia:** CC BY-NC-ND 4.0

---

## 1. Descripción del Proyecto

**Tone & Beats by Hostility** es una aplicación de escritorio Windows para el análisis de audio que detecta automáticamente:
- **BPM** (Beats Per Minute) del archivo de audio
- **Tonalidad** (Key) en formato estándar musical (C, C#, D, etc.)
- **Información técnica** del archivo (formato, sample rate, bit depth, bitrate, canales)
- **LUFS** (Loudness): Integrated, LRA, True Peak

### Características Principales

- Interfaz borderless con WindowChrome (bordes redondeados, sombra)
- Title bar custom con botones de ventana (minimizar, maximizar, cerrar)
- Redimensionamiento libre con WindowChrome (bordes y esquinas)
- Sistema de temas visuales (Dark, Light, Blue, iOS Light, iOS Dark)
- Reproducción de audio con seek interactivo
- Guardado de metadatos BPM y Key en el archivo
- Drag & drop de archivos
- Ajuste manual de BPM detectado
- Visualización de tonalidad relativa (mayor/menor)
- Análisis paralelo de BPM + Key + Waveform
- Tamaño mínimo 350x900, maximizado respeta barra de tareas

---

## 2. Arquitectura del Proyecto

### Estructura de Archivos

```
src/
├── App.xaml / App.xaml.cs           # Punto de entrada WPF
├── MainWindow.xaml / .cs            # Ventana principal
├── AboutWindow.xaml / .cs           # Ventana Acerca De
├── AudioAnalyzer.csproj             # Proyecto .NET 8.0
│
├── Assets/                          # Recursos gráficos
│   ├── HOST_BLANCO.png             # Logo blanco (temas Dark/Blue)
│   ├── HOST_NEGRO.png              # Logo negro (tema Light)
│   └── HOSTBLANCO.ico               # Icono de aplicación
│
├── Services/                        # Lógica de negocio
│   ├── AudioPlayerService.cs        # Reproducción con NAudio
│   ├── BpmDetector.cs              # Detección BPM híbrida
│   ├── KeyDetector.cs              # Detección de tonalidad
│   ├── WaveformAnalyzer.cs         # Análisis de forma de onda
│   ├── MetadataWriter.cs           # Escritura de metadatos
│   └── LoggerService.cs            # Sistema de logging
│
├── ViewModels/                      # Patrón MVVM
│   └── MainViewModel.cs           # ViewModel principal
│
├── Models/                          # Modelos de datos
│   ├── AudioFileInfo.cs           # Información del archivo
│   └── WaveformData.cs           # Datos de forma de onda
│
├── Controls/                        # Controles personalizados
│   └── WaveformControl.xaml / .cs # Visualizador de forma de onda
│
├── Themes/                         # Sistema de temas
│   ├── DarkTheme.xaml            # Tema oscuro
│   ├── LightTheme.xaml           # Tema claro
│   ├── BlueTheme.xaml            # Tema azul
│   └── ThemeManager.cs           # Gestor de cambio de temas
│
├── Infrastructure/                 # Clases de soporte
│   ├── ViewModelBase.cs          # Base ViewModel
│   ├── MessageBoxService.cs      # Servicio de MessageBox
│   ├── FilePickerService.cs      # Selector de archivos
│   ├── BoolToVisibilityConverter.cs
│   └── CornerResizeBehavior.cs   # Redimensionado solo esquinas
│
├── Interfaces/                     # Contratos de servicios
│   ├── IAudioPlayerService.cs
│   ├── IBpmDetectorService.cs
│   ├── IKeyDetectorService.cs
│   ├── IWaveformAnalyzerService.cs
│   ├── IFilePickerService.cs
│   └── IMessageBoxService.cs
│
└── Commands/                      # Comandos MVVM
    └── RelayCommand.cs
```

---

## 3. Librerías y Dependencias

| Paquete | Versión | Propósito | Licencia |
|---------|---------|----------|----------|
| NAudio | 2.2.1 | Reproducción y análisis de audio | Ms-PL |
| TagLibSharp | 2.3.0 | Lectura/escritura de metadatos | LGPL 2.1 |
| BpmFinder | 0.1.0 | Detección de tempo (MP3/WAV) | MIT |
| MediaInfo.Wrapper.Core | 26.1.0 | Info técnica de audio | BSD-2-Clause |
| KeyDetector (custom) | - | Detección de tonalidad (implementación propia) | - |
| FFMpegCore | 5.1.0 | Análisis loudness LUFS (wrapper FFmpeg) | MIT |

---

## 4. Algoritmos de Análisis

### 4.1 Detección de BPM (Híbrido con Fallback)

El sistema utiliza un enfoque de **detección híbrida** con fallback para formatos no soportados:

1. **BpmFinder** (análisis primario - solo MP3/WAV)
   - Análisis de energía en bandas de frecuencia
   - Detección de beats mediante onset detection
   - Autocorrelación para periodicidad
   - Configuración: MinBpm=50, MaxBpm=220, PreferStableTempo=true

2. **Algoritmo avanzado propio** (fallback para FLAC/OGG/M4A/AAC/AIFF/WMA)
   - Detección de rango adaptativo basada en energía de graves
   - Pre-procesamiento: Low-frequency emphasis + Normalization
   - 3 onset functions: Spectral Flux, Energy Flux, Complex Domain
   - Multi-candidato BPM con weighted voting y verificación de consistencia

3. **Lógica de selección**
   - Si formato es MP3/WAV: usa BpmFinder primero, fallback a avanzado si falla
   - Si formato es FLAC/OGG/etc: usa algoritmo avanzado directamente
   - Nota: El análisis avanzado tiene mayor precisión pero es más lento

### 4.2 Detección de Tonalidad

**Biblioteca**: KeyDetector personalizado (implementación propia del algoritmo Krumhansl-Schmuckler)

- Pitch Class Profile (PCP): Calcula perfil cromático
- FFT de 16384 samples, A4=440Hz referencia
- 8 armónicos por clase de pitch
- Correlación con templates Major/Minor
- Retorna: Key (C-B) + Mode (Major/Minor) + Confidence

### 4.3 Análisis de Forma de Onda

1. Downsample a 1000 puntos
2. Calcula min/max por ventana
3. Renderiza usando WPF Path
4. Beat grid superpuesto basado en BPM (simplificado para rendimiento)

### 4.4 Análisis Paralelo

Los tres análisis (BPM, Key, Waveform) se ejecutan en paralelo usando `Task.WhenAll` para reducir el tiempo total de procesamiento.

---

## 5. Guía de Uso

### 5.1 Cargar un Archivo
- **Opción A**: Click en "Browse" y seleccionar archivo
- **Opción B**: Arrastrar archivo a la ventana (drag & drop)
- **Filtros**: MP3, WAV, OGG, FLAC, M4A, AAC, AIFF, WMA

### 5.2 Reproducir Audio
- **▶ Play**: Inicia reproducción
- **⏸ Pause**: Pausa la reproducción
- **⏹ Stop**: Detiene y reinicia

### 5.3 Análisis de Audio
1. Cargar archivo de audio
2. Click en "🔍 Analyze Audio"
3. Los tres análisis (BPM, Key, Waveform) se ejecutan en paralelo
4. Resultados aparecen en Row 6 (BPM y Key)
5. Opcional: Click en "💾 Save to Metadata" para guardar en el archivo

### 5.4 Ajuste de BPM (Row 6)
- **Click izquierdo**: Ajusta BPM
  - Si BPM > 135: divide ÷2
  - Si BPM ≤ 135: multiplica ×2
- **Click derecho**: Acción opuesta
  - Si BPM < 65: multiplica ×2
  - Si BPM ≥ 65: divide ÷2
- **Segundo click**: Reset al BPM original

### 5.5 Cambiar Tonalidad (Row 6)
- **Click en Key**: Alterna entre tonalidad detectada y su relativo
  - Major → Minor (3 semitonos abajo)
  - Minor → Major (3 semitonos arriba)

### 5.6 Navegar por el Audio
- Click en cualquier posición de la forma de onda para seek

### 5.7 Cambiar Tema
- Click en botón "🎨" en esquina superior derecha
- Cicla: Dark → Light → Blue → Dark

### 5.8 Redimensionar Ventana

- **Solo desde esquinas**: La ventana escala proporcionalmente en diagonal
- **Ancho variable**: MinWidth=350, MaxWidth=600
- **Altura variable**: MinHeight=500
- Proporción mantenida: ~1.9 (760/400)

---

## 6. Construcción y Distribución

### Compilación

```bash
cd src
dotnet publish -c Release -r win-x64 --self-contained true -o ../publish
```

### Instalador (Inno Setup)

El instalador se genera con Inno Setup 6:

```bash
iscc setup.iss
```

Ubicación del instalador:
- `installer/ToneAndBeatsByHostility_Setup_v1.0.0.exe`

### Distribución

| Método | Ubicación | Descripción |
|--------|-----------|-------------|
| Instalador | `installer/ToneAndBeatsByHostility_Setup_v1.0.0.exe` | ~140 MB (comprimido) |
| Portable | `publish/` | 511 archivos (incluye runtime) |

---

## 7. Copyright y Legal

### Información de Copyright

```
© 2026 Hostility Music. www.hostilitymusic.com. Todos los derechos reservados.
info@hostilitymusic.com
```

### Licencia de Uso

El software es propiedad de Hostility Music. Queda prohibida la reproducción, distribución, modificación o uso no autorizado sin consentimiento expreso por escrito.

Ver archivo `LICENSE.txt` para información completa.

### Ubicaciones del Copyright

1. Ventana "Acerca de" de la aplicación
2. Archivo LICENSE.txt
3. Instalador (propiedades)
4. AssemblyInfo.cs

---

## 8. Historial de Versiones

### v1.0.11 (13 de Abril 2026) - WINDOW CHROME

**Mejoras de ventana:**
- ✅ Ventana borderless con WindowChrome
- ✅ Title bar custom (24px) con botones custom
- ✅ CornerRadius 10 con sombra exterior
- ✅ ResizeBorderThickness 6 para resize nativo
- ✅ Maximizar respeta barra de tareas
- ✅ Free scaling (sin proporción fija)

### v1.0.10 (12 de Abril 2026) - BPM PIPELINE

**Mejoras de detección BPM:**
- ✅ 9 guards y fallbacks implementados
- ✅ Score 95% (19/20 MATCH)
- ✅ GRID NOISE GUARD para audio 74 BPM
- ✅ ST/2 Guard para audio 76.7 BPM
- ✅ Cross-validation en fallback

### v1.0.2 (8 de Abril 2026) - LUFS MODULE

**Nuevo módulo implementado:**
- ✅ Análisis de loudness usando FFmpeg loudnorm filter
- ✅ Detección de LUFS Integrated
- ✅ Detección de LRA (Loudness Range)
- ✅ Detección de True Peak (dBTP)
- ✅ Row 4 en UI para mostrar resultados de loudness
- ✅ Etiqueta "Short Term" cambiada a "LRA"
- ✅ Sistema de colores según nivel de loudness (verde/amarillo/rojo)

### v1.0.1 (7 de Abril 2026) - HOTFIX

**Bug corregido:**
- ✅ BpmFinder no soporta archivos FLAC/OGG/M4A/AAC/AIFF/WMA
- ✅ Implementado algoritmo avanzado como fallback para todos los formatos
- ✅ Ahora el BPM se detecta correctamente en archivos FLAC

### v1.0.0 (7 de Abril 2026) - RELEASE

**Features implementadas:**
- ✅ Detección BPM (BpmFinder)
- ✅ Detección de tonalidad con relativo mayor/menor
- ✅ Sistema de temas (Dark/Light/Blue)
- ✅ Interfaz redimensionable con Viewbox
- ✅ Row 2 dinámico según formato de audio
- ✅ Row 6 con click handlers para BPM y Key
- ✅ Sistema de logging
- ✅ MediaInfo para información técnica de audio
- ✅ Guardado de metadatos
- ✅ Drag & drop
- ✅ Análisis paralelo (BPM + Key + Waveform)
- ✅ Redimensionado solo desde esquinas (CornerResizeBehavior)
- ✅ Proporcionalidad de ventana (1.9 ratio)
- ✅ Ventana fija 400x760px
- ✅ Nuevo Row 4 para Status
- ✅ Copyright y licencia actualizados
- ✅ Installer con Inno Setup
- ✅ VERSION 1.0.0 OFICIAL

### v1.0.0-beta (6 de Abril 2026) - BETA

- Versión inicial beta

---

## 9. Especificaciones Técnicas

### Requisitos del Sistema

- **Windows**: 10/11 (x64)
- **Arquitectura**: x64
- **Espacio**: ~150 MB (portable), ~140 MB (instalado)

### Memoria y Rendimiento

- Tiempo de análisis típico: 3-15 segundos (dependiendo del archivo)
- Memoria: ~200-500 MB durante análisis
- Formatos soportados: MP3, WAV, OGG, FLAC, M4A, AAC, AIFF, WMA

### Configuración de Build

```xml
<TargetFramework>net8.0-windows</TargetFramework>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<SelfContained>true</SelfContained>
```

---

## 10. Estructura de Archivos de Release

```
O:\Test\BPM KEY\
├── publish/                    # Build portable (511 archivos)
│   └── ToneAndBeatsByHostility.exe
│
├── installer/                  # Instalador
│   ├── setup.iss              # Script Inno Setup
│   └── ToneAndBeatsByHostility_Setup_v1.0.0.exe
│
├── LICENSE.txt                 # Licencia completa
│
└── DOCUMENTACION.md            # Este documento
```

---

## 11. Próximos Pasos / Backlog

### Features para v1.1+ (Propuestas)

1. **Batch Processing** - Procesar múltiples archivos
2. **Botón Donation** - PayPal en ventana "Acerca de"
3. **Exportar reporte** - CSV/JSON de análisis
4. **Detección de secciones** - Intro/Verse/Chorus
5. **Mejoras de UI** - Waveform interactivo avanzado

### Known Issues

- Depuración de código pendientes (eventos, try/catch)
- Testing con archivos grandes no realizado

---

## 12. Notas de Desarrollo

### Changelog Reciente (9 Abril 2026)

- v1.0.3 Release
- iOS Themes (Light/Dark) añadidos
- KoFi donation button + QR image en About Window
- LRA terminology corregido (mostrando "LRA" en vez de "LUFS")
- Single-file publish con todos los assets embebidos
- License cambiada a CC BY-NC-ND 4.0 (Donationware)
- GitHub repository publicado
- Documentación completa actualizada

### Git Checkpoints

| Commit | Descripción |
|--------|-------------|
| 837aa6f | docs: update documentation with CC BY-NC-ND 4.0 license |
| a6c2d6c | docs: add RELEASE.md with v1.0.3 documentation |
| 3f5b292 | fix: sync AssemblyVersion with csproj for v1.0.3 |

---

*Documentación actualizada: 13 de Abril de 2026*
*Desarrollado por: Luis Jiménez (Hostility) - Medellín, Colombia*
*Contacto: info@hostilitymusic.com*
*Web: www.hostilitymusic.com*
*Licencia: CC BY-NC-ND 4.0*
*Repositorio: https://github.com/h057co/Tone-and-Beats-by-Hostility*