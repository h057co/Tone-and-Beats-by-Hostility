# Tone & Beats by Hostility

Aplicación de escritorio Windows para análisis de audio que detecta BPM y tonalidad musical.

## Versión 1.0.2 - Release (LUFS Module)

**Fecha:** 8 de Abril de 2026  
**Estado:** ✅ Estable / Production Ready

---

## Requisitos

- **Windows 10/11** (x64)
- **No requiere .NET Runtime** (self-contained)
- **FFmpeg** (incluido en la carpeta `ffmpeg/`)

## Descarga e Instalación

### Opción 1: Instalador (Recomendado)
Ejecutar: `installer/ToneAndBeatsByHostility_Setup_v1.0.0.exe`

### Opción 2: Portable
Carpeta: `publish/` (contiene `ffmpeg/` para análisis LUFS)

---

## Características

- ✅ Detección de BPM automático (todos los formatos)
- ✅ Detección de tonalidad (Key)
- ✅ Análisis de loudness: LUFS, LRA, True Peak
- ✅ Soporte para FLAC, MP3, WAV, OGG, M4A, AAC, AIFF, WMA
- ✅ Visualización de forma de onda
- ✅ Reproducción de audio con seek
- ✅ Guardar metadatos en archivo
- ✅ Temas: Dark / Light / Blue
- ✅ Análisis paralelo (más rápido)
- ✅ Redimensionado proporcional
- ✅ Soporte para FLAC, MP3, WAV, OGG, M4A, AAC, AIFF, WMA
- ✅ Visualización de forma de onda
- ✅ Reproducción de audio con seek
- ✅ Guardar metadatos en archivo
- ✅ Temas: Dark / Light / Blue
- ✅ Análisis paralelo (más rápido)
- ✅ Redimensionado proporcional

---

## Guía de Uso

### Cargar Archivo
- Click en "Browse" o arrastrar archivo

### Analizar
- Click en "🔍 Analyze Audio"
- Procesa BPM + Key + Waveform en paralelo

### Ajustar BPM
- Click izquierdo: ×2 o ÷2
- Click derecho: acción inversa
- Segundo click: reset

### Cambiar Tonalidad
- Click en Key: alterna Major/Minor relativo

### Temas
- Click en 🎨 para cambiar tema

---

## Estructura del Proyecto

```
src/
├── MainWindow.xaml / .cs            # Interfaz principal
├── AboutWindow.xaml / .cs           # Acerca de
├── Services/                         # Lógica de negocio
│   ├── BpmDetector.cs              # Detección BPM
│   ├── KeyDetector.cs              # Detección Key
│   ├── WaveformAnalyzer.cs         # Waveform
│   └── AudioPlayerService.cs       # Reproducción
├── ViewModels/                      # MVVM
│   └── MainViewModel.cs
└── Themes/                         # Temas
    ├── DarkTheme.xaml
    ├── LightTheme.xaml
    └── BlueTheme.xaml
```

---

## Librerías

| Librería | Propósito |
|----------|-----------|
| NAudio | Audio playback |
| BpmFinder | BPM detection (MP3/WAV) |
| MediaInfo.Wrapper.Core | Audio metadata |
| TagLibSharp | Write metadata |
| KeyDetector (custom) | Key detection (implementación propia) |

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

## Documentación

- `DOCUMENTACION.md` - Documentación técnica completa
- `LICENSE.txt` - Licencia y Copyright

---

## Copyright

```
© 2026 Hostility Music. www.hostilitymusic.com. Todos los derechos reservados.
info@hostilitymusic.com
```

---

## Contacto

- **Web:** www.hostilitymusic.com
- **Email:** info@hostilitymusic.com

---

*Version 1.0.1 - Release (Hotfix)*
*Desarrollado por Luis Jimenez - Hostility Music*