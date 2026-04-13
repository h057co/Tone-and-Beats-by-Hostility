using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using AudioAnalyzer.Services;

Console.WriteLine("╔════════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║          BPM DETECTION TEST - FASE 1 BASELINE (20 archivos de test)           ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝\n");

// Archivos de test con BPM esperado extraído del nombre
var testFiles = new (string path, double expectedBpm, string description)[]
{
    ("..\\Assets\\audiotest\\audio 17 bpm 90.mp3", 90.0, "Hip-hop 1"),
    ("..\\Assets\\audiotest\\audio1 bpm 98,256 .mp3", 98.0, "Reggaeton 1"),
    ("..\\Assets\\audiotest\\audio10 bpm 112 .mp3", 112.0, "Dance 1"),
    ("..\\Assets\\audiotest\\audio11 bpm 82.mp3", 82.0, "Ballad"),
    ("..\\Assets\\audiotest\\audio12 bpm 98.mp3", 98.0, "Reggaeton 2"),
    ("..\\Assets\\audiotest\\audio13 bpm 102.mp3", 102.0, "Pop 1"),
    ("..\\Assets\\audiotest\\audio14 bpm 128.mp3", 128.0, "EDM 1"),
    ("..\\Assets\\audiotest\\audio15 bpm 130.mp3", 130.0, "EDM 2"),
    ("..\\Assets\\audiotest\\audio16 bpm 100.mp3", 100.0, "Pop 2"),
    ("..\\Assets\\audiotest\\audio2 bpm 90.flac", 90.0, "Hip-hop 2 (FLAC)"),
    ("..\\Assets\\audiotest\\audio4 bpm 79.wav", 79.0, "Slow (WAV)"),
    ("..\\Assets\\audiotest\\audio5 bpm 76,665.m4a", 76.665, "Slow Decimal (M4A)"),
    ("..\\Assets\\audiotest\\audio6 bpm 74.ogg", 74.0, "Slow (OGG)"),
    ("..\\Assets\\audiotest\\audio8 bpm 90.aiff", 90.0, "Hip-hop 3 (AIFF)"),
    ("..\\Assets\\audiotest\\audio9 bpm 110.mp3", 110.0, "Dance 2"),
    ("..\\Assets\\audiotest\\master bpm 152.mp3", 152.0, "Trap (master MP3)"),
    ("..\\Assets\\audiotest\\master bpm 152.wav", 152.0, "Trap (master WAV)"),
    ("..\\Assets\\audiotest\\sin master bpm 152.mp3", 152.0, "Trap (sin master MP3)"),
    ("..\\Assets\\audiotest\\sin master bpm 152.wav", 152.0, "Trap (sin master WAV)"),
    ("..\\Assets\\audiotest\\Ta Buena Rancha bpm 108.mp3", 108.0, "Reggaeton 3"),
};

var results = new List<TestResult>();
var bpmDetector = new BpmDetector();

Console.WriteLine($"Total de archivos de test: {testFiles.Length}\n");

// Ejecutar detección para cada archivo
for (int i = 0; i < testFiles.Length; i++)
{
    var (path, expectedBpm, description) = testFiles[i];
    var fullPath = Path.Combine(Directory.GetCurrentDirectory(), path);

    if (!File.Exists(fullPath))
    {
        Console.Write($"[{i + 1,2}/{testFiles.Length}] SKIP - File not found: {Path.GetFileName(path)}\n");
        continue;
    }

    try
    {
        Console.Write($"[{i + 1,2}/{testFiles.Length}] {Path.GetFileName(path)} (esperado: {expectedBpm} BPM)... ");

        var (primaryBpm, _) = bpmDetector.DetectBpm(fullPath);

        // Calcular Alt BPM con lógica de tresillo (x1.5 y x0.667)
        double altBpm = CalculateAlternativeTresilloBpm(primaryBpm);

        // Aplicar snap a integer si la diferencia es < 0.3
        primaryBpm = SnapToInteger(primaryBpm);
        altBpm = SnapToInteger(altBpm);

        // Verificar match con tolerancia ±1 BPM
        string status = DeterminateStatus(primaryBpm, altBpm, expectedBpm);

        results.Add(new TestResult
        {
            FileName = Path.GetFileName(path),
            Expected = expectedBpm,
            Primary = primaryBpm,
            Alternative = altBpm,
            Status = status,
            Description = description
        });

        Console.WriteLine($"✓ Primary: {primaryBpm:F1} | Alt: {altBpm:F1} | [{status}]");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR - {ex.Message}");
        results.Add(new TestResult
        {
            FileName = Path.GetFileName(path),
            Expected = expectedBpm,
            Primary = 0,
            Alternative = 0,
            Status = "ERROR",
            Description = description
        });
    }
}

