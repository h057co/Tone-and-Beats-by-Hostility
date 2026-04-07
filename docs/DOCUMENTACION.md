# Tone & Beats by Hostility - Documentación Técnica

**Versión:** 1.0.0-beta  
**Fecha:** 6 de Abril de 2026  
**Framework:** .NET 8.0 + WPF

---

## 1. Descripción del Proyecto

**Tone & Beats by Hostility** es una aplicación de escritorio Windows para el análisis de audio que detecta automáticamente:
- **BPM** (Beats Per Minute) del archivo de audio
- **Tonalidad** (Key) en formato estándar musical (C, C#, D, etc.)
- **Información técnica** del archivo (formato, sample rate, bit depth, bitrate, canales)

### Características Principales

- Interfaz redimensionable con escala proporcional (Viewbox)
- Sistema de temas visuales (Dark, Light, Blue)
- Reproducción de audio con seek interactivo
- Guardado de metadatos BPM y Key en el archivo
- Drag & drop de archivos
- Ajuste manual de BPM detectado
- Visualización de tonalidad relativa (mayor/menor)

---

## 2. Arquitectura del Proyecto

### Estructura de Archivos

```
AudioAnalyzer/
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
│   ├── WaveformData.cs           # Datos de forma de onda
│   └── AnalysisResult.cs          # Resultado de análisis
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
│   └── BoolToVisibilityConverter.cs
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
| BpmFinder | 0.1.0 | Detección de tempo | MIT |
| MediaInfo.Wrapper.Core | 26.1.0 | Info técnica de audio | BSD-2-Clause |
| libKeyFinder.NET | 1.0.0 | Detección de tonalidad | GPL 2.0 |
| LiveChartsCore.SkiaSharpView.WPF | 2.0.0-rc2 | Visualización de gráficos | MIT |

---

## 4. Algoritmos de Análisis

### 4.1 Detección de BPM (Híbrido)

El sistema utiliza un enfoque de **detección híbrida** combinando:

1. **BpmFinder** (análisis primario)
   - Análisis de energía en bandas de frecuencia
   - Detección de beats mediante onset detection
   - Autocorrelación para periodicidad
   - Configuración: MinBpm=50, MaxBpm=220, PreferStableTempo=true

2. **Análisis avanzado propio** (validación secundaria)
   - Pre-procesamiento: Low-frequency emphasis + Normalization
   - 3 onset functions: Spectral Flux, Energy Flux, Complex Domain
   - Multi-candidato BPM con weighted voting
   - Harmonic checking (half-time, double-time detection)
   - Beat period consistency check
   - Rango adaptativo basado en energía de graves

3. **Combinación**
   - Si ambos resultados < 3 BPM de diferencia: promedio
   - Si harmonicDiff < 3 y confianza > 0.6: usar análisis avanzado
   - Sino: weighted average (60% BpmFinder, 40% avanzado)

### 4.2 Detección de Tonalidad

**Biblioteca**: libKeyFinder.NET (algoritmo Krumhansl-Schmuckler)

- Pitch Class Profile (PCP): Calcula perfil cromático
- FFT de 16384 samples, A4=440Hz referencia
- 8 armónicos por clase de pitch
- Correlación con templates Major/Minor
- Retorna: Key (C-B) + Mode (Major/Minor) + Confidence

### 4.3 Análisis de Forma de Onda

1. Downsample a 1000 puntos
2. Calcula min/max por ventana
3. Renderiza usando WPF Path
4. Beat grid superpuesto basado en BPM

### 4.4 Información Técnica de Audio

**Biblioteca**: MediaInfo.Wrapper.Core

Proporciona:
- File Type
- Sample Rate
- Bit Depth
- Channels
- Bitrate
- Bitrate Mode (CBR/VBR)

---

## 5. Guía de Uso

### 5.1 Cargar un Archivo
- **Opción A**: Click en "Browse" y seleccionar archivo
- **Opción B**: Arrastrar archivo a la ventana (drag & drop)

### 5.2 Reproducir Audio
- **▶ Play**: Inicia reproducción
- **⏸ Pause**: Pausa la reproducción
- **⏹ Stop**: Detiene y reinicia

### 5.3 Análisis de Audio
1. Cargar archivo de audio
2. Click en "🔍 Analyze Audio"
3. Resultados aparecen en Row 6 (BPM y Key)
4. Opcional: Click en "💾 Save to Metadata" para guardar

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

---

## 6. Construcción y Distribución

### Compilación

```bash
dotnet build -c Release
```

### Publicación Framework-Dependent

```bash
dotnet publish -c Release -r win-x64 --self-contained false -o ./bin/Beta/framework-dependent
```

**Requiere**: .NET 8 Runtime instalado

### Publicación Single-File

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./bin/Beta/single-file
```

**Incluye**: Runtime .NET 8 (no requiere instalación)

### Ubicaciones de Builds

| Tipo | Ubicación |
|------|-----------|
| Framework-Dependent | `bin/Beta/framework-dependent/` |
| Single-File | `bin/Beta/single-file/` |

---

## 7. Historial de Versiones

### v1.0.0-beta (6 de Abril 2026)

**Features implementadas:**
- Detección BPM híbrida (BpmFinder + análisis avanzado propio)
- Detección de tonalidad con relativo mayor/menor
- Sistema de temas (Dark/Light/Blue)
- Interfaz redimensionable con Viewbox
- Row 2 dinámico según formato de audio
- Row 6 con click handlers para BPM y Key
- Sistema de logging
- MediaInfo para información técnica de audio
- Guardado de metadatos
- Drag & drop

---

## 8. Sistema de Archivos de Build

```
bin/
├── Beta/
│   ├── framework-dependent/    # Requiere .NET 8 Runtime
│   │   ├── ToneAndBeatsByHostility.exe
│   │   └── [39 DLLs]
│   └── single-file/           # Self-contained
│       └── ToneAndBeatsByHostility.exe (~158MB)
└── Checkpoint-1140AM/        # Backup del build de las 11:40 AM
```

---

## 9. Notas de Desarrollo

### Licencias de Librerías

Ver archivo `LICENSES.md` para información detallada.

### Compatibilidad

- **Windows**: 10/11 (x64)
- **.NET**: 8.0 Runtime o superior
- **Arquitectura**: x64

---

*Documentación actualizada: 6 de Abril de 2026*
