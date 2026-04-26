# Reporte Técnico de Validación de Algoritmo BPM
**Fecha:** 26 de Abril de 2026
**Objetivo:** Validar la precisión del pipeline actual v1.0.10+ para el especialista en algoritmos.
**Metodología:** Test de línea de comandos sobre dataset de baseline (20 archivos).

## 1. Resumen Estadístico
| Métrica | Valor | Porcentaje |
|---------|-------|------------|
| **Total de Tests** | 20 | 100% |
| **MATCH (Primary)** | 14 | 70% |
| **ALT_MATCH (Alternative)** | 5 | 25% |
| **FAIL** | 1 | 5% |
| **Tasa de Éxito Total** | **19/20** | **95%** |

---

## 2. Análisis de Resultados Individuales

### 🟢 Éxitos Críticos (MATCH/ALT_MATCH)
- **Trap (152 BPM):** Los 4 archivos de Trap (con y sin master, MP3 y WAV) fueron detectados correctamente como `ALT_MATCH` con un Primary de **101.4 BPM** y un Alternativo exacto de **152.0 BPM**. Esto confirma que la lógica de tresillo/ratio 1.5x está funcionando como "safety net" para este género.
- **Reggaetón/Urban:** Detección sólida en archivos como `Ta Buena Rancha` (108 BPM) y `audio1` (98 BPM).
- **Formatos Diversos:** Se confirmó estabilidad en FLAC, WAV, OGG, AIFF y M4A.

### 🔴 Caso de Fallo (FAIL)
- **Archivo:** `audio11 bpm 82.mp3`
- **Esperado:** 82.0 BPM
- **Detectado:** 83.5 BPM
- **Análisis:** El error es de +1.5 BPM. Esto se debe a la resolución actual del motor de autocorrelación (pasos de 0.5 BPM). El algoritmo "rescató" este archivo mediante cross-validation espectral, pero no logró entrar en el margen de tolerancia de ±1.0 BPM.

### 🟡 Observaciones Técnicas (ALT_MATCH)
- **audio12 bpm 98.mp3:** Detectado como 65.0 BPM (Primary). El alternativo 97.5 BPM rescató el match. El motor primario se enganchó en un sub-armónico (ratio ~0.66).

---

## 3. Notas para el Especialista
1.  **Resolución de Autocorrelación:** Se observa que el límite de 0.5 BPM impide alcanzar precisión exacta en archivos con fluctuaciones sutiles o tempos no enteros. Se recomienda evaluar el incremento de la resolución a 0.1 BPM.
2.  **Pérdida de Energía en Transientes:** En los casos de `ALT_MATCH`, el motor de `TransientGrid` suele reportar confianzas bajas, forzando al sistema a depender del `SpectralFlux`.
3.  **Seguridad de Tresillo:** El ratio 1.5x es vital. Sin él, la tasa de éxito caería al 70%.

---

## 4. Conclusión
El módulo es altamente fiable para producción (95% de éxito), pero presenta una debilidad sistemática en la resolución fina de la autocorrelación, lo que genera errores de ±1.5 BPM en casos límite como la balada de prueba.