// === TABLA DE RESULTADOS ===
Console.WriteLine("\n╔════════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                        TABLA DE RESULTADOS                                    ║");
Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");
Console.WriteLine("║ Archivo                              │ Esperado │ Primary │ Alt      │ Status  ║");
Console.WriteLine("╠════════════════════════════════════════════════════════════════════════════════╣");

foreach (var result in results)
{
    string fileName = result.FileName.Length > 34 ? result.FileName.Substring(0, 31) + "..." : result.FileName.PadRight(34);
    Console.WriteLine($"║ {fileName} │ {result.Expected,7:F1} │ {result.Primary,7:F1} │ {result.Alternative,8:F1} │ {result.Status,7} ║");
}

Console.WriteLine("╚════════════════════════════════════════════════════════════════════════════════╝");

// === RESUMEN ESTADÍSTICO ===
int totalTests = results.Count;
int matches = results.Count(r => r.Status == "MATCH");
int altMatches = results.Count(r => r.Status == "ALT_MATCH");
int fails = results.Count(r => r.Status == "FAIL");
int errors = results.Count(r => r.Status == "ERROR");

double successRate = totalTests > 0 ? (matches + altMatches) * 100.0 / totalTests : 0;

Console.WriteLine($"\n╔════════════════════════════════════════════════════════════════════════════════╗");
Console.WriteLine($"║                           RESUMEN ESTADÍSTICO                                 ║");
Console.WriteLine($"╠════════════════════════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║ Total de tests:                 {totalTests,4}                                        ║");
Console.WriteLine($"║ MATCH (Primary correcto):       {matches,4}  ({matches * 100.0 / totalTests,5:F1}%)                          ║");
Console.WriteLine($"║ ALT_MATCH (Alternativo correcto): {altMatches,4}  ({altMatches * 100.0 / totalTests,5:F1}%)                          ║");
Console.WriteLine($"║ FAIL (Ninguno coincide):        {fails,4}  ({fails * 100.0 / totalTests,5:F1}%)                          ║");
Console.WriteLine($"║ ERROR (Excepción):              {errors,4}  ({errors * 100.0 / totalTests,5:F1}%)                          ║");
Console.WriteLine($"╠════════════════════════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║ ✓ TASA DE ÉXITO TOTAL:          {successRate,5:F1}%                                      ║");
Console.WriteLine($"╚════════════════════════════════════════════════════════════════════════════════╝");

Console.WriteLine($"\n📊 Logs disponibles en: %LOCALAPPDATA%\\ToneAndBeats\\app.log");

// === HELPER FUNCTIONS ===

static double SnapToInteger(double bpm)
{
    double rounded = Math.Round(bpm);
    if (Math.Abs(bpm - rounded) < 0.3)
        return rounded;
    return Math.Round(bpm, 1);
}

static double CalculateAlternativeTresilloBpm(double primaryBpm)
{
    if (primaryBpm <= 0) return 0;

    // Candidatos en orden de preferencia: tresillo primero, luego double/half
    double[] candidates = {
        primaryBpm * 1.5,    // tresillo up
        primaryBpm * 0.667,  // tresillo down
        primaryBpm * 2.0,    // double-time
        primaryBpm * 0.5     // half-time
    };

    // Retornar el primer candidato en rango musical (60-200)
    foreach (var c in candidates)
    {
        if (c >= 60 && c <= 200)
            return c;
    }
    return candidates[0]; // fallback
}

static string DeterminateStatus(double primaryBpm, double altBpm, double expectedBpm)
{
    const double TOLERANCE = 1.0;

    // Tolerancia ±1 BPM
    if (Math.Abs(primaryBpm - expectedBpm) <= TOLERANCE)
        return "MATCH";

    if (Math.Abs(altBpm - expectedBpm) <= TOLERANCE)
        return "ALT_MATCH";

    return "FAIL";
}

// === DATA MODEL ===

class TestResult
{
    public string FileName { get; set; }
    public double Expected { get; set; }
    public double Primary { get; set; }
    public double Alternative { get; set; }
    public string Status { get; set; }
    public string Description { get; set; }
}
