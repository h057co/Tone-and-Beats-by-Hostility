using System.Diagnostics;
using System.IO;
using AudioAnalyzer.Services;
using AudioAnalyzer.Models;

namespace AudioAnalyzer.PerfTest;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== PERFORMANCE AUDIT - Tone & Beats ===");
        Console.WriteLine();

        string testFolder = @"..\..\..\Assets\audiotest";
        if (!Directory.Exists(testFolder))
        {
            testFolder = @"..\..\Assets\audiotest";
        }
        
        var files = Directory.GetFiles(testFolder).OrderBy(f => f).ToArray();
        
        Console.WriteLine($"Archivos encontrados: {files.Length}");
        foreach (var f in files)
        {
            var fi = new FileInfo(f);
            Console.WriteLine($"  - {Path.GetFileName(f)} ({fi.Length / 1024.0 / 1024.0:F2} MB)");
        }
        Console.WriteLine();

        var results = new List<PerformanceResult>();
        
        var initialMemory = GC.GetTotalMemory(true);
        Console.WriteLine($"Memoria inicial: {initialMemory / 1024.0 / 1024.0:F2} MB");
        
        var stopwatchTotal = Stopwatch.StartNew();
        
        var bpmDetector = new BpmDetector();
        var keyDetector = new KeyDetector();
        var loudnessAnalyzer = new LoudnessAnalyzer();
        var waveformAnalyzer = new WaveformAnalyzer();

        long peakMemory = 0;

        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];
            string fileName = Path.GetFileName(file);
            FileInfo fileInfo = new FileInfo(file);
            
            Console.WriteLine($"[{i+1}/{files.Length}] Procesando: {fileName}");
            
            var stopwatch = Stopwatch.StartNew();
            var process = Process.GetCurrentProcess();
            long memBefore = process.WorkingSet64;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
                var bpmTask = Task.Run(() => bpmDetector.DetectBpm(file), cts.Token);
                var keyTask = Task.Run(() => keyDetector.DetectKey(file), cts.Token);
                var loudnessTask = Task.Run(() => loudnessAnalyzer.Analyze(file), cts.Token);
                var waveformTask = Task.Run(() => waveformAnalyzer.Analyze(file), cts.Token);
                
                await Task.WhenAll(bpmTask, keyTask, loudnessTask, waveformTask);
                
                stopwatch.Stop();
                
                process = Process.GetCurrentProcess();
                long memAfter = process.WorkingSet64;
                
                long memoryUsed = memAfter - memBefore;
                if (memoryUsed > peakMemory) peakMemory = memoryUsed;

                var bpm = bpmTask.Result;
                var key = keyTask.Result;
                var loudness = loudnessTask.Result;

                Console.WriteLine($"  OK - Tiempo: {stopwatch.ElapsedMilliseconds} ms, BPM: {bpm:F1}, Key: {key.Key}/{key.Mode}, LUFS: {loudness.IntegratedLufs:F1}");

                results.Add(new PerformanceResult
                {
                    FileName = fileName,
                    FileSizeMB = fileInfo.Length / 1024.0 / 1024.0,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds,
                    MemoryUsedMB = memoryUsed / 1024.0 / 1024.0,
                    Success = true
                });
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                Console.WriteLine($"  TIMEOUT: Archivo tardó más de 120 segundos");
                results.Add(new PerformanceResult
                {
                    FileName = fileName,
                    FileSizeMB = fileInfo.Length / 1024.0 / 1024.0,
                    DurationMs = 120000,
                    Success = false,
                    Error = "Timeout"
                });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.WriteLine($"  ERROR: {ex.Message}");
                results.Add(new PerformanceResult
                {
                    FileName = fileName,
                    FileSizeMB = fileInfo.Length / 1024.0 / 1024.0,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds,
                    Success = false,
                    Error = ex.Message
                });
            }

            Console.WriteLine();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        stopwatchTotal.Stop();

        Console.WriteLine("===========================================");
        Console.WriteLine("        RESUMEN DE PERFORMANCE             ");
        Console.WriteLine("===========================================");
        Console.WriteLine();
        
        double throughput = files.Length / (stopwatchTotal.ElapsedMilliseconds / 1000.0 / 60.0);
        Console.WriteLine($"Archivos procesados: {results.Count(r => r.Success)}/{files.Length}");
        Console.WriteLine($"Tiempo total: {stopwatchTotal.ElapsedMilliseconds} ms ({stopwatchTotal.Elapsed.TotalSeconds:F2} s)");
        Console.WriteLine($"Throughput: {throughput:F2} archivos/minuto");
        Console.WriteLine($"Memoria pico: {peakMemory / 1024.0 / 1024.0:F2} MB");
        Console.WriteLine();

        Console.WriteLine("| # | Archivo           | Tamaño  | Tiempo  | Memoria |");
        Console.WriteLine("|---|-------------------|---------|---------|----------|");
        
        int idx = 1;
        foreach (var r in results)
        {
            string status = r.Success ? "OK" : "ERR";
            string duration = r.Success ? $"{r.DurationMs,7} ms" : r.Error ?? "ERROR";
            Console.WriteLine($"| {idx,2} | {r.FileName,-17} | {r.FileSizeMB,6:F2} MB | {duration,-7} | {r.MemoryUsedMB,6:F2} MB |");
            idx++;
        }

        var successful = results.Where(r => r.Success).ToList();
        if (successful.Any())
        {
            double avgTime = successful.Average(r => r.DurationMs);
            double totalSize = successful.Sum(r => r.FileSizeMB);
            double avgIoSpeed = totalSize / (stopwatchTotal.ElapsedMilliseconds / 1000.0);
            
            Console.WriteLine();
            Console.WriteLine($"Promedio tiempo: {avgTime:F2} ms");
            Console.WriteLine($"I/O estimada: {avgIoSpeed:F2} MB/s");
            
            bool recommendParallel = avgTime > 10000 && successful.Count > 2;
            Console.WriteLine();
            Console.WriteLine(recommendParallel 
                ? "RECOMENDACION: Implementar multithreading" 
                : "RENDIMIENTO: Aceptable para produccion");
        }
    }
}

class PerformanceResult
{
    public string FileName { get; set; } = "";
    public double FileSizeMB { get; set; }
    public long DurationMs { get; set; }
    public double MemoryUsedMB { get; set; }
    public double CpuUsedMs { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
