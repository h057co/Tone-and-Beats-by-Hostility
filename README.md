# Tone & Beats by Hostility

**Versión:** 1.0.6  
**Tipo:** Donationware - Aplicación de análisis de audio para DJs y Productores Musicales  
**Licencia:** [CC BY-NC-ND 4.0](LICENSE.md)

---

## Acerca del Autor

**Creador:** Luis Jiménez aka Hostility  
**Ubicación:** Medellín, Colombia  
**Contacto:** info@hostilitymusic.com  
**Sitio Web:** [www.hostilitymusic.com](https://www.hostilitymusic.com)

Esta aplicación fue diseñada y desarrollada exclusivamente por Hostility en Medellín, Colombia. Todos los derechos de autor sobre el código base son propiedad de Luis Jiménez (Hostility).

---

## Descripción

Tone & Beats by Hostility es una potente herramienta de análisis de audio diseñada específicamente para DJs, Productores Musicales y Beatmakers. La aplicación permite analizar archivos de audio y extraer información precisa sobre:

- **BPM (Tempo):** Detección automática del tempo de la canción
- **Key (Tonalidad):** Identificación de la tonalidad musical (Camelot Wheel)
- **Waveform (Forma de onda):** Visualización gráfica de la onda de audio
- **Loudness (Volumen):** Medición de LUFS, LRA y True Peak (dBTP)

---

## Características

### Análisis de Audio
- Detección de BPM precisa
- Identificación de tonalidad musical
- Visualización de forma de onda
- Análisis de volumen (LUFS, LRA, True Peak)

### Formatos Soportados
- MP3, WAV, FLAC, OGG, M4A, AAC, WMA, OPUS

### Temas Disponibles
- Dark (Oscuro)
- Light (Claro)
- Blue (Azul)
- iOS Light
- iOS Dark

### Requisitos del Sistema
- **Sistema Operativo:** Windows 10 (64-bit) o posterior
- **Espacio en Disco:** ~150 MB
- **Memoria RAM:** 4 GB mínimo recomendado

---

## Instalación

### Instalador (Recomendado)
1. Descarga `ToneAndBeatsByHostility_Setup_v1.0.6.exe` desde la sección de Releases
2. Ejecuta el instalador
3. Sigue las instrucciones del asistente
4. Opcional: Crea acceso directo en escritorio

### Versión Portable
1. Descarga el archivo ejecutable
2. Ejecuta directamente `ToneAndBeatsByHostility.exe`

---

## Donationware - Apoyo al Proyecto

**Esta aplicación es Donationware.**

Si esta herramienta te ha sido útil en tu trabajo como DJ, Productor Musical, Beatmaker o entusiasta de la música, considera realizar una donación voluntaria para apoyar el desarrollo continuo del proyecto.

### Cómo Donar
- **KoFi:** Puedes donate a través del botón KoFi en la aplicación (Acerca de)
- **QR Donaciones:** Escanea el código QR en la ventana Acerca de

Tu apoyo permite que Hostility continúe mejorando y manteniendo esta aplicación gratuitamente.

---

## Licencia

### Licencia Principal: CC BY-NC-ND 4.0

Esta aplicación está protegida bajo la licencia **Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International (CC BY-NC-ND 4.0)**.

**Esto significa:**
- ✅ **Atribución:** Debes dar crédito apropiado a Hostility
- ✅ **Uso Personal:** Puedes usar la aplicación para fines personales
- ✅ **Donaciones:** Los usuarios pueden hacer donaciones voluntarias
- ❌ **No Comercial:** No puedes usar esta aplicación para generar ingresos
- ❌ **No Derivados:** No puedes crear obras derivadas ni modificaciones
- ❌ **No Redistribución:** No puedes redistribuir la aplicación sin autorización

### Licencias de Librerías Externas

Esta aplicación utiliza librerías de terceros con sus propias licencias. Ver archivo [LICENSES.md](LICENSES.md) para detalles completos.

| Librería | Propósito | Licencia |
|----------|-----------|----------|
| NAudio | Reproducción y análisis de audio | Ms-PL |
| NAudio.Vorbis | Soporte para formato OGG | Ms-PL |
| FFMpegCore | Análisis loudness (LUFS) | MIT |
| MediaInfo.Wrapper.Core | Extracción de metadatos | BSD-2-Clause |
| TagLibSharp | Lectura/escritura de tags | LGPL v2.1 |
| SoundTouch.Net | Procesamiento de audio (BPM) | LGPL v2.1 |

---

## Advertencias Legales

1. **Propiedad Intelectual:** Todo el código fuente y recursos de esta aplicación son propiedad exclusiva de Luis Jiménez (Hostility). La descarga o uso de esta aplicación no otorga ningún derecho de propiedad a terceros.

2. **Uso Comercial Prohibido:** Queda estrictamente prohibido el uso de esta aplicación para fines comerciales, incluyendo pero no limitado a: venta, alquiler, licensing comercial, o cualquier forma de monetización.

3. **Redistribución Prohibida:** No está permitida la redistribución de esta aplicación en ningún formato sin autorización expresa del autor.

4. **Sin Garantías:** Esta aplicación se proporciona "tal cual", sin garantías de ningún tipo, expresas o implícitas.

---

## Estructura del Proyecto

```
src/
├── App.xaml / App.xaml.cs           # Punto de entrada WPF
├── MainWindow.xaml / .cs            # Interfaz principal
├── AboutWindow.xaml / .cs            # Acerca de
├── Controls/                         # Controles personalizados
│   └── WaveformControl.xaml / .cs   # Visualizador de forma de onda
├── Services/                         # Lógica de negocio
│   ├── AudioAnalysisPipeline.cs     # Orquestación de análisis
│   ├── AudioDataProvider.cs         # Carga centralizada de audio
│   ├── AudioPlayerService.cs        # Reproducción
│   ├── AudioReaderFactory.cs        # Factory de formatos
│   ├── BpmDetector.cs               # Detección BPM híbrida
│   ├── FftHelper.cs                 # Utilidades FFT compartidas
│   ├── KeyDetector.cs               # Detección de tonalidad
│   ├── LoudnessAnalyzer.cs          # Análisis LUFS/LRA
│   ├── WaveformAnalyzer.cs          # Análisis de forma de onda
│   └── LoggerService.cs             # Sistema de logging
├── ViewModels/                       # MVVM
│   └── MainViewModel.cs
├── Models/                           # Modelos de datos
│   ├── AudioFileInfo.cs
│   ├── LoudnessResult.cs
│   └── WaveformData.cs
├── Themes/                          # Temas visuales
│   ├── DarkTheme.xaml
│   ├── LightTheme.xaml
│   ├── BlueTheme.xaml
│   ├── IosLightTheme.xaml
│   └── IosDarkTheme.xaml
├── Infrastructure/                   # Utilidades
│   ├── BoolToVisibilityConverter.cs
│   ├── CornerResizeBehavior.cs
│   ├── FilePickerService.cs
│   ├── LevelToBrushConverter.cs
│   ├── MessageBoxService.cs
│   ├── StringToVisibilityConverter.cs
│   └── ViewModelBase.cs
├── Interfaces/                      # Contratos de servicios
│   ├── IAudioAnalysisPipeline.cs
│   ├── IAudioPlayerService.cs
│   ├── IBpmDetectorService.cs
│   ├── IFilePickerService.cs
│   ├── IKeyDetectorService.cs
│   ├── ILoudnessAnalyzerService.cs
│   ├── IMessageBoxService.cs
│   └── IWaveformAnalyzerService.cs
├── Commands/                        # Comandos MVVM
│   └── RelayCommand.cs
└── Helpers/                         # Helpers
    └── EmbeddedResourceHelper.cs
```

---

## Desarrollo

```bash
# Compilar
cd src
dotnet build

# Ejecutar
dotnet run

# Publicar
dotnet publish -c Release -r win-x64 --self-contained true -o ../publish
```

---

## Contacto

Para reportes de errores, sugerencias o consultas:
- **Email:** info@hostilitymusic.com
- **Sitio Web:** www.hostilitymusic.com

---

**© 2026 Luis Jiménez (Hostility) - Medellín, Colombia**

Todos los derechos reservados. Tone & Beats by Hostility es una marca registrada.

*Desarrollado por Luis Jiménez - Hostility - Medellín, Colombia*