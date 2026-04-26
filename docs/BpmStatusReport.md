# Reporte de Estado: Módulo de Detección de BPM
**Fecha:** 26 de Abril de 2026
**Estado:** Experimentación (Rama: `experiment/ajuste-bpm`)

## 1. Estado Actual
El sistema utiliza un pipeline de **ensamble multi-fuente** que combina análisis en el dominio del tiempo y la frecuencia. Actualmente tiene una precisión del **95%** (19 de 20 archivos en el baseline) tras las optimizaciones de la v1.0.10.

### Métricas y Rendimiento
- **Acierto (Match):** 19/20 archivos.
- **Error Crítico (Fail):** 1 archivo (audio11 - mejora de error de 17.5 a 1.5 BPM).
- **Consumo:** Optimizado mediante downsampling adaptativo y selección de segmentos representativos (máx 60s).

---

## 2. Flujo de Funcionamiento (Workflow)

El flujo se divide en 7 etapas críticas dentro de `BpmDetector.cs`:

1.  **Carga y Mono-Mixdown:** 
    - El `AudioDataProvider` lee el archivo y lo convierte a un array de floats en mono para procesamiento rápido en memoria.
2.  **Estimación Rápida (SoundTouch):**
    - Se realiza una pasada inicial con la librería SoundTouch para obtener un pulso base. Es muy estable pero propenso a errores de "double-time" (2x).
3.  **Selección de Segmento:**
    - El algoritmo detecta dónde comienza la energía sostenida (ignora intros silenciosas o ambientales) y selecciona hasta 60 segundos de audio.
4.  **Extracción de Características (Dual-Path):**
    - **Path A (Transientes):** Filtro High-Pass (Pre-emphasis) para resaltar bombos y cajas. Se detectan picos de energía (TransientGrid).
    - **Path B (Espectral):** Análisis de cambios en el espectro de frecuencia (SpectralFlux), robusto ante mastering pesado.
5.  **Sistema de Votación (Consenso):**
    - Se comparan los resultados de SoundTouch, Grid y SF. 
    - Si dos coinciden (margen ±5 BPM) -> Consenso.
    - Si no hay coincidencia, gana el de mayor confianza (Prioridad: SF > Grid > ST).
6.  **Heurísticas Especializadas:**
    - **Guardia de Trap:** Aplica corrección 1.5x en rangos específicos (ej: 101.4 -> 152).
    - **Guardia de Half-Time:** Detecta si el motor fue engañado por un sub-armónico.
    - **Fallback Avanzado:** Si todo falla, usa detección en dominio complejo (fase).
7.  **Normalización y Salida:**
    - Ajuste según el perfil de rango seleccionado (Auto, 50-100, etc.).
    - Redondeo inteligente (Snap to integer si está a menos de 0.3 BPM).

---

## 3. Problemas Conocidos y Oportunidades
- **Ambigüedad 2/3 (Tresillo):** Algunos géneros (Reggaetón/Trap) todavía confunden el pulso base con el tresillo.
- **Resolución de Autocorrelación:** Limitada a pasos de 0.5 BPM en el motor de transientes.
- **Archivos Altamente Comprimidos:** El mastering extremo "aplasta" los transientes, dejando al SpectralFlux como única fuente fiable.

---

## 4. Plan de Experimentación (Rama actual)
- [ ] Refinar la resolución de la autocorrelación a 0.1 BPM.
- [ ] Mejorar la detección de baladas/audio suave mediante el análisis de fase.
- [ ] Validar contra el archivo `audio11` para alcanzar el 100% de match.
