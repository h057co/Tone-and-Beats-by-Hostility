# Tone & Beats by Hostility — Rhythmic & Tonality Engine v1.2.0

![JUCE Framework](https://img.shields.io/badge/Framework-JUCE%20v7-blue.svg)
![C++ Version](https://img.shields.io/badge/C%2B%2B-17-orange.svg)
![License](https://img.shields.io/badge/License-Proprietary-red.svg)

**Tone & Beats** es una suite profesional de análisis de audio MIR (Music Information Retrieval) diseñada específicamente para flujos de trabajo de producción urbana (Trap, Reggaetón, Hip-Hop). El sistema combina algoritmos de inducción de tempo global con validación de rejilla rítmica para ofrecer la máxima precisión en géneros con síncopas complejas.

## 🚀 Características Principales

### 1. Motor de Análisis de BPM (Professional Offline Architecture)
Arquitectura de análisis multi-segmento que elimina las ambigüedades comunes de octava (half-time/double-time).
*   **Global Consensus Voting**: Análisis triple (Inicio, Medio, Final) con sistema de votación para estabilidad total.
*   **Trap & Urban Heuristics**: Resolutor de ambigüedad de *Tresillo* (101 vs 152 BPM) y anclaje de octava basado en inducción espectral.
*   **Pulse Density Bias**: Algoritmo propietario que favorece el tempo musical percibido sobre la frecuencia rítmica técnica.

### 2. Detección de Tonalidad (Key Detection)
Análisis armónico basado en perfiles de croma optimizados.
*   **Filtro de Energía**: Discriminación de ruido y transientes para un análisis puramente armónico.
*   **Normalización Krumhansl-Schmuckler**: Perfiles de tonalidad refinados para música moderna.

### 3. Loudness & Normalización
*   **Cumplimiento EBU R128**: Medición de LUFS (Integrated, Short-term, Momentary).
*   **True Peak Analysis**: Detección de picos reales para masterización segura.

## 🛠 Stack Tecnológico
*   **Core**: JUCE Framework (v7.0+)
*   **DSP Inducción**: SoundTouch (Time-stretch & BPM Induction)
*   **Metadata**: TagLib (Lectura/Escritura de BWF, ID3v2, MP4)
*   **Loudness**: libebur128 (Estándar de la industria para normalización)

## 📂 Estructura del Proyecto
*   `Source/Core/`: Motores de procesamiento de señal (DSP).
*   `Source/UI/`: Interfaz de usuario responsiva y overlays interactivos.
*   `Source/Tests/`: Harness de validación automática del dataset.

## ⚙️ Compilación
El proyecto utiliza CMake para una gestión de dependencias multiplataforma.

### Windows (Visual Studio 2022)
```powershell
mkdir build
cd build
cmake .. -G "Visual Studio 17 2022" -A x64
cmake --build . --config Release
```

### macOS (Xcode / Apple Silicon)
```bash
mkdir build
cd build
cmake .. -G Xcode
cmake --build . --config Release
```

---
**© 2026 Luis Jiménez (Hostility) - Medellín, Colombia**  
*Ingeniería de Audio Avanzada para la Nueva Era Urbana.*