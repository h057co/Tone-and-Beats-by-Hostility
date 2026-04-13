# Tone & Beats by Hostility - v1.0.12 Release Notes

**Fecha:** 13 de Abril de 2026  
**Versión:** 1.0.12 (Final Release)  
**Licencia:** CC BY-NC-ND 4.0  
**Plataforma:** Windows 10+ (64-bit)  

---

## 🎉 Resumen de Lanzamiento

La versión **1.0.12** marca la conclusión del proyecto con una refactorización completa del sistema de temas visuales, mejorando significativamente la escalabilidad, mantenibilidad y experiencia del usuario.

---

## ✨ Principales Características

### Análisis de Audio Avanzado
- **Detección de BPM:** Algoritmo híbrido con 95% de precisión
- **Identificación de Tonalidad:** Soporte para Camelot Wheel y notación musical
- **Visualización de Forma de Onda:** Renderizado GPU con SkiaSharp
- **Análisis de Volumen:** LUFS Integrated, LRA, True Peak (dBTP)

### Interfaz Visual Mejorada
- **5 Temas Disponibles:** Dark, Light, Blue, iOS Light, iOS Dark
- **Alto Contraste:** ComboBox y controles con paletas optimizadas
- **Logo Dinámico:** Cambia automáticamente según tema seleccionado
- **Responsive Design:** Ventana borderless con redimensionamiento libre

### Reproducción de Audio
- **Playback Control:** Play, Pause, Stop con barra de progreso
- **Visualización de Waveform:** Interactivo con seek
- **Soporta Múltiples Formatos:** MP3, WAV, FLAC, OGG, M4A, AAC, WMA, OPUS

---

## 🔧 Cambios Técnicos v1.0.12

### Refactorización del Sistema de Temas
```
ThemeManager.cs: Rediseño completo
├── Evento ThemeChanged para actualizaciones dinámicas
├── Eliminación de hardcoding de botones
├── Mayor escalabilidad y mantenibilidad
└── Documentación XML completa

MainWindow.xaml.cs: Dinámica mejorada
├── Suscripción a eventos de tema
├── Logo dinámico (negro/blanco)
└── Gestión de ciclo de vida del componente
```

### ComboBox de BPM Range
- **Popup funcional:** Dropdown completamente operativo
- **Alto contraste:** Colores específicos por tema
- **Estilos mejorados:** Hover, selected, disabled states

### Paleta de Colores (ComboBox)
| Tema | Fondo | Texto | Borde |
|------|-------|-------|-------|
| Dark | #1E1E2E | #E0E0E0 | #4A90D9 |
| Light | #FFFFFF | #2D2D2D | #3498DB |
| Blue | #1B263B | #B3E5FC | #00B4D8 |
| iOS Light | #FFFFFF | #000000 | #007AFF |
| iOS Dark | #1C1C1E | #FFFFFF | #0A84FF |

---

## 📋 Requisitos del Sistema

### Software
- **Sistema Operativo:** Windows 10 (build 17763) o posterior (64-bit)
- **Framework:** .NET 8.0 Runtime (incluido en instalador)
- **Memoria RAM:** Mínimo 4 GB (recomendado 8 GB)
- **Espacio en Disco:** 200 MB para instalación

### Hardware
- **Procesador:** Intel/AMD compatible con Windows 10+
- **Audio:** Dispositivo de audio estándar
- **Pantalla:** Resolución mínima 350x900 (recomendado 1920x1080)

---

## 📥 Instalación

### Desde Instalador
1. Descargar `ToneAndBeatsByHostility_Setup_v1.0.12.exe`
2. Ejecutar el instalador
3. Seguir asistente de instalación
4. Iniciar aplicación desde menú Inicio o escritorio

### Desde Binario Compilado
1. Navegar a `src/bin/Release/net8.0-windows/win-x64/publish/`
2. Ejecutar `ToneAndBeatsByHostility.exe` directamente

---

## 🚀 Uso Principal

### Análisis Básico
1. Abrir aplicación
2. Click en "Browse" para seleccionar archivo de audio
3. Click en "🔍 Analyze Audio"
4. Resultados de BPM y Key se mostrarán en tiempo real

### Cambio de Temas
1. Click en botón "🎨" en esquina superior derecha
2. Tema cambia automáticamente
3. Logo y colores se adaptan dinámicamente

### Guardado de Metadatos
1. Analizar archivo
2. Click en "💾 Save to Metadata"
3. Metadatos BPM/Key se guardan en archivo

---

## 📊 Dependencias de Compilación

| Librería | Versión | Licencia |
|----------|---------|----------|
| NAudio | 2.2.1 | Ms-PL |
| FFMpegCore | 5.1.0 | LGPL 2.1 |
| MediaInfo.Wrapper.Core | 26.1.0 | GPL v2/LGPL v2 |
| TagLibSharp | 2.3.0 | LGPL v2.1 |
| NAudio.Vorbis | 1.5.0 | Ms-PL |
| SkiaSharp | 2.88.8 | MIT |
| SoundTouch.Net | 2.3.2 | LGPL 2.1 |

---

## 📜 Licencia

**Tone & Beats by Hostility** está licenciado bajo **CC BY-NC-ND 4.0**

### Permisos
✅ Compartir y distribuir  
✅ Usar para fines personales  
✅ Recibir donaciones voluntarias  

### Restricciones
❌ Uso comercial  
❌ Crear derivados o modificaciones  
❌ Redistribuir sin autorización  

Para más información: [LICENSE.md](LICENSE.md)

---

## 👨‍💻 Acerca del Autor

**Luis Jiménez (Hostility)**  
Productor Musical | Desarrollador  
Medellín, Colombia

- **Contacto:** info@hostilitymusic.com
- **Sitio Web:** www.hostilitymusic.com
- **GitHub:** https://github.com/h057co

---

## 🙏 Agradecimientos

Agradecemos a todos los usuarios que han utilizado y proporcionado feedback sobre Tone & Beats. Este proyecto representa años de desarrollo y refinamiento.

---

## 📝 Notas Finales

Esta es la **versión final (1.0.12)** de Tone & Beats by Hostility. El proyecto se considera completado y estable para uso en producción.

**Última Actualización:** 13 de Abril de 2026

---

*Tone & Beats by Hostility © 2026 Luis Jiménez*
