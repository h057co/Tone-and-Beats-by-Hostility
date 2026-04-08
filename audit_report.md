# 🔍 Auditoría Completa — Tone & Beats by Hostility v1.0.0

**Proyecto:** Tone & Beats by Hostility  
**Framework:** .NET 8.0 + WPF  
**Fecha de auditoría:** 7 de Abril de 2026  
**Archivos auditados:** ~35 archivos fuente (C#, XAML, configuración)

---

## Resumen Ejecutivo

El proyecto es una aplicación WPF funcional y bien estructurada para detección de BPM y tonalidad musical. Sigue el patrón MVVM con inyección de dependencias manual, interfaces bien definidas y separación de responsabilidades. Sin embargo, se identificaron **bugs activos, riesgos legales, problemas de rendimiento, código muerto y varias áreas de mejora**.

### Puntuación General

| Categoría | Puntuación | Nivel |
|-----------|:---------:|-------|
| Arquitectura | 7/10 | 🟢 Buena |
| Calidad de Código | 5/10 | 🟡 Regular |
| Bugs & Errores | 4/10 | 🔴 Necesita atención |
| Rendimiento | 5/10 | 🟡 Regular |
| Licencias/Legal | 3/10 | 🔴 Riesgo alto |
| Mantenibilidad | 6/10 | 🟡 Aceptable |
| Documentación | 8/10 | 🟢 Buena |

---

## 🔴 1. BUGS CRÍTICOS

### 1.1 `BrowseCommand` nunca se inicializa

> [!CAUTION]
> El `BrowseCommand` se declara como propiedad pública pero **nunca se asigna** en el constructor de `MainViewModel`. Esto causará un `NullReferenceException` al hacer click en "Browse".

```csharp
// MainViewModel.cs:320 — Declarada pero nunca inicializada
public ICommand BrowseCommand { get; }
```

El constructor (líneas 61-78) asigna todos los servicios, pero **no inicializa `BrowseCommand`** ni `AnalyzeCommand`.

```csharp
// Línea 336 — Mismo problema
public ICommand AnalyzeCommand { get; }
```

**Corrección requerida:** Inicializar en el constructor:
```csharp
BrowseCommand = new RelayCommand(ExecuteBrowse);
AnalyzeCommand = new RelayCommand(ExecuteAnalyze, () => IsAnalyzeButtonEnabled);
```

---

### 1.2 Bug en autocorrelación de BPM — `count` nunca se incrementa

> [!CAUTION]
> En [WaveformAnalyzer.cs:459-505](file:///o:/Test/BPM%20KEY/Services/WaveformAnalyzer.cs#L459-L505), el método `EstimateBpmWithCandidates` tiene un bug donde la variable `count` se declara pero **nunca se incrementa**, haciendo que `acf[lag]` siempre sea 0.

```csharp
// WaveformAnalyzer.cs:472-478
for (int lag = minLag; lag <= maxLag && lag < onsetStrength.Length; lag++)
{
    double sum = 0;
    int count = 0;  // ← Se declara
    for (int i = 0; i < onsetStrength.Length - lag; i++)
        sum += onsetStrength[i] * onsetStrength[i + lag];
    if (count > 0) acf[lag] = sum / count;  // ← count siempre es 0, NUNCA se ejecuta
}
```

**Impacto:** El análisis avanzado de BPM **siempre retorna 0**, inutilizando completamente todo el sistema avanzado de detección (spectral flux, energy flux, complex domain, weighted voting). Los ~400 líneas de código del sistema avanzado son **código muerto** por este bug.

**Corrección:**
```csharp
for (int i = 0; i < onsetStrength.Length - lag; i++)
{
    sum += onsetStrength[i] * onsetStrength[i + lag];
    count++;  // ← Falta esta línea
}
```

---

### 1.3 Error de sintaxis en `UpdateTimeline` del WaveformControl

> [!WARNING]
> En [WaveformControl.xaml.cs:227-228](file:///o:/Test/BPM%20KEY/Controls/WaveformControl.xaml.cs#L227-L228), hay un `catch` mal indentado que rompe la estructura del `try`:

```csharp
// Líneas 227-231 — catch fuera del try
        }
            catch (System.Exception)   // ← Indentación incorrecta, posible error de compilación
        {
            // Silently handle
        }
```

Esto puede causar un error de compilación dependiendo de cómo C# interprete la estructura. El `catch` debería estar al mismo nivel que el `try`.

---

### 1.4 `UpdateTimeline` — formato incorrecto de tiempo

```csharp
// WaveformControl.xaml.cs:220
TimeEndLabel.Text = $"{(int)_duration / 60}:{(_duration % 60):D2}";
```

`_duration` es `double`, y el format specifier `:D2` (decimal) solo funciona con enteros. Esto lanzará una `FormatException` en runtime. Debería ser:

```csharp
TimeEndLabel.Text = $"{(int)(_duration / 60)}:{(int)(_duration % 60):D2}";
```

---

### 1.5 `MetadataWriter` sobrescribe el campo `Performers`

> [!WARNING]
> En [MetadataWriter.cs:37](file:///o:/Test/BPM%20KEY/Services/MetadataWriter.cs#L37), el escritor de metadata **reemplaza los artistas** del archivo con el nombre de la aplicación:

```csharp
file.Tag.Performers = new[] { "Tone And Beat's by Hostility" };
```

Esto **destruye la metadata original del artista** del archivo de audio. Un analizador no debería modificar campos no relacionados con BPM/Key.

---

## 🟡 2. PROBLEMAS DE RENDIMIENTO

### 2.1 Archivo de audio leído múltiples veces

El archivo de audio se lee completamente en memoria **3 veces** durante un análisis:

| Lectura | Archivo | Método |
|---------|---------|--------|
| 1ª | BpmDetector.cs:55-67 | `GetAdvancedBpm()` |
| 2ª | KeyDetector.cs:21-35 | `DetectKey()` |
| 3ª (×2) | WaveformAnalyzer.cs:43-58 + 24-25 | `LoadAudioFile()` + `Analyze()` |

El `WaveformAnalyzer.Analyze()` además **abre el archivo dos veces**: una en `LoadAudioFile()` (línea 21) y otra vez para obtener el sample rate (líneas 24-25).

**Impacto:** Para un archivo de 50MB, esto consume ~200MB+ de RAM innecesariamente y duplica I/O.

**Recomendación:** Crear un servicio compartido `AudioSampleProvider` que cargue los samples una sola vez y los comparta entre los tres analizadores.

---

### 2.2 Análisis secuencial, no paralelo

> [!IMPORTANT]
> A pesar de que la documentación indica "Análisis paralelo (BPM + Key + Waveform)", el método `ExecuteAnalyze()` ejecuta todo **secuencialmente**:

```csharp
// MainViewModel.cs:507-582 — TODO ES SECUENCIAL
var bpm = await _bpmDetectorService.DetectBpmAsync(FilePath);       // 1. Espera BPM
var (key, mode, confidence) = await _keyDetectorService.DetectKeyAsync(FilePath);  // 2. Espera Key
WaveformData = await _waveformAnalyzerService.AnalyzeAsync(FilePath, ...);         // 3. Espera Waveform
```

**Corrección para análisis paralelo real:**
```csharp
var bpmTask = _bpmDetectorService.DetectBpmAsync(FilePath);
var keyTask = _keyDetectorService.DetectKeyAsync(FilePath);
var waveformTask = _waveformAnalyzerService.AnalyzeAsync(FilePath, null);

await Task.WhenAll(bpmTask, keyTask, waveformTask);

var bpm = bpmTask.Result;
var (key, mode, confidence) = keyTask.Result;
WaveformData = waveformTask.Result;
```

---

### 2.3 `BpmDetector` llama `.GetAwaiter().GetResult()` dentro de `Task.Run`

```csharp
// BpmDetector.cs:30
var bpmFinderResult = BpmAnalyzer.AnalyzeFileAsync(filePath, options).GetAwaiter().GetResult();
```

Esto bloquea el thread del threadpool. Debería usarse `await` directamente:
```csharp
var bpmFinderResult = await BpmAnalyzer.AnalyzeFileAsync(filePath, options);
```

---

### 2.4 Uso excesivo de `List<float>` con `.Add()` para cargar samples

Tres servicios usan el mismo patrón ineficiente:
```csharp
var samples = new List<float>();
// ... loop con .Add()
return samples.ToArray();
```

Deberían pre-asignar la capacidad: `new List<float>(estimatedSize)` o usar directamente un array.

---

### 2.5 Logger sincrónico escribiendo a disco en cada llamada

[LoggerService.cs](file:///o:/Test/BPM%20KEY/Services/LoggerService.cs) usa `File.AppendAllText()` sincrónicamente en cada log. Esto puede causar contención de I/O durante análisis.

**Recomendación:** Usar un buffer o cola asíncrona.

---

## 🔴 3. RIESGO LEGAL / LICENCIAS

### 3.1 GPL 2.0 — libKeyFinder.NET (RIESGO ALTO)

> [!CAUTION]
> `libKeyFinder.NET` usa licencia **GPL 2.0**, que es **copyleft fuerte**. Esto significa que:
> 
> - Si distribuyes un binario que enlaza con esta librería, **todo tu código fuente debe ser distribuido bajo GPL 2.0**
> - Tu licencia actual ("Todos los derechos reservados") es **incompatible** con GPL 2.0
> - La nota en LICENSES.md que dice "Uso comercial ✅ Permitido" es **engañosa** — GPL 2.0 permite uso comercial pero **requiere liberar todo el código fuente**

**Sin embargo**, reviso el código y veo que `KeyDetector.cs` implementa su propia FFT y algoritmo Krumhansl-Schmuckler **sin importar** ningún namespace de libKeyFinder.NET. El paquete NuGet está en el `.csproj` pero posiblemente **no se usa realmente**.

**Acción requerida:** 
1. Verificar si `libKeyFinder.NET` se usa realmente en runtime
2. Si no se usa: **eliminar la referencia del `.csproj`**
3. Si se usa: Cambiar la licencia del proyecto a GPL 2.0 o reemplazar la librería

### 3.2 LGPL 2.1 — TagLibSharp

TagLibSharp usa LGPL 2.1. Al ser self-contained y linked estáticamente, **podrías estar obligado a proporcionar** la posibilidad de reemplazar la librería. La distribución como self-contained podría violar LGPL si no se permite al usuario reemplazar la DLL de TagLibSharp.

### 3.3 LiveChartsCore — No se usa pero está referenciada

`LiveChartsCore.SkiaSharpView.WPF` está en el `.csproj` pero **no se importa en ningún archivo de código**. Esto añade ~20MB+ al build sin beneficio.

---

## 🟡 4. PROBLEMAS ESTRUCTURALES

### 4.1 Archivos duplicados: raíz vs `src/`

> [!WARNING]
> El proyecto tiene **dos copias divergentes** de los archivos fuente:
> 
> | Archivo | Raíz (`o:\Test\BPM KEY\`) | `src/` |
> |---------|:---:|:---:|
> | AudioAnalyzer.csproj | v1.0.0-beta, sin SelfContained | v1.0.0, con SelfContained |
> | AssemblyInfo.cs | "AudioAnalyzer" / "Copyright 2026" | "Tone & Beats" / "Hostility Music" |
> | MainWindow.xaml | 700×800, 8 rows, sin CornerResize | 400×760, 9 rows, con CornerResize |
> | AboutWindow.xaml | Sin copyright/email | Con copyright y email |

Los archivos en la raíz son versiones **obsoletas** (beta). El código real de release está en `src/`. Esto crea confusión y riesgo de compilar la versión incorrecta.

**Recomendación:** Eliminar los archivos duplicados de la raíz o mover todo a `src/` de forma definitiva.

### 4.2 `CornerResizeBehavior` referenciado pero no existe

El `src/MainWindow.xaml` usa:
```xml
infra:CornerResizeBehavior.EnableOnlyCornerResize="True"
```

Pero no existe ningún archivo `CornerResizeBehavior.cs` en la carpeta `Infrastructure/`. Esto causará un error de compilación en la versión `src/`.

### 4.3 `AnalysisResult` model nunca se usa

[AnalysisResult.cs](file:///o:/Test/BPM%20KEY/Models/AnalysisResult.cs) define un modelo que **no se referencia en ningún otro archivo** del proyecto. Es código muerto.

### 4.4 Interfaz `IWaveformAnalyzerService` importa `AudioAnalyzer.Services`

```csharp
// IWaveformAnalyzerService.cs:2
using AudioAnalyzer.Services;
```

Una interfaz no debería depender de su implementación. Este import es innecesario (no se usa ningún tipo de `Services`).

---

## 🟡 5. CALIDAD DE CÓDIGO

### 5.1 Catch vacíos — excepciones silenciadas

El proyecto tiene **13+ catch vacíos** que ocultan errores:

| Ubicación | Contexto |
|-----------|----------|
| MainWindow.xaml.cs:68 | Logo fail to load |
| MainWindow.xaml.cs:84 | Hyperlink navigation |
| MainViewModel.cs:389 | Audio stop durante análisis |
| MainViewModel.cs:395 | Audio unload |
| BpmDetector.cs:46 | Detección BPM completa |
| BpmDetector.cs:72 | BPM avanzado |
| KeyDetector.cs:53 | Detección Key completa |
| LoggerService.cs:23-25 | Escritura de log |
| LoggerService.cs:35-37 | Borrado de log |
| ThemeManager.cs:68-71 | Style updates |
| WaveformControl.xaml.cs:137 | Rendering |
| WaveformControl.xaml.cs:203 | Playhead update |
| WaveformControl.xaml.cs:228 | Timeline update |

**Recomendación:** Al mínimo, loggear el error antes de descartarlo:
```csharp
catch (Exception ex)
{
    LoggerService.Log($"Error: {ex.Message}");
}
```

### 5.2 Hardcoded `SolidColorBrush` en ViewModel

> [!WARNING]
> `MainViewModel` crea instancias de `SolidColorBrush` directamente (líneas 25, 34, 399, 409, 479, 690, 696, 702, 708, 720), violando MVVM al acoplar el ViewModel con System.Windows.Media.

```csharp
FileNameForeground = new SolidColorBrush(Color.FromRgb(136, 136, 136));
StatusForeground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
```

El ViewModel no debería conocer colores de UI. Esto debería manejarse con converters o estilos en XAML.

### 5.3 Row 2 hardcoded a `#252525`

```xml
<!-- MainWindow.xaml:151 -->
<Border Grid.Row="2" Background="#252525" ...>
```

No usa `DynamicResource`, así que este fondo **no cambia con el tema**. En tema Light o Blue se verá incongruente.

### 5.4 Mezcla de idiomas

El código mezcla inglés y español sin criterio:
- Métodos en inglés: `ExecutePlay()`, `HandleDrop()`
- Mensajes en español: "Análisis en proceso", "Archivo en cola"
- Tooltips en español: "Click para ajustar"
- StatusText a veces en inglés: "File loaded. Ready to analyze."
- A veces en español: "Análisis en proceso. Archivo en cola."

**Recomendación:** Estandarizar o usar un sistema de localización (`.resx`).

### 5.5 MediaInfo.Wrapper.Core no incluido en About Window

[AboutWindow.xaml](file:///o:/Test/BPM%20KEY/AboutWindow.xaml) lista 5 librerías, pero falta **MediaInfo.Wrapper.Core** (BSD-2-Clause) que sí se usa en `AudioPlayerService.cs`.

---

## 🟡 6. INCONSISTENCIAS EN DOCUMENTACIÓN

### 6.1 Discrepancias con el código real

| Documentación dice | Código real |
|-------------------|------------|
| "Análisis paralelo con Task.WhenAll" | Análisis es secuencial (`await` uno tras otro) |
| "Ventana fija 400×760px" | Raíz tiene 700×800, src tiene 400×760 |
| "MinWidth=450, MaxWidth=400" | Contradicción: MinWidth > MaxWidth |
| "Redimensionado solo desde esquinas" | Solo en `src/`, raíz permite resize normal |
| "Detección de BPM híbrida" | El avanzado siempre retorna 0 por el bug de `count` |
| "Row 4 para Status" | En raíz, Status está en Row 7 (footer) |

### 6.2 Versión inconsistente

| Ubicación | Versión |
|-----------|---------|
| `src/AudioAnalyzer.csproj` | 1.0.0 |
| Raíz `AudioAnalyzer.csproj` | 1.0.0-beta |
| Raíz `AssemblyInfo.cs` | 1.0.0.0 (pero Company = "AudioAnalyzer") |
| `src/AssemblyInfo.cs` | 1.0.0.0 (Company = "Hostility Music") |
| `src/AboutWindow.xaml` | "Versión 1.0.0" |
| Raíz `AboutWindow.xaml` | "Versión 1.0.3" ← **¿de dónde sale 1.0.3?** |

---

## 🟢 7. ASPECTOS POSITIVOS

- ✅ **Buena separación MVVM** con interfaces y servicios inyectados
- ✅ **Sistema de temas** bien implementado con ResourceDictionary dinámicos
- ✅ **Manejo de drag & drop** con validación de extensiones
- ✅ **Queue de archivos** durante análisis (previene crashes por cargas simultáneas)
- ✅ **Dispose pattern** correctamente implementado en `AudioPlayerService`
- ✅ **Documentación exhaustiva** (DOCUMENTACION.md, LICENSES.md, LICENSE.txt)
- ✅ **Verificación post-escritura** en MetadataWriter
- ✅ **Playhead con drag interactivo** en WaveformControl
- ✅ **ViewModelBase** bien implementada con `SetProperty<T>` genérico

---

## 📋 8. PLAN DE ACCIÓN PRIORITIZADO

### 🔴 Prioridad ALTA (Bugs/Riesgos)

| # | Acción | Archivo |
|---|--------|---------|
| 1 | Inicializar `BrowseCommand` y `AnalyzeCommand` | MainViewModel.cs |
| 2 | Arreglar bug `count` en autocorrelación | WaveformAnalyzer.cs:478 |
| 3 | Arreglar formato de tiempo (`:D2` en double) | WaveformControl.xaml.cs:220 |
| 4 | Arreglar catch mal indentado | WaveformControl.xaml.cs:228 |
| 5 | Eliminar asignación de `Performers` | MetadataWriter.cs:37 |
| 6 | Resolver riesgo GPL de libKeyFinder.NET | AudioAnalyzer.csproj |
| 7 | Eliminar archivos duplicados raíz vs src | Estructura proyecto |

### 🟡 Prioridad MEDIA (Rendimiento/Calidad)

| # | Acción | Archivo |
|---|--------|---------|
| 8 | Implementar análisis paralelo real con `Task.WhenAll` | MainViewModel.cs |
| 9 | Evitar lectura múltiple del archivo de audio | Servicios |
| 10 | Eliminar `LiveChartsCore` si no se usa | .csproj |
| 11 | Mover colores hardcoded del ViewModel al XAML | MainViewModel.cs |
| 12 | Usar `DynamicResource` en Row 2 | MainWindow.xaml:151 |
| 13 | Agregar MediaInfo a la ventana About | AboutWindow.xaml |

### 🟢 Prioridad BAJA (Mejoras)

| # | Acción |
|---|--------|
| 14 | Estandarizar idioma (español o inglés) |
| 15 | Agregar logging a catch vacíos |
| 16 | Eliminar `AnalysisResult.cs` (no usado) |
| 17 | Eliminar `ThemeSelector` style (no usado) |
| 18 | Consistencia de versión en todos los archivos |
| 19 | Pre-asignar capacidad en `List<float>` |

---

*Auditoría realizada por análisis estático de código. Se recomienda testing funcional complementario con archivos de audio de diferentes formatos y tamaños.*
