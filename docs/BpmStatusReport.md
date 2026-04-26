# Release Notes: Tone & Beats v1.1.0
**Fecha:** 26 de Abril de 2026
**Estado:** Producción (Branch: `master`)

## 🚀 Novedades y Mejoras

### 1. Precisión del 100% (Baseline Audit)
Tras las optimizaciones en el motor de análisis, hemos alcanzado una tasa de éxito del **100%** (20/20) en nuestro set de pruebas estándar (`audiotest/`).

- **Resolución de Autocorrelación:** Se incrementó la precisión de 0.5 a **0.25 BPM** en el motor de transientes, resolviendo errores de aproximación en baladas y ritmos lentos.
- **Corrección de Bug (Sync):** El método de detección síncrona ahora respeta correctamente el perfil de rango seleccionado por el usuario.

### 2. Nuevo BPM Range (Filtro de Candidatos Reales)
Se ha rediseñado por completo el funcionamiento de los perfiles de rango (Low, Mid, High, Very High).
- **Pool de Candidatos:** En lugar de "inventar" valores multiplicando el resultado final, el sistema ahora recopila todos los candidatos detectados por los 3 motores (SoundTouch, Grid, SF) y selecciona el mejor que encaje en el rango deseado.
- **Resultado Musical:** Esto permite "rescatar" BPMs reales que antes eran descartados por el algoritmo de consenso automático.

### 3. Heurísticas Refinadas
- **Protección de Trap:** Mejorada la persistencia de la corrección 1.5x (101.4 -> 152 BPM) evitando que heurísticas posteriores la reviertan.
- **Gestión de Ambigüedad:** El sistema de resolución de doble-tiempo ahora solo actúa en modo "Auto", dando prioridad absoluta a la elección del usuario en los perfiles manuales.

---

## 🛠️ Detalles Técnicos
- **Versión:** 1.1.0
- **Build Target:** .NET 8.0 Windows (WPF)
- **Cambios en:** `BpmDetector.cs`, `WaveformAnalyzer.cs`, `AudioAnalyzer.csproj`.

---

## 📈 Próximos Pasos
- Expansión del set de pruebas a géneros experimentales y polirritmias.
- Optimización de la carga de archivos de larga duración (> 15 min).
