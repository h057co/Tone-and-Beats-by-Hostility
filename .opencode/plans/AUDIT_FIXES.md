# AUDIT FIXES - Tone & Beats by Hostility
## Instrucciones para Haiku (Agente de Ejecucion)

**Fecha**: 2026-04-13
**Proyecto**: O:\Tone and Beats\src
**Total**: 22 patches, 15 fixes, 8 archivos afectados

---

## REGLAS DE EJECUCION

1. Ejecutar los patches en el ORDEN EXACTO listado en la seccion ORDEN DE EJECUCION al final.
2. Cada PATCH indica: archivo, operacion (REPLACE/DELETE/INSERT_AFTER), fragmento original (busqueda) y fragmento de reemplazo.
3. Para operacion REPLACE: buscar el fragmento original EXACTO y reemplazar por el nuevo.
4. Para operacion DELETE: buscar el fragmento original EXACTO y eliminarlo (reemplazo vacio).
5. Despues de aplicar TODOS los patches, ejecutar: `dotnet build AudioAnalyzer.sln --configuration Release` desde `O:\Tone and Beats\src`.
6. Si el build falla, reportar el error exacto.
7. Todos los archivos son relativos a `O:\Tone and Beats\src\`.

---

## PATCHES

---

### PATCH-01b -> FIX-01b | CRITICO
**Archivo**: `Models\LoudnessResult.cs`
**Operacion**: REPLACE
**Problema**: Falta propiedad LraDisplay dedicada para Loudness Range.

**Buscar**:
```csharp
    public string ShortTermDisplay => ShortTermLufs > 0 && ShortTermLufs < 50 ? $"{ShortTermLufs:F1}" : "--";
```

**Reemplazar por**:
```csharp
    public string ShortTermDisplay => ShortTermLufs > 0 && ShortTermLufs < 50 ? $"{ShortTermLufs:F1}" : "--";
    public string LraDisplay => ShortTermLufs > 0 && ShortTermLufs < 50 ? $"{ShortTermLufs:F1} LU" : "--";
```

---

### PATCH-01 -> FIX-01 | CRITICO
**Archivo**: `ViewModels\MainViewModel.cs`
**Operacion**: REPLACE
**Problema**: LoudnessLraDisplay retorna ShortTermDisplay en vez de LRA real.

**Buscar**:
```csharp
    public string LoudnessLraDisplay => _loudnessResult?.ShortTermDisplay ?? "--";
```

**Reemplazar por**:
```csharp
    public string LoudnessLraDisplay => _loudnessResult?.LraDisplay ?? "--";
```

---

### PATCH-02b -> FIX-02 | CRITICO
**Archivo**: `ViewModels\MainViewModel.cs`
**Operacion**: REPLACE
**Problema**: async void ExecuteAnalyze no propaga excepciones.

**Buscar**:
```csharp
    private async void ExecuteAnalyze()
```

**Reemplazar por**:
```csharp
    private async Task ExecuteAnalyzeAsync()
```

---

### PATCH-02d -> FIX-02 | CRITICO
**Archivo**: `ViewModels\MainViewModel.cs`
**Operacion**: REPLACE
**Problema**: Llamada recursiva interna debe usar el nuevo nombre.

**Buscar**:
```csharp
                LoadAudioFile(nextFile);
                ExecuteAnalyze();
```

**Reemplazar por**:
```csharp
                LoadAudioFile(nextFile);
                await ExecuteAnalyzeAsync();
```

---

### PATCH-02c -> FIX-02 | CRITICO
**Archivo**: `ViewModels\MainViewModel.cs`
**Operacion**: REPLACE
**Problema**: Metodo publico debe envolver async Task con try/catch.

**Buscar**:
```csharp
    public void ExecuteAnalyzeCommand()
    {
        ExecuteAnalyze();
    }
```

**Reemplazar por**:
```csharp
    public async void ExecuteAnalyzeCommand()
    {
        try { await ExecuteAnalyzeAsync(); }
        catch (Exception ex) { LoggerService.Log($"ExecuteAnalyzeCommand - Unhandled: {ex.Message}"); }
    }
