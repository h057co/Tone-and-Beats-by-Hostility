# Tone & Beats by Hostility - JUCE (macOS / Linux) Architecture Plan

Este documento es una guía de arquitectura y migración técnica diseñada para un **Senior C++ Developer**. Su propósito es portar la aplicación WPF/C# "Tone & Beats by Hostility" a **macOS y Linux** utilizando el framework **JUCE 7/8**.

---

## 1. Mapeo de Tecnologías (C# -> C++)

Para replicar con exactitud el comportamiento actual de la app (BPM 100% exacto, Key Detection, metadatos ID3v2/BEXT, Waveform rendering y diseño gráfico premium), se usará el siguiente stack nativo:

| Funcionalidad | Stack Actual (Windows / C#) | Stack Propuesto (macOS/Linux / C++) |
| :--- | :--- | :--- |
| **Framework Base / GUI** | WPF (.NET 8) | **JUCE Framework** (`juce_gui_basics`) |
| **Decodificación de Audio** | NAudio / MediaFoundation | `juce_audio_formats` (WAV, AIFF, MP3, FLAC, OGG) |
| **Detección de Tempo (BPM)** | SoundTouch.Net + Custom Logic | **SoundTouch C++** (Nativo) + `juce::dsp::FFT` |
| **Detección Tonal (Key)** | Algoritmo C# Custom | **Aubio (C)** (Estándar de la industria, 100% de precisión) |
| **Metadatos (Lectura Técnica)**| MediaInfo.Wrapper | `juce::AudioFormatReader` + **TagLib (C++)** |
| **Metadatos (Escritura Tag)** | TagLibSharp | **TagLib (C++)** nativo (`TKEY`, `TBPM`) |
| **Análisis de Volumen (LUFS)** | FFmpeg (CLI Executable) | **`libebur128`** (Resultados 1:1 con FFmpeg loudnorm) |

> [!TIP]
> **Optimización LUFS:** Al usar `libebur128` en C++, eliminamos la necesidad de empaquetar un binario gigante de FFmpeg (~100MB), reduciendo drásticamente el peso de la app en macOS/Linux y mejorando la velocidad de análisis.

---

## 2. Arquitectura del Proyecto CMake / Projucer

Se recomienda estructurar el proyecto mediante **CMake**, ya que es el estándar de la industria y facilita la integración de dependencias como TagLib y libebur128.

### Estructura de Directorios recomendada:
```text
ToneAndBeats_JUCE/
├── CMakeLists.txt
├── modules/
│   ├── JUCE/                 # Submodulo Git de JUCE
│   ├── taglib/               # Source/Binary de TagLib
│   ├── libebur128/           # Source de EBU R128
│   └── soundtouch/           # Source de SoundTouch
├── Source/
│   ├── Main.cpp              # Entry point
│   ├── MainComponent.cpp     # Vista principal (Drag & Drop, Layout)
│   ├── Core/
│   │   ├── BpmDetector.cpp   # Algoritmo de 4 etapas transpilar de C#
│   │   ├── KeyDetector.cpp   # Wrapper de Aubio/Chromagram
│   │   ├── Loudness.cpp      # Wrapper para libebur128
│   │   └── Metadata.cpp      # TagLib writer (ID3v2, Vorbis)
│   └── UI/
│       ├── LookAndFeel.cpp   # Estilo oscuro, branding "Hostility"
│       ├── WaveformView.cpp  # Dibujado de onda (juce::AudioThumbnail)
│       └── CustomButtons.cpp
└── Resources/
    ├── Fonts/                # Inter, Roboto (Embebidos)
    └── Images/               # Logos Hostility (SVG/PNG)
```

---

## 3. Implementación de la Interfaz Gráfica (GUI)

La estética debe sentirse *premium*, oscura y moderna, idéntica a la versión WPF. 

### A. LookAndFeel Personalizado
Debes heredar de `juce::LookAndFeel_V4` para sobreescribir los colores globales y deshabilitar los bordes redondos por defecto si interfieren con el diseño flat/glassmorphism.

```cpp
class HostilityLookAndFeel : public juce::LookAndFeel_V4
{
public:
    HostilityLookAndFeel()
    {
        // Paleta de colores oscuros (Dark Mode)
        setColour(juce::ResizableWindow::backgroundColourId, juce::Colour(0xff121212));
        setColour(juce::TextButton::buttonColourId, juce::Colour(0xff2a2a2a));
        setColour(juce::TextButton::textColourOffId, juce::Colours::white);
        // ... (Ajustar colores base)
    }

    // Sobreescribir pintado de botones para estética flat / hover dynamics
    void drawButtonBackground (juce::Graphics& g, juce::Button& button, const juce::Colour& backgroundColour,
                               bool shouldDrawButtonAsHighlighted, bool shouldDrawButtonAsDown) override
    {
        auto bounds = button.getLocalBounds().toFloat();
        auto baseColor = backgroundColour;

        if (shouldDrawButtonAsDown)        baseColor = baseColor.darker(0.2f);
        else if (shouldDrawButtonAsHighlighted) baseColor = baseColor.brighter(0.1f);

        g.setColour(baseColor);
        g.fillRoundedRectangle(bounds, 4.0f); // Bordes redondeados sutiles
    }
};
```

### B. Drag & Drop y Renderizado de Waveform
La clase `MainComponent` debe heredar de `juce::FileDragAndDropTarget` para recibir los archivos de audio.

```cpp
class MainComponent : public juce::Component, public juce::FileDragAndDropTarget
{
public:
    // ...
    bool isInterestedInFileDrag (const juce::StringArray& files) override {
        return files.size() == 1; // Aceptamos 1 archivo a la vez por ahora
    }

    void filesDropped (const juce::StringArray& files, int x, int y) override {
        auto filePath = files[0];
        // 1. Mostrar loading overlay
        // 2. Disparar thread de análisis de BpmDetector, KeyDetector y LUFS
        // 3. Cargar waveform
        loadAudioFile(filePath);
    }
    
private:
    juce::AudioFormatManager formatManager;
    juce::AudioThumbnailCache thumbnailCache;
    juce::AudioThumbnail waveform;
    
    void paint (juce::Graphics& g) override {
        // Pintar fondo y contenedor
        g.fillAll (getLookAndFeel().findColour (juce::ResizableWindow::backgroundColourId));
        
        // Dibujar Waveform con color dinámico/branding
        auto waveformBounds = getLocalBounds().reduced(20).withHeight(100);
        g.setColour(juce::Colour(0xff00d2ff)); // Color Cyan/Accent de Hostility
        if (waveform.isFullyLoaded())
            waveform.drawChannels(g, waveformBounds, 0.0, waveform.getTotalLength(), 1.0f);
    }
};
```

---

## 4. Motor de Análisis (Refactorización a C++)

### A. BPM Detector (El Flujo de 4 Etapas)
En C# implementamos un algoritmo preciso (100% de aciertos) con 4 etapas (Multi-Motor, Pool de Candidatos, Guards y Final Gate). En JUCE, usarás un hilo en background (`juce::Thread` o `juce::ThreadPoolJob`):

```cpp
double BpmDetector::analyzeBpm(const juce::File& file)
{
    // 1. Extraer samples usando juce::AudioFormatReader
    // 2. Pasar buffer por SoundTouch C++ configurado para tempo:
    /*
        soundtouch::SoundTouch st;
        st.setSampleRate(reader->sampleRate);
        st.setChannels(reader->numChannels);
        st.setTempo(1.0); // Búsqueda neutra
    */
    // 3. Implementar el algoritmo de "Pool de Candidatos Reales".
    // Extraer transientes usando juce::dsp::FFT o un simple envelope follower.
    // 4. Seleccionar el mejor BPM basado en el perfil y score de energía.
    return finalBpm;
}
```

### B. Análisis LUFS (libebur128)
En lugar de iniciar un sub-proceso, C++ permite analizar los frames de audio directamente en memoria a una velocidad abismal.

```cpp
#include "ebur128.h"

LoudnessResult LoudnessAnalyzer::analyze(juce::AudioBuffer<float>& buffer, double sampleRate)
{
    ebur128_state* state = ebur128_init(buffer.getNumChannels(), (unsigned long)sampleRate, 
                                        EBUR128_MODE_I | EBUR128_MODE_LRA | EBUR128_MODE_TRUE_PEAK);
    
    // Convertir AudioBuffer a array entrelazado (interleaved)
    // Pasar a ebur128_add_frames_float(state, interleavedData, numFrames);
    
    double integratedLufs = 0.0;
    ebur128_loudness_global(state, &integratedLufs);
    
    double lra = 0.0;
    ebur128_loudness_range(state, &lra);
    
    double truePeak = 0.0;
    ebur128_sample_peak(state, 0, &truePeak); // Iterar canales si se requiere True Peak real
    
    ebur128_destroy(&state);
    
    return { integratedLufs, lra, truePeak };
}
```

---

## 5. Escritura de Metadatos (TagLib C++)

Para asegurar compatibilidad con la industria DJ (Serato, Rekordbox), **NO** se debe guardar el BPM como un simple entero, ni el Key en los Comentarios. Se usará TagLib nativo de C++.

```cpp
#include <taglib/fileref.h>
#include <taglib/tag.h>
#include <taglib/id3v2tag.h>
#include <taglib/textidentificationframe.h>

void MetadataWriter::saveTags(const juce::File& file, double bpm, const juce::String& key)
{
    TagLib::FileRef f(file.getFullPathName().toUTF8());
    
    if(!f.isNull() && f.tag()) {
        // Redondeo clásico para reproductores estándar
        f.tag()->setBPM(static_cast<unsigned int>(std::round(bpm)));
        
        // Acceder a ID3v2 para inyección avanzada en MP3, AIFF y WAV
        if (auto* id3v2Tag = dynamic_cast<TagLib::ID3v2::Tag*>(f.file()->tag())) {
            
            // Escribir TKEY (Tonalidad Oficial)
            TagLib::ID3v2::TextIdentificationFrame* tkeyFrame = 
                new TagLib::ID3v2::TextIdentificationFrame("TKEY", TagLib::String::UTF8);
            tkeyFrame->setText(key.toStdString());
            id3v2Tag->addFrame(tkeyFrame);
            
            // Escribir TBPM exacto (Con Decimales)
            TagLib::ID3v2::TextIdentificationFrame* tbpmFrame = 
                new TagLib::ID3v2::TextIdentificationFrame("TBPM", TagLib::String::UTF8);
            char bpmStr[32];
            snprintf(bpmStr, sizeof(bpmStr), "%.2f", bpm);
            tbpmFrame->setText(bpmStr);
            id3v2Tag->addFrame(tbpmFrame);
        }
        
        f.save();
    }
}
```

---

## 6. Siguientes Pasos (Roadmap de Ejecución)

1. **Setup Inicial:** Configurar CMake con JUCE, añadiendo `juce_audio_formats`, `juce_audio_utils`, `juce_gui_extra` y `juce_dsp`.
2. **Dependencias:** Clonar/Vincular `TagLib`, `libebur128` y `soundtouch`.
3. **Core Lógico:** Transpilar la lógica de análisis del archivo `BpmDetector.cs` y `KeyDetector.cs` hacia C++. (Revisar los tests de Windows para asegurar el 100% de precisión).
4. **GUI:** Montar el `MainComponent` con soporte de Drag and Drop, dibujando la forma de onda.
5. **Testing Cruzado:** Compilar en macOS (x86_64 y arm64/Apple Silicon) y Linux (Ubuntu/Debian) probando con los mismos archivos del test original (`audiotest/`).
