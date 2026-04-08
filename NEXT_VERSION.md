# v1.1.0 - Próximos Pasos

## Estado Actual: v1.0.2 ✅ LUFS Module implementado

---

## Features Pendientes / Propuestas

### Prioridad Alta

| # | Feature | Descripción | Estado |
|---|---------|-------------|--------|
| 1 | **Botón Donation** | Agregar PayPal/donation link en "Acerca de" | ⏳ Pendiente |
| 2 | **Batch Processing** | Procesar múltiples archivos | ⏳ Pendiente |
| 3 | **Depuración código** | Arreglar catch vacíos, desuscribir eventos | ⏳ Pendiente |

### Prioridad Media

| # | Feature | Descripción | Estado |
|---|---------|-------------|--------|
| 4 | **Exportar Reporte** | CSV/JSON de análisis | ⏳ Pendiente |
| 5 | **Detección secciones** | Intro/Verse/Chorus | ⏳ Pendiente |
| 6 | **Analytics** | Google Analytics para descargas | ⏳ Pendiente |

### Prioridad Baja

| # | Feature | Descripción | Estado |
|---|---------|-------------|--------|
| 7 | **Historial análisis** | Guardar análisis anteriores | ⏳ Pendiente |
| 8 | **Testing avanzado** | Archivos grandes, casos edge | ⏳ Pendiente |

---

## Bugs Conocidos

1. ⚠️ No hay testing con archivos >100MB
2. ⚠️ Redimensionado mínimo requiere afinación

---

## Notas para Desarrollo Futuro

### Git
- Ultimo commit: Implementado fallback BPM para FLAC
- Para volver a v1.0.0: `git checkout <commit-anterior>`

### Builds
- Release: `publish/`
- Installer: `installer/ToneAndBeatsByHostility_Setup_v1.0.0.exe`

### Documentación
- Principal: `DOCUMENTACION.md`
- License: `LICENSE.txt`
- RPD: `RPD.md`

---

## Checklist Pre-Lanzamiento v1.1

- [ ] Implementar botón de donation
- [ ] Code review: arreglar catch vacíos
- [ ] Code review: desuscribir eventos
- [ ] Testing con archivos grandes
- [ ] Actualizar documentación
- [ ] Versionar a v1.1.0
- [ ] Compilar installer

---

*Ultima actualización: 7 de Abril 2026*
*Hotfix v1.0.1: Bug de detección BPM en FLAC corregido*