```

---

### PATCH-02 -> FIX-02 | CRITICO
**Archivo**: `ViewModels\MainViewModel.cs`
**Operacion**: REPLACE
**Problema**: RelayCommand lambda debe usar await.

**Buscar**:
```csharp
    public RelayCommand AnalyzeCommand 
    {
        get => _analyzeCommand ??= new RelayCommand(
            () => { if (!string.IsNullOrEmpty(FilePath)) ExecuteAnalyze(); },
            () => !string.IsNullOrEmpty(FilePath) && !_isAnalyzingInProgress);
        private set => _analyzeCommand = value;
    }
```

**Reemplazar por**:
```csharp
    public RelayCommand AnalyzeCommand 
    {
        get => _analyzeCommand ??= new RelayCommand(
            async () => { if (!string.IsNullOrEmpty(FilePath)) await ExecuteAnalyzeAsync(); },
            () => !string.IsNullOrEmpty(FilePath) && !_isAnalyzingInProgress);
        private set => _analyzeCommand = value;
    }
```

---

### PATCH-03 -> FIX-03 | ALTO
**Archivo**: `Services\LoudnessAnalyzer.cs`
**Operacion**: REPLACE
**Problema**: ReadToEnd secuencial de stderr/stdout puede deadlockear con FFmpeg.

**Buscar**:
```csharp
            using var process = Process.Start(startInfo);
            if (process == null)
                throw new Exception("No se pudo iniciar FFmpeg");

            var output = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
            process.WaitForExit(180000);
```

**Reemplazar por**:
```csharp
            using var process = Process.Start(startInfo);
            if (process == null)
                throw new Exception("No se pudo iniciar FFmpeg");

            // Leer stderr y stdout en paralelo para evitar deadlock por buffer lleno
            var stderrTask = process.StandardError.ReadToEndAsync();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            process.WaitForExit(180000);
            var output = stderrTask.Result + stdoutTask.Result;
```

---

### PATCH-04 -> FIX-04 | ALTO
**Archivo**: `Services\LoggerService.cs`
**Operacion**: REPLACE
**Problema**: File.AppendAllText abre/cierra archivo en cada llamada bajo lock.

**Buscar**:
```csharp
public static class LoggerService
{
    private static readonly object _lock = new object();

    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ToneAndBeats",
        "app.log");

    public static void Log(string message)
    {
        lock (_lock)
        {
            try
            {
                var directory = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoggerService.Log failed: {ex.Message}");
            }
        }
    }
```

**Reemplazar por**:
```csharp
public static class LoggerService
{
    private static readonly object _lock = new object();
    private static StreamWriter? _writer;

    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ToneAndBeats",
        "app.log");

    public static void Log(string message)
    {
        lock (_lock)
        {
            try
            {
                if (_writer == null)
                {
                    var directory = Path.GetDirectoryName(LogFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                        Directory.CreateDirectory(directory);
                    _writer = new StreamWriter(LogFilePath, append: true) { AutoFlush = true };
                }

                _writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoggerService.Log failed: {ex.Message}");
            }
        }
    }
```

---

### PATCH-05 -> FIX-05 | ALTO
**Archivo**: `ViewModels\MainViewModel.cs`
**Operacion**: REPLACE
**Problema**: 7x OnPropertyChanged redundantes - SetProperty ya notifica.

**Buscar**:
```csharp
                OnPropertyChanged(nameof(AudioFileType));
                OnPropertyChanged(nameof(SampleRateText));
                OnPropertyChanged(nameof(BitDepthText));
                OnPropertyChanged(nameof(ChannelsText));
                OnPropertyChanged(nameof(BitrateText));
                OnPropertyChanged(nameof(BitrateModeText));
                OnPropertyChanged(nameof(AudioInfoSummary));

                StatusText = $"Audio: {audioInfo.FileType} | {audioInfo.SampleRateDisplay} | {audioInfo.BitDepthDisplay} | {audioInfo.BitrateDisplay} | {audioInfo.BitrateModeDisplay} | {audioInfo.ChannelsDisplay}";
            }
            else
            {
                LoggerService.Log("LoadAudioFile() - audioInfo es null, asignando valores vacios");

                _currentAudioInfo = null;
                AudioFileType = "";
                SampleRateText = "";
                BitDepthText = "";
                ChannelsText = "";
                BitrateText = "";
                BitrateModeText = "";

                OnPropertyChanged(nameof(AudioFileType));
                OnPropertyChanged(nameof(SampleRateText));
                OnPropertyChanged(nameof(BitDepthText));
                OnPropertyChanged(nameof(ChannelsText));
                OnPropertyChanged(nameof(BitrateText));
                OnPropertyChanged(nameof(BitrateModeText));
                OnPropertyChanged(nameof(AudioInfoSummary));
            }
