# Tone & Beats by Hostility

Aplicación de escritorio Windows para análisis de audio que detecta BPM y tonalidad musical.

## Requisitos

- **Windows 10/11** (x64)
- **.NET 8.0 SDK** (para desarrollo)
- **.NET 8.0 Runtime** (para ejecutar, si no usas self-contained)

## Inicio Rápido

### Desarrollo

```bash
# Restaurar paquetes
dotnet restore

# Compilar
dotnet build

# Ejecutar
dotnet run
```

### Builds Pre-compilados

| Tipo | Ubicación | Requiere Runtime |
|------|----------|-----------------|
| Framework-Dependent | `build/framework-dependent/` | ✅ .NET 8 Runtime |
| Single-File | `build/single-file/` | ❌ No |

## Estructura del Proyecto

```
ToneAndBeats/
├── src/                          # Código fuente
│   ├── AudioAnalyzer/           # Proyecto principal
│   │   ├── Services/            # Lógica de negocio
│   │   ├── ViewModels/         # MVVM
│   │   ├── Models/             # Modelos de datos
│   │   ├── Controls/           # Controles WPF
│   │   ├── Themes/            # Sistema de temas
│   │   └── ...
│   └── AudioAnalyzer.sln       # Solución
├── build/                        # Builds pre-compilados
├── docs/                         # Documentación
│   ├── DOCUMENTACION.md
│   └── LICENSES.md
├── assets/                       # Recursos gráficos
│   └── README.md
└── README.md
```

## Librerías Principales

| Librería | Uso |
|----------|-----|
| NAudio | Reproducción de audio |
| BpmFinder | Detección de BPM |
| libKeyFinder.NET | Detección de tonalidad |
| MediaInfo.Wrapper.Core | Información técnica de audio |
| LiveChartsCore | Visualización |
| TagLibSharp | Metadatos |

## Comandos Útiles

```bash
# Desarrollo
dotnet build
dotnet run

# Publicación
dotnet publish -c Release -r win-x64 --self-contained false -o ./build/framework-dependent
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./build/single-file

# Limpiar
dotnet clean
```

## Documentación

- `docs/DOCUMENTACION.md` - Documentación técnica completa
- `docs/LICENSES.md` - Licencias de librerías

## Plataforma

- **Framework**: .NET 8.0 + WPF
- **Arquitectura**: x64
- **OS**: Windows 10/11

---

**Versión**: 1.0.0-beta  
**Fecha**: 6 de Abril de 2026
