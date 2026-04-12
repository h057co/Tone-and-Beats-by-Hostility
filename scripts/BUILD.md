# Tone & Beats - Scripts de Construcción

**Versión:** 1.0.6  
**Fecha:** 12 de Abril de 2026

## Desarrollo

```bash
# Restaurar paquetes y compilar
cd src
dotnet build -c Release

# Ejecutar
dotnet run -c Release

# Limpiar
dotnet clean
```

## Publicación

### Framework-Dependent (requiere .NET 8 Runtime)

```bash
dotnet publish -c Release -r win-x64 --self-contained false -o ./publish
```

### Single-File (self-contained, no requiere runtime)

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o ./publish-v1.0.6
```

## Estructura de Builds Actual

```
publish-v1.0.6/                      # Single-file (~370 MB)
├── ToneAndBeatsByHostility.exe     # Executable
├── D3DCompiler_47_cor3.dll
├── PresentationNative_cor3.dll
├── wpfgfx_cor3.dll
└── ffmpeg/                         # FFmpeg
    ├── ffmpeg.exe
    └── ffprobe.exe
```

## Installer (Inno Setup)

```bash
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" "installer/setup.iss"
```

Output: `installer/ToneAndBeatsByHostility_Setup_v1.0.6.exe`

## Comandos Rápidos desde la raíz

```powershell
# Desde la carpeta raíz del proyecto
dotnet build src/AudioAnalyzer.csproj -c Release
dotnet run --project src/AudioAnalyzer.csproj -c Release
dotnet publish src/AudioAnalyzer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../publish-v1.0.6
```

---

*Versión 1.0.6 - Updated: 12 de Abril 2026*
*License: CC BY-NC-ND 4.0*
