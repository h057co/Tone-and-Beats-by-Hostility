# v1.1.0 - Próximos Pasos

## Estado Actual: v1.0.3 ✅ Release Completado

**Fecha de Release:** 9 de Abril de 2026  
**Licencia:** CC BY-NC-ND 4.0 (Donationware)  
**Repositorio:** https://github.com/h057co/Tone-and-Beats-by-Hostility

---

## Features Implementados en v1.0.3 ✅

| Feature | Descripción | Estado |
|---------|-------------|--------|
| Botón Donation | KoFi + QR donations en "Acerca de" | ✅ Completado |
| iOS Themes | iOS Light + iOS Dark | ✅ Completado |
| LRA Terminology | Corregido display "LRA" | ✅ Completado |
| Single-File | Executable self-contained | ✅ Completado |
| Documentación | README, LICENSE, CHANGELOG | ✅ Completado |

---

## Features Pendientes / Propuestas

### Prioridad Alta

| # | Feature | Descripción | Estado |
|---|---------|-------------|--------|
| 1 | **Batch Processing** | Procesar múltiples archivos | ⏳ Pendiente |
| 2 | **Code Cleanup** | Arreglar catch vacíos, desuscribir eventos | ⏳ Pendiente |

### Prioridad Media

| # | Feature | Descripción | Estado |
|---|---------|-------------|--------|
| 3 | **Exportar Reporte** | CSV/JSON de análisis | ⏳ Pendiente |
| 4 | **Detección secciones** | Intro/Verse/Chorus | ⏳ Pendiente |

### Prioridad Baja

| # | Feature | Descripción | Estado |
|---|---------|-------------|--------|
| 5 | **Historial análisis** | Guardar análisis anteriores | ⏳ Pendiente |
| 6 | **Testing avanzado** | Archivos grandes, casos edge | ⏳ Pendiente |

---

## Bugs Conocidos

1. ⚠️ No hay testing con archivos >100MB
2. ⚠️ Redimensionado mínimo requiere afinación

---

## Notas para Desarrollo Futuro

### Git
- Rama principal: `master`
- Tags: `v1.0.3`, `v1.0.2`
- Para volver a versión anterior: `git checkout v1.0.2`

### Builds
- Release portable: `publish-v1.0.3/ToneAndBeatsByHostility.exe` (148 MB)
- Installer: `installer/ToneAndBeatsByHostility_Setup_v1.0.3.exe` (146 MB)

### Documentación
- Principal: `README.md`
- Licencia: `LICENSE.md` (CC BY-NC-ND 4.0)
- Librerías: `LICENSES.md`

---

## Checklist Pre-Lanzamiento v1.1

- [ ] Implementar batch processing
- [ ] Code review: arreglar catch vacíos
- [ ] Code review: desuscribir eventos
- [ ] Testing con archivos grandes
- [ ] Actualizar documentación
- [ ] Versionar a v1.1.0
- [ ] Compilar installer
- [ ] Actualizar GitHub release

---

*Última actualización: 9 de Abril 2026*  
*v1.0.3: Donationware, CC BY-NC-ND 4.0*