# 🔍 Reporte de Auditoría Estática de Software — Tone & Beats

**Fecha de auditoría:** 8 de Abril de 2026  
**Analista:** Auditor de Software Senior (Antigravity)  
**Alcance:** Base de código WPF (.NET 8)

El siguiente documento detalla los hallazgos de la auditoría técnica estática realizada sobre la base de código. Se evaluaron los 5 pilares solicitados, priorizando por niveles de severidad.

---

## 1. Rendimiento (Performance)

### 🔴 CRÍTICO: Cuello de botella de E/S y Pico de Memoria por Lecturas Paralelas Redundantes
**Justificación:** Ciertos servicios como `BpmDetector`, `KeyDetector`, `WaveformAnalyzer` y `LoudnessAnalyzer` han sido paralelizados exitosamente vía `Task.WhenAll`. Sin embargo, cada servicio ejecuta `AudioReaderFactory.CreateReader(filePath)` individualmente. Esto provoca que el **mismo archivo se abra y decodifique en memoria en 4 hilos distintos simultáneamente**, consumiendo 4 veces más memoria de la necesaria e introduciendo contención severa de disco (I/O). En archivos de gran tamaño (RAW, WAV de larga duración), la aplicación corre un alto riesgo de sufrir un pico masivo de RAM (Out Of Memory) o degradación enorme de rendimiento.

**Solución sugerida:**
Centralizar la lectura en un solo punto, cargando los *samples* una única vez y distribuyéndolo a los servicios.

```csharp
// 1. Modificar interfaz para recibir los samples o un proveedor:
float[] audioSamples = LoadAudioFile(FilePath);
int sampleRate = GetSampleRate(FilePath);

// 2. Pasar los mismos datos en memoria a todos los analizadores paralelos:
var bpmTask = _bpmDetectorService.DetectBpmAsync(audioSamples, sampleRate);
var keyTask = _keyDetectorService.DetectKeyAsync(audioSamples, sampleRate);
var waveformTask = _waveformAnalyzerService.AnalyzeAsync(audioSamples, sampleRate);
var loudnessTask = _loudnessAnalyzerService.AnalyzeAsync(audioSamples, sampleRate);

await Task.WhenAll(bpmTask, keyTask, waveformTask, loudnessTask);
```

### 🟠 ALTO: Fuga teórica de rendimiento por expansión constante de `List<float>`
**Justificación:** En `WaveformAnalyzer.cs` (y otros decodificadores), el archivo se carga iterando sobre un buffer y usando `samples.Add(sum)`. Al ser un `List<float>` dinámico sin capacidad pre-dimensionada, el arreglo interno se reasigna y copia múltiples veces durante la lectura de millones de muestras de audio, generando un estrés altísimo al *Garbage Collector (GC)* y bloqueos esporádicos.

**Solución sugerida:**
Pre-asignar el tamaño del array o lista conociendo la longitud del _stream_.

```csharp
// WaveformAnalyzer.cs - LoadAudioFile
var totalSamples = (int)(waveStream.Length / (waveStream.WaveFormat.BitsPerSample / 8));
var expectedCapacity = totalSamples / waveStream.WaveFormat.Channels;
var samples = new List<float>(expectedCapacity); // ¡Capacidad inicial definida!
```

---

## 2. Calidad del Código y Deuda Técnica

### 🟠 ALTO: Excepciones Silenciadas (Catch vacíos)
**Justificación:** A lo largo del código (en especial `MainViewModel.cs` líneas 470-480 y `LoggerService.cs`) existen bloques `try { ... } catch { }` que tragan la excepción completamente. Un código sin _fail-fast_ o sin logging adecuado en métodos que controlan la vida útil del archivo de audio oculta errores fatales difíciles de depurar en producción.

**Solución sugerida:**
Como mínimo, documentar el error sin interrumpir el flujo.

```csharp
try
{
    _audioPlayerService.UnloadFile();
}
catch (Exception ex)
{
    // Silencioso para la UI, pero visible para los desarrolladores
    System.Diagnostics.Debug.WriteLine($"Failed to unload: {ex.Message}");
    LoggerService.Log($"Warning: AudioPlayer Unload failed - {ex.Message}");
}
```

