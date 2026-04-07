# Licencias de Librerías

Este proyecto usa las siguientes librerías de terceros. Todas las licencias permiten uso comercial.

---

## Librerías Principales

### NAudio
| Campo | Valor |
|-------|-------|
| **Versión** | 2.2.1 |
| **Licencia** | Microsoft Public License (Ms-PL) |
| **Descripción** | Reproducción y análisis de audio |
| **Repositorio** | https://github.com/naudio/NAudio |
| **Uso comercial** | ✅ Permitido |
| **Modificación** | ✅ Permitido |

---

### TagLibSharp
| Campo | Valor |
|-------|-------|
| **Versión** | 2.3.0 |
| **Licencia** | LGPL 2.1 |
| **Descripción** | Lectura y escritura de metadatos de audio |
| **Repositorio** | https://github.com/mono/taglib-sharp |
| **Uso comercial** | ✅ Permitido |
| **Modificación** | ⚠️ Requiere liberar modificaciones a la librería |

---

### BpmFinder
| Campo | Valor |
|-------|-------|
| **Versión** | 0.1.0 |
| **Licencia** | MIT |
| **Descripción** | Detección de BPM/tempo |
| **Repositorio** | https://github.com/bpmfinder/bpm-finder |
| **Uso comercial** | ✅ Permitido |
| **Modificación** | ✅ Permitido |

---

### MediaInfo.Wrapper.Core
| Campo | Valor |
|-------|-------|
| **Versión** | 26.1.0 |
| **Licencia** | BSD-2-Clause |
| **Descripción** | Información técnica de audio/video (bitrate, CBR/VBR, etc.) |
| **Repositorio** | https://github.com/MediaArea/MediaInfo |
| **Uso comercial** | ✅ Permitido |
| **Modificación** | ✅ Permitido (muy permisiva) |

---

### libKeyFinder.NET
| Campo | Valor |
|-------|-------|
| **Versión** | 1.0.0 |
| **Licencia** | GPL 2.0 |
| **Descripción** | Detección de tonalidad musical (Key) |
| **Repositorio** | https://github.com/Restroyer/libKeyFinder.NET |
| **Uso comercial** | ✅ Permitido |
| **Modificación** | ⚠️ Requiere liberar modificaciones bajo GPL 2.0 |

---

### LiveChartsCore.SkiaSharpView.WPF
| Campo | Valor |
|-------|-------|
| **Versión** | 2.0.0-rc2 |
| **Licencia** | MIT |
| **Descripción** | Visualización de gráficos y formas de onda |
| **Repositorio** | https://github.com/LiveCharts/LiveCharts2 |
| **Uso comercial** | ✅ Permitido |
| **Modificación** | ✅ Permitido |

---

## Resumen de Licencias

| Librería | Licencia | Uso Comercial | Modificación |
|----------|----------|--------------|-------------|
| NAudio | Ms-PL | ✅ | ✅ |
| TagLibSharp | LGPL 2.1 | ✅ | ⚠️ Solo librería |
| BpmFinder | MIT | ✅ | ✅ |
| MediaInfo.Wrapper.Core | BSD-2-Clause | ✅ | ✅ |
| libKeyFinder.NET | GPL 2.0 | ✅ | ⚠️ GPL |
| LiveChartsCore | MIT | ✅ | ✅ |

---

## Framework

### .NET 8.0
| Campo | Valor |
|-------|-------|
| **Framework** | .NET 8.0 |
| **Licencia** | MIT |
| **Proveedor** | Microsoft |

---

## Dependencias Transitivas

Al usar las librerías principales, se incluyen automáticamente:

- SkiaSharp (MIT) - Renderizado de gráficos
- HarfBuzzSharp (MIT) - Renderizado de texto
- System.Numerics.Complex - Soporte de números complejos (parte de .NET)

---

## Notas sobre Distribucion

### Requisitos de Atribucion

Al distribuir esta aplicación, se recomienda incluir:

1. Copia de las licencias de cada librería
2. Avisos de copyright de los autores originales
3. Descargo de responsabilidad de garantía

### Compatibilidad de Licencias

Todas las librerías usadas son compatibles entre sí para uso comercial:
- **MIT, Ms-PL, BSD-2-Clause**: Permisivas, sin requisitos de distribución del código
- **LGPL 2.1**: Solo requiere liberar modificaciones a la librería misma
- **GPL 2.0**: Requiere liberar código modificado bajo la misma licencia

---

## Recursos

- [NAudio Documentation](https://github.com/naudio/NAudio)
- [TagLibSharp Documentation](https://github.com/mono/taglib-sharp)
- [BpmFinder Repository](https://github.com/bpmfinder/bpm-finder)
- [MediaInfo Documentation](https://mediaarea.net/MediaInfo)
- [LiveCharts Documentation](https://livecharts.dev/docs/wpf)
- [.NET Documentation](https://docs.microsoft.com/dotnet/)

---

*Documento actualizado: 6 de Abril de 2026*