```

**Reemplazar por**:
```csharp
                // SetProperty ya notifica - solo AudioInfoSummary necesita notificacion manual (es calculated)
                OnPropertyChanged(nameof(AudioInfoSummary));

                StatusText = $"Audio: {audioInfo.FileType} | {audioInfo.SampleRateDisplay} | {audioInfo.BitDepthDisplay} | {audioInfo.BitrateDisplay} | {audioInfo.BitrateModeDisplay} | {audioInfo.ChannelsDisplay}";
            }
            else
            {
                LoggerService.Log("LoadAudioFile() - audioInfo es null, asignando valores vacios");

                _currentAudioInfo = null;
                AudioFileType = "";
                SampleRateText = "";
                BitDepthText = "";
                ChannelsText = "";
                BitrateText = "";
                BitrateModeText = "";

                OnPropertyChanged(nameof(AudioInfoSummary));
            }
```

---

### PATCH-11 -> FIX-11 | MEDIO
**Archivo**: `ViewModels\MainViewModel.cs`
**Operacion**: REPLACE
**Problema**: Dispatcher.Invoke puede deadlockear desde UI thread.

**Buscar**:
```csharp
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
```

**Reemplazar por**:
```csharp
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
```

---

### PATCH-06 -> FIX-06 | ALTO
**Archivo**: `MainWindow.xaml.cs`
**Operacion**: DELETE
**Problema**: Dead code - _aspectRatio sin uso.

**Buscar y ELIMINAR**:
```csharp
    // Proporcion exacta (Ancho / Alto)
    private readonly double _aspectRatio = 400.0 / 900.0;
```

---

### PATCH-06b -> FIX-06 | ALTO
**Archivo**: `MainWindow.xaml.cs`
**Operacion**: DELETE
**Problema**: Dead code - SourceInitialized, WINDOWPOS, WindowPosChangingHook.

**Buscar y ELIMINAR todo este bloque**:
```csharp
    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        // Hook removed - free scaling enabled
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct WINDOWPOS
    {
        public IntPtr hwnd;
        public IntPtr hwndInsertAfter;
        public int x;
        public int y;
        public int cx; // Ancho
        public int cy; // Alto
        public int flags;
    }

    private const int WM_WINDOWPOSCHANGING = 0x0046;

    private IntPtr WindowPosChangingHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // ESCALADO LIBRE - Sin restriccion de proporcion
        // Comented out to allow free resizing:
        // if (msg == WM_WINDOWPOSCHANGING)
        // {
        //     WINDOWPOS windowPos = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS))!;
        //     if (WindowState != WindowState.Maximized && (windowPos.flags & 0x0001) == 0)
        //     {
        //         windowPos.cx = (int)(windowPos.cy * _aspectRatio);
        //         Marshal.StructureToPtr(windowPos, lParam, true);
        //     }
        // }
        return IntPtr.Zero;
    }
```

---

### PATCH-06c -> FIX-06 | ALTO
**Archivo**: `MainWindow.xaml.cs`
**Operacion**: DELETE
**Problema**: Dead code - BpmText_RightClick handler vacio sin referencia XAML.

**Buscar y ELIMINAR**:
```csharp
    // Handler eliminado: la funcion de click derecho fue reemplazada
    // por el ciclo de un solo click (CycleBpmAdjustment).
    // Se mantiene el metodo vacio para compatibilidad con cualquier
    // referencia XAML residual que no haya sido actualizada.
    private void BpmText_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // No-op: ver BpmText_LeftClick para el ciclo de ajuste BPM
    }
