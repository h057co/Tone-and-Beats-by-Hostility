# Tone & Beats - Scripts de Construcción

## Desarrollo

```bash
# Restaurar paquetes y compilar
dotnet build

# Ejecutar
dotnet run

# Limpiar
dotnet clean
```

## Publicación

### Framework-Dependent (requiere .NET 8 Runtime)

```bash
dotnet publish -c Release -r win-x64 --self-contained false -o ./build/framework-dependent
```

### Single-File (self-contained, no requiere runtime)

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./build/single-file
```

## Estructura de Builds

```
build/
├── framework-dependent/    # Requiere .NET 8 Runtime
│   └── ToneAndBeatsByHostility.exe
└── single-file/          # Self-contained
    └── ToneAndBeatsByHostility.exe (~158MB)
```

## Comandos Rápidos desde la raíz

```powershell
# Desde la carpeta raíz del proyecto
dotnet build src/AudioAnalyzer.csproj
dotnet run --project src/AudioAnalyzer.csproj
dotnet publish src/AudioAnalyzer.csproj -c Release -r win-x64 --self-contained false -o ./build/framework-dependent
```
