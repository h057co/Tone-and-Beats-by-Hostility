# Tone & Beats by Hostility - Documentación Técnica

**Versión:** 1.2.0  
**Fecha:** 1 de Mayo de 2026  
**Framework:** .NET 8.0 + WPF  
**Estado:** Release (Donationware) - Optimizado
**Licencia:** CC BY-NC-ND 4.0

---

## 1. Descripción del Proyecto

**Tone & Beats by Hostility** es una aplicación de escritorio Windows de alto rendimiento para el análisis de audio, diseñada para DJs y productores. Utiliza un motor híbrido avanzado para extraer:
- **BPM Inteligente**: Detección con corrección automática de armónicos y perfiles de rango.
- **Tonalidad (Key)**: Identificación basada en el algoritmo Krumhansl-Schmuckler (Camelot Wheel).
- **Loudness Profesional**: Medición EBU R128 (LUFS Integrated, LRA, True Peak).
- **Visualización Waveform**: Renderizado de alta fidelidad con SkiaSharp.

---

## 2. Arquitectura del Proyecto

El proyecto sigue el patrón **MVVM (Model-View-ViewModel)** para una separación clara entre la interfaz y la lógica DSP.

### Estructura de Servicios
- **BpmDetector**: Orquestador del motor híbrido de tempo.
- **WaveformAnalyzer**: Motor DSP para análisis de transientes, flujo espectral y FFT.
- **KeyDetector**: Analizador de perfiles cromáticos para detección de escala.
- **LoudnessAnalyzer**: Integración con FFmpeg para normalización y medición de volumen.

---

## 3. Algoritmos de Análisis (v1.2.0)

### 3.1 Motor Híbrido de BPM (Triple-Engine)
El sistema utiliza un modelo de **Consenso por Voto** entre tres motores independientes:

1.  **SoundTouch**: Autocorrelación en el dominio del tiempo (Rápido y estable).
2.  **TransientGrid**: Detección de picos de amplitud y ajuste de rejilla rítmica.
3.  **SpectralFlux**: Análisis de cambios de energía espectral (Ideal para audio masterizado).

**Innovación v1.2.0 (Detección Inteligente)**:
- **Rescate de Trap/Reggaetón**: Heurística específica para detectar el tresillo urbano y aplicar el multiplicador 1.5x cuando es necesario.
- **Guardia de Estabilidad**: Filtro de sub-graves (100Hz) y umbrales de confianza dinámicos para evitar errores de octava en temas rápidos (ej. 108 BPM).

### 3.2 Detección de Tonalidad
- Implementación avanzada del perfil de clases de pitch (PCP).
- FFT de alta resolución (16384 samples) para una discriminación precisa de semitonos.
- Soporte nativo para la Rueda Camelot.

---

## 4. Historial de Versiones Relevante

### v1.2.0 (1 de Mayo 2026) - INTEL DETECTOR
- ✅ **BPM Inteligente**: Límite de rescate Trap ajustado a 105 BPM para evitar errores en temas de 108-115 BPM.
- ✅ **Umbrales Dinámicos**: Mayor exigencia de confianza para multiplicadores en tempos altos.
- ✅ **Limpieza de Documentación**: Consolidación técnica y eliminación de archivos obsoletos.
- ✅ **Interacción UI**: Simplificación del cambio de BPM (Swap Directo entre Match/Alt).

### v1.1.0 (Abril 2026) - DSP REFACTOR
- ✅ Implementación de Spectral Flux.
- ✅ Sistema de perfiles de rango (Auto, Low, Mid, High).
- ✅ Medición de Loudness EBU R128 integrada.

---

## 5. Construcción y Release

### Orquestación
El proyecto utiliza `./release_build.ps1` para generar binarios `win-x64` auto-contenidos. La versión se centraliza en `src/AudioAnalyzer.csproj` y se propaga automáticamente a la UI mediante Reflection.

---

**© 2026 Luis Jiménez (Hostility) - Medellín, Colombia**  
*Desarrollado con pasión por la música y la ingeniería de audio.*