```

---

### PATCH-06d -> FIX-06 | ALTO
**Archivo**: `MainWindow.xaml.cs`
**Operacion**: REPLACE
**Problema**: Usings huerfanos tras eliminar InteropServices y Interop code.

**Buscar**:
```csharp
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
```

**Reemplazar por**:
```csharp
using System.Windows;
using System.Windows.Controls;
```

---

### PATCH-07 -> FIX-07 | ALTO
**Archivo**: `Controls\WaveformControl.xaml.cs`
**Operacion**: REPLACE
**Problema**: Seek usa pixels fisicos (CanvasSize) vs DIPs (GetPosition). Roto en HiDPI.

**Buscar**:
```csharp
    private void HandleSeek(double x)
    {
        if (_duration <= 0 || SkiaCanvas.CanvasSize.Width <= 0) return;

        double newPosition = (x / SkiaCanvas.CanvasSize.Width) * _duration;
```

**Reemplazar por**:
```csharp
    private void HandleSeek(double x)
    {
        if (_duration <= 0 || SkiaCanvas.ActualWidth <= 0) return;

        double newPosition = (x / SkiaCanvas.ActualWidth) * _duration;
```

---

### PATCH-13 -> FIX-13 | MEDIO
**Archivo**: `Controls\WaveformControl.xaml.cs`
**Operacion**: REPLACE
**Problema**: RebuildGradient con CanvasSize.Width=0 genera shader degenerado.

**Buscar**:
```csharp
    private void RebuildGradient()
    {
        if (_waveformFillPaint == null) return;

        _waveformFillPaint.Shader = SKShader.CreateLinearGradient(
```

**Reemplazar por**:
```csharp
    private void RebuildGradient()
    {
        if (_waveformFillPaint == null) return;
        if (SkiaCanvas.CanvasSize.Width <= 0) return;

        _waveformFillPaint.Shader = SKShader.CreateLinearGradient(
```

---

### PATCH-08 -> FIX-08 | MEDIO
**Archivo**: `MainWindow.xaml`
**Operacion**: REPLACE
**Problema**: ComboBox con colores hardcodeados que rompen themes oscuros.

**Buscar**:
```xml
                <ComboBox ItemsSource="{Binding AvailableBpmProfiles}" 
                          SelectedValue="{Binding SelectedBpmProfile}" 
                          SelectedValuePath="Key"
                          Width="180" 
                          Background="White"
                          TextElement.Foreground="Black">
                    
                    <ComboBox.ItemTemplate>
                        <DataTemplate>
                            <TextBlock Text="{Binding Value}" Foreground="Black" />
                        </DataTemplate>
                    </ComboBox.ItemTemplate>
```

**Reemplazar por**:
```xml
                <ComboBox ItemsSource="{Binding AvailableBpmProfiles}" 
                          SelectedValue="{Binding SelectedBpmProfile}" 
                          SelectedValuePath="Key"
                          Width="180" 
                          Background="{DynamicResource ResultBoxBackgroundBrush}"
                          Foreground="{DynamicResource ForegroundBrush}">
                    
                    <ComboBox.ItemTemplate>
                        <DataTemplate>
                            <TextBlock Text="{Binding Value}" Foreground="{DynamicResource ForegroundBrush}" />
                        </DataTemplate>
                    </ComboBox.ItemTemplate>
```

---

### PATCH-09 -> FIX-09 | MEDIO
**Archivo**: `AboutWindow.xaml`
**Operacion**: REPLACE
**Problema**: Version hardcodeada 1.0.10 no coincide con csproj 1.0.11.

**Buscar**:
```xml
             <TextBlock Text="Version 1.0.10" FontSize="12" 
```

**Reemplazar por**:
```xml
             <TextBlock Text="Version 1.0.11" FontSize="12" 
```

---

### PATCH-10 -> FIX-10 | MEDIO
**Archivo**: `Services\MetadataWriter.cs`
**Operacion**: REPLACE
**Problema**: Key se acumula en Comment con cada guardado generando duplicados.

**Buscar**:
```csharp
            string keyComment = $"Key: {key} {mode}";
            if (string.IsNullOrEmpty(file.Tag.Comment))
            {
                file.Tag.Comment = keyComment;
            }
            else
            {
                file.Tag.Comment += $"; {keyComment}";
            }
```

**Reemplazar por**:
```csharp
            string keyComment = $"Key: {key} {mode}";
            if (string.IsNullOrEmpty(file.Tag.Comment))
            {
                file.Tag.Comment = keyComment;
            }
            else
            {
                // Reemplazar entry existente de Key en lugar de acumular duplicados
                var parts = file.Tag.Comment.Split(';')
                    .Select(p => p.Trim())
                    .Where(p => !p.StartsWith("Key:"))
                    .ToList();
                parts.Add(keyComment);
                file.Tag.Comment = string.Join("; ", parts);
            }
```

**NOTA**: Verificar que `using System.Linq;` exista al inicio del archivo. Si no existe, agregarlo.

---

### PATCH-14 -> FIX-14 | BAJO
**Archivo**: `AboutWindow.xaml`
**Operacion**: REPLACE
**Problema**: URL text parece clickeable pero no navega a ningun sitio.

**Buscar**:
```xml
            <TextBlock Text="www.hostilitymusic.com" FontSize="14" 
                       Foreground="{DynamicResource AccentBrush}" HorizontalAlignment="Center" Cursor="Hand"/>
```

**Reemplazar por**:
```xml
            <TextBlock FontSize="14" HorizontalAlignment="Center">
                <Hyperlink NavigateUri="https://www.hostilitymusic.com" 
                           RequestNavigate="Hyperlink_RequestNavigate"
                           Foreground="{DynamicResource AccentBrush}">www.hostilitymusic.com</Hyperlink>
            </TextBlock>
```

---

### PATCH-15 -> FIX-15 | BAJO
**Archivo**: `MainWindow.xaml`
**Operacion**: REPLACE
**Problema**: Boton Minimize sin nombre accesible para screen readers.

**Buscar**:
```xml
                        <!-- Minimize -->
                        <Button Content="-" Width="32" Height="24" 
                                WindowChrome.IsHitTestVisibleInChrome="True"
                                Click="Minimize_Click" 
                                FontSize="10" Cursor="Hand">
```

**Reemplazar por**:
```xml
                        <!-- Minimize -->
                        <Button Content="-" Width="32" Height="24" 
                                WindowChrome.IsHitTestVisibleInChrome="True"
                                Click="Minimize_Click" 
                                AutomationProperties.Name="Minimize"
                                FontSize="10" Cursor="Hand">
```

---

### PATCH-15b -> FIX-15 | BAJO
**Archivo**: `MainWindow.xaml`
**Operacion**: REPLACE
**Problema**: Boton Maximize sin nombre accesible para screen readers.

**Buscar**:
```xml
                        <!-- Maximize/Restore -->
                        <Button x:Name="MaximizeButton" Content="square" Width="32" Height="24" 
                                WindowChrome.IsHitTestVisibleInChrome="True"
                                Click="Maximize_Click" 
                                FontSize="10" Cursor="Hand">
```

**Reemplazar por**:
```xml
                        <!-- Maximize/Restore -->
                        <Button x:Name="MaximizeButton" Content="square" Width="32" Height="24" 
                                WindowChrome.IsHitTestVisibleInChrome="True"
                                Click="Maximize_Click" 
                                AutomationProperties.Name="Maximize"
                                FontSize="10" Cursor="Hand">
```

---

### PATCH-15c -> FIX-15 | BAJO
**Archivo**: `MainWindow.xaml`
**Operacion**: REPLACE
**Problema**: Boton Close sin nombre accesible para screen readers.

**Buscar**:
```xml
                        <!-- Close -->
                        <Button Content="x" Width="32" Height="24" 
                                WindowChrome.IsHitTestVisibleInChrome="True"
                                Click="Close_Click" 
                                FontSize="12" Cursor="Hand">
```

**Reemplazar por**:
```xml
                        <!-- Close -->
                        <Button Content="x" Width="32" Height="24" 
                                WindowChrome.IsHitTestVisibleInChrome="True"
                                Click="Close_Click" 
                                AutomationProperties.Name="Close"
                                FontSize="12" Cursor="Hand">
```

---

## ORDEN DE EJECUCION

Ejecutar en este orden exacto. Los patches marcados como paralelos pueden aplicarse en cualquier orden entre si pero ANTES del siguiente grupo.

```
GRUPO 1 - CRITICOS (LoudnessResult + ViewModel LRA)
  1. PATCH-01b  -> Models\LoudnessResult.cs
  2. PATCH-01   -> ViewModels\MainViewModel.cs

GRUPO 2 - CRITICOS (async void -> async Task)
  3. PATCH-02b  -> ViewModels\MainViewModel.cs
  4. PATCH-02d  -> ViewModels\MainViewModel.cs
  5. PATCH-02c  -> ViewModels\MainViewModel.cs
  6. PATCH-02   -> ViewModels\MainViewModel.cs

GRUPO 3 - ALTO (paralelos entre si)
  7. PATCH-03   -> Services\LoudnessAnalyzer.cs
  8. PATCH-04   -> Services\LoggerService.cs

GRUPO 4 - ALTO (ViewModel cleanup, paralelos entre si)
  9. PATCH-05   -> ViewModels\MainViewModel.cs
  10. PATCH-11  -> ViewModels\MainViewModel.cs

GRUPO 5 - ALTO (Dead code removal, secuenciales)
  11. PATCH-06  -> MainWindow.xaml.cs
  12. PATCH-06b -> MainWindow.xaml.cs
  13. PATCH-06c -> MainWindow.xaml.cs
  14. PATCH-06d -> MainWindow.xaml.cs

GRUPO 6 - ALTO (WaveformControl, paralelos entre si)
  15. PATCH-07  -> Controls\WaveformControl.xaml.cs
  16. PATCH-13  -> Controls\WaveformControl.xaml.cs

GRUPO 7 - MEDIO/BAJO (XAML + MetadataWriter, paralelos entre si)
  17. PATCH-08  -> MainWindow.xaml
  18. PATCH-09  -> AboutWindow.xaml
  19. PATCH-10  -> Services\MetadataWriter.cs
  20. PATCH-14  -> AboutWindow.xaml

GRUPO 8 - BAJO (Accessibility, secuenciales)
  21. PATCH-15  -> MainWindow.xaml
  22. PATCH-15b -> MainWindow.xaml
  23. PATCH-15c -> MainWindow.xaml
```

## VERIFICACION POST-PATCHES

Despues de aplicar todos los patches:

```bash
cd O:\Tone and Beats\src
dotnet build AudioAnalyzer.sln --configuration Release
```

**Resultado esperado**: Build succeeded. 0 Error(s).

## RESUMEN DE ARCHIVOS MODIFICADOS

| Archivo | Patches | Severidad max |
|---------|---------|---------------|
| `Models\LoudnessResult.cs` | PATCH-01b | CRITICO |
| `ViewModels\MainViewModel.cs` | PATCH-01, 02, 02b, 02c, 02d, 05, 11 | CRITICO |
| `Services\LoudnessAnalyzer.cs` | PATCH-03 | ALTO |
| `Services\LoggerService.cs` | PATCH-04 | ALTO |
| `MainWindow.xaml.cs` | PATCH-06, 06b, 06c, 06d | ALTO |
| `Controls\WaveformControl.xaml.cs` | PATCH-07, 13 | ALTO |
| `MainWindow.xaml` | PATCH-08, 15, 15b, 15c | MEDIO |
| `AboutWindow.xaml` | PATCH-09, 14 | MEDIO |
| `Services\MetadataWriter.cs` | PATCH-10 | MEDIO |

## NOTAS PARA CAMBIOS FUTUROS (NO incluidos)

1. **FIX-12 (Title bar theming)**: Requiere agregar TitleBarBackgroundBrush a los 5 archivos de tema.
2. **ViewModel God Object**: Refactorizar MainViewModel (~1013 lineas) en ViewModels separados.
3. **Architecture DI**: Migrar de poor-mans DI a Microsoft.Extensions.DependencyInjection.