### 🟡 MEDIO: Violación del principio SoC (Separación de Responsabilidades) en el ViewModel
**Justificación:** En `MainViewModel.cs`, se instancian colores estrictamente atados a WPF y su presentación gráfica (e.g. `FileNameForeground = new SolidColorBrush(Color.FromRgb(136, 136, 136));`). El *ViewModel* no debe acoplarse con librerías de UI gráficas. Esto imposibilita el testing unitario multiplataforma sin depender de librerías de interfaz gráfica, rompiendo la filosofía MVVM.

**Solución sugerida:** 
Exponer propiedades semánticas (`bool IsFileLoaded`, `enum FeedbackState`) en el *ViewModel* y delegar los colores al `View` a través de desencadenadores de estilo (DataTriggers) en XAML.

```xml
<!-- En MainWindow.xaml -->
<DataTrigger Binding="{Binding IsFileSelected}" Value="True">
    <Setter Property="Foreground" Value="#E0E0E0" />
</DataTrigger>
```

---

## 3. Seguridad

### 🟡 MEDIO: Falta de *Thread-Safety* en sistema de Logging (`LoggerService.cs`)
**Justificación:** El servicio implementa un método estático sincrónico `Log(string message)` que utiliza `File.AppendAllText` para escribir la línea en el disco. En la nueva arquitectura de análisis concurrente (con `Task.WhenAll`), si los detectores terminan y emiten mensajes en el mismo instante, podría generarse una excepción de tipo `IOException` (Archivo en Uso), perdiendo los diagnósticos o deteniendo una ejecución importante.

**Solución sugerida:**
Añadir un bloqueo sincrónico u optar por un Logger Concurrente robusto.

```csharp
public static class LoggerService
{
    private static readonly object _lock = new object();

    public static void Log(string message)
    {
        lock (_lock)
        {
            try {
                // ... Código File.AppendAllText
            } catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }
    }
}
```

---

## 4. Arquitectura y Estructura

### 🟡 MEDIO: Acoplamiento a lógica de negocio dentro de `MainViewModel`
**Justificación:** El método `ExecuteAnalyze` de `MainViewModel` consta de más de 100 líneas, donde no sólamente orquesta la presentación y estados mutables (como barras de progreso y textos) sino que encapsula la propia "receta" del análisis (cuándo llamar al Bpm, luego reconstruir Waveform con el Bpm y evaluar la Loudness).
El control de la lógica de flujo del proceso analítico debería pertenecer a un Servicio Agregador de dominio.

**Solución sugerida:** 
Crear un orquestador, por ejemplo `IAudioAnalysisPipeline`, que retorne un objeto `AnalysisReport`.

```csharp
// MainViewModel.cs - Queda muy limpio y abstracto
var report = await _pipeline.AnalyzeAudioAsync(FilePath);

BpmText = report.Bpm.ToString("F1");
KeyText = report.Key;
LoudnessResult = report.Loudness;
```

---

## 5. Estado de Dependencias

### 🟡 MEDIO: `BpmFinder` en versión Alpha/Beta (`0.1.0`)
**Justificación:** El archivo `AudioAnalyzer.csproj` evidencia el uso de un paquete `BpmFinder` versión `0.1.0`. Por ciclo semántico, las versiones `< 1.0.0` no certifican ser estables ni garantizan compatibilidad en actualizaciones futuras. Esto representa un leve riesgo de mantenimiento temprano.

### 🔵 BAJO: Evaluación del peso del `FFMpegCore`
**Justificación:** Depende de un binario en la ruta `publish\ffmpeg\ffmpeg.exe`. Aunque robusto, si la aplicación local solo necesita decodificar audio de formatos populares a memoria PCM, NAudio ya maneja gran parte a través del Media Foundation de Windows. Se podría explorar quitar librerías dependientes ajenas para aligerar la magnitud del binario portable en el largo plazo.

---
**Conclusión:** 
Tras previas refactorizaciones de licencias y bugs masivos detectados anteriormente, el software ha mejorado bastante su integridad. Las verdaderas alertas rojas actualmente recaen puramente en la **eficiencia del flujo de datos en memoria (lecturas en bucle por cada hilo y conversiones de Arrays)** y el pulido de las responsabilidades arquitectónicas (SoC en la UI/ViewModel).
