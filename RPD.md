# RPD - Tone & Beats by Hostility

## 1. Información General

| Campo | Detalle |
|-------|---------|
| **Nombre del Proyecto** | Tone & Beats by Hostility |
| **Versión Actual** | 1.0.3 (Donationware Release) |
| **Fecha de Lanzamiento** | 9 de Abril de 2026 |
| **Última Actualización** | 9 de Abril de 2026 |
| **Desarrollador** | Luis Jiménez (Hostility) - Medellín, Colombia |
| **Plataforma** | Windows 10/11 (x64) |
| **Tipo** | Aplicación de escritorio WPF |
| **Licencia** | CC BY-NC-ND 4.0 (Donationware) |
| **Repositorio** | https://github.com/h057co/Tone-and-Beats-by-Hostility |

---

## 2. Descripción del Proyecto

**Tone & Beats by Hostility** es una aplicación de escritorio Windows para el análisis de audio que detecta automáticamente:
- **BPM** (Beats Per Minute) del archivo de audio
- **Tonalidad** (Key) en formato estándar musical (C, C#, D, etc.)
- **Información técnica** del archivo (formato, sample rate, bit depth, bitrate, canales)
- **Forma de onda** (Waveform) visual

### Propósito
Herramienta para DJs, productores musicales y profesionales del audio que necesitan conocer rápidamente los metadatos técnicos de archivos de audio.

---

## 3. Stack Tecnológico

| Componente | Tecnología |
|------------|------------|
| **Framework** | .NET 8.0 + WPF |
| **Lenguaje** | C# |
| **Arquitectura** | MVVM |
| **Build** | dotnet publish (self-contained) |
| **Instalador** | Inno Setup 6 |

### Librerías Principales

| Librería | Propósito |
|----------|-----------|
| NAudio 2.2.1 | Reproducción y análisis de audio |
| BpmFinder 0.1.0 | Detección de BPM (MP3/WAV) |
| MediaInfo.Wrapper.Core 26.1.0 | Metadatos técnicos de audio |
| TagLibSharp 2.3.0 | Escritura de metadatos en archivos |
| KeyDetector (custom) | Detección de tonalidad (implementación propia) |
| FFMpegCore 5.1.0 | Análisis de loudness LUFS (wrapper de FFmpeg) |

---

## 4. Funcionalidades Principales

### 4.1 Análisis de Audio
- ✅ Detección automática de BPM
- ✅ Detección automática de tonalidad (Key)
- ✅ Detección de tonalidad relativa (Major/Minor)
- ✅ Visualización de forma de onda
- ✅ Análisis paralelo (BPM + Key + Waveform + Loudness simultáneos)
- ✅ Análisis de loudness: LUFS Integrated, LRA, True Peak

### 4.2 Reproducción
- ✅ Reproducción de audio con controles Play/Pause/Stop
- ✅ Seek interactivo en la forma de onda
- ✅ Información técnica del archivo

### 4.3 Interfaz de Usuario
- ✅ Sistema de temas (Dark / Light / Blue)
- ✅ Interfaz redimensionable con escala proporcional
- ✅ Redimensionado solo desde esquinas
- ✅ Drag & drop de archivos

### 4.4 Metadatos
- ✅ Guardar BPM y Key en metadatos del archivo
- ✅ Formatos soportados: MP3, WAV, OGG, FLAC, M4A, AAC, AIFF, WMA

---

## 5. Estructura del Proyecto

```
Tone and Beats/
├── src/                          # Código fuente
│   ├── MainWindow.xaml/.cs       # Ventana principal
│   ├── AboutWindow.xaml/.cs      # Ventana Acerca de
│   ├── App.xaml/.cs              # Entry point WPF
│   ├── Services/                 # Lógica de negocio
│   │   ├── AudioPlayerService.cs
│   │   ├── BpmDetector.cs
│   │   ├── KeyDetector.cs
│   │   ├── WaveformAnalyzer.cs
│   │   ├── MetadataWriter.cs
│   │   └── LoggerService.cs
│   ├── ViewModels/               # MVVM
│   │   └── MainViewModel.cs
│   ├── Models/                   # Modelos de datos
│   │   ├── AudioFileInfo.cs
│   │   └── WaveformData.cs
│   ├── Controls/                 # Controles personalizados
│   │   └── WaveformControl.xaml/.cs
│   ├── Themes/                   # Temas visuales
│   │   ├── DarkTheme.xaml
│   │   ├── LightTheme.xaml
│   │   └── BlueTheme.xaml
│   ├── Infrastructure/           # Clases de soporte
│   ├── Interfaces/               # Contratos de servicios
│   └── Commands/                 # Comandos MVVM
├── publish/                      # Build portable
├── installer/                    # Instalador
├── DOCUMENTACION.md              # Documentación técnica
├── README.md                     # Guía de usuario
└── LICENSE.txt                   # Licencia
```

---

## 6. Estado del Proyecto

| Aspecto | Estado |
|---------|--------|
| **Versión** | 1.0.1 - Hotfix |
| **Estabilidad** | ✅ Estable / Production Ready |
| **Testing** | ✅ Funcional |
| **Documentación** | ✅ Completa |
| **Instalador** | ✅ Generado (Inno Setup) |
| **Bug Fijo** | ✅ BPM detección en archivos FLAC |

---

## 7. Distribución

| Método | Ubicación | Tamaño |
|--------|-----------|--------|
| **Instalador** | `installer/ToneAndBeatsByHostility_Setup_v1.0.0.exe` | ~140 MB |
| **Portable** | `publish/` (511 archivos) | ~150 MB |

### Requisitos del Sistema
- Windows 10/11 (x64)
- No requiere .NET Runtime (self-contained)

---

## 8. Historial de Versiones

| Versión | Fecha | Descripción |
|---------|-------|-------------|
| 1.0.2 | 8 Abril 2026 | Nuevo módulo LUFS: Detección de LUFS Integrated, LRA y True Peak. UI actualizada con Row 4 (Loudness). Etiqueta cambiada de "Short Term" a "LRA". |
| 1.0.1 | 7 Abril 2026 | Hotfix: Detectado bug - BpmFinder no soporta FLAC. Implementado algoritmo avanzado como fallback para formatos no soportados (OGG, FLAC, M4A, AAC, AIFF, WMA). |
| 1.0.0 | 7 Abril 2026 | Release oficial |
| 1.0.0-beta | 6 Abril 2026 | Beta inicial |

---

## 9. Contacto

- **Web:** www.hostilitymusic.com
- **Email:** info@hostilitymusic.com
- **Copyright:** © 2026 Hostility Music. Todos los derechos reservados.

---

## 10. Próximos Pasos (Backlog)

| # | Feature | Prioridad |
|---|---------|-----------|
| 1 | Batch Processing - Múlti archivos | Media |
| 2 | Botón Donation (PayPal) | Baja |
| 3 | Exportar reporte (CSV/JSON) | Baja |
| 4 | Detección de secciones | Baja |
| 5 | Mejoras UI waveform | Baja |

---

*Documento generado: 7 de Abril de 2026*
*Proyecto: Tone & Beats by Hostility v1.0.0*