# 📜 Manual Maestro de Rituales: IA + Git + Journaling

Este manual establece los protocolos de comunicación y gestión técnica para maximizar la productividad con agentes de IA, garantizando la integridad del código, la persistencia de la memoria y la seguridad del proyecto.

---

## FASE 1: INICIALIZACIÓN DEL PROYECTO

### 🚀 1. Ritual de Génesis (Día Cero)
**Objetivo:** Establecer las bases técnicas y de documentación desde el día 1.

👇 **COPIA ESTE PROMPT** 👇
```text
Vamos a iniciar un proyecto nuevo. Ejecuta el Ritual de Génesis:

1. Inicialización: Crea la estructura de carpetas e inicializa con `git init`.
2. Archivos Base: Crea `.gitignore`, `README.md` y el archivo `JOURNAL.md`.
3. Commit Cero: Realiza el primer commit como `chore: initial project setup`.
4. Reglas: Define en el Journal la sección `# Project Overview & Rules` con el stack y arquitectura elegida.
```

### 🛡️ 2. Protocolos de Seguridad y Calidad (Reglas Permanentes)
**Objetivo:** Definir los límites del agente de IA que deben respetarse en todo momento.

👇 **COPIA ESTOS PROMPTS SEGÚN LA NECESIDAD** 👇
```text
[Sanidad de Dependencias]: Antes de instalar cualquier dependencia nueva, realiza un Análisis de Necesidad: 1. ¿Existe ya algo que haga esto? 2. ¿Impacto en el tamaño del bundle? 3. ¿Es segura? Espera mi aprobación.

[Protección de Secretos]: Prohibido el hardcoding de credenciales. Todo secreto debe ir en el archivo `.env`. Si detectas que estoy olvidando esto, detente y adviérteme antes de cualquier commit.

[Poda de Contexto]: Para esta tarea, solo carga en memoria activa los archivos relacionados con el módulo actual. Ignora el resto del repositorio para evitar ruido.
```

### 📖 3. Estructura Estándar del `JOURNAL.md`
**Objetivo:** Mantener un formato predecible para que la IA lea y escriba el historial sin confundirse.

👇 **ESTRUCTURA DE REFERENCIA PARA EL ARCHIVO** 👇
```markdown
# LOG DE DESARROLLO

## [FECHA] - Título de la Entrada
**Commit:** `[7-char-hash]`

### 1. Changelog
* [feat] Descripción...
* [fix] Descripción...

### 2. Deuda Técnica / TODOs
* [ ] Tareas pendientes o refactorizaciones...

### 3. Handover Note
* Retomar en `archivo.js`, línea X. Siguiente paso: ...
```

---

## FASE 2: FLUJO DE TRABAJO DIARIO

### 🌅 4. Ritual de Apertura (Inicio de Jornada)
**Objetivo:** Sincronizar al agente con el estado real del proyecto y retomar exactamente donde se dejó.

👇 **COPIA ESTE PROMPT** 👇
```text
Iniciamos sesión de desarrollo. Ejecuta el Ritual de Apertura Maestro:

1. Context Ingestion: Lee la última entrada del `JOURNAL.md`. Presta especial atención a la Handover Note y a la Deuda Técnica registrada.
2. Git Check: Verifica el estado actual del repositorio (`git status` y `git log -1`). Confirma la rama activa y el hash del último commit.
3. Environment Validation: Asegúrate de que no hay errores de compilación o dependencias faltantes.
4. Action Plan: Define la primera tarea técnica basada en el Handover y los pendientes.

Confirma tu lectura y dime el primer paso a ejecutar.
```

---

## FASE 3: DESARROLLO Y EXPERIMENTACIÓN

### 🧪 5. Ritual de Experimentación (Branching)
**Objetivo:** Probar nuevas implementaciones, lógicas complejas o refactorizaciones en un entorno aislado.

👇 **COPIA ESTE PROMPT** 👇
```text
Iniciamos implementación experimental. Ejecuta el Ritual de Branching:

1. Sanity Check: Confirma que `git status` está limpio en la rama principal.
2. Aislamiento: Crea y cambia a la rama `feat/[nombre-experimento]`.
3. Registro: Documenta en `JOURNAL.md` el inicio del experimento y el objetivo.
```

### ✅ 6. Ritual de Fusión (Éxito del Experimento)
**Objetivo:** Integrar formalmente la nueva funcionalidad probada a la rama principal.

👇 **COPIA ESTE PROMPT** 👇
```text
El experimento es un éxito. Ejecuta el Ritual de Merge:

1. Commit Final: Asegura los cambios en la rama experimental.
2. Integración: Regresa a la rama principal y realiza el `git merge`.
3. Cierre: Elimina la rama experimental y actualiza el Journal como [SUCCESSFUL MERGE].
```

### 🛑 7. Ritual de Aborto (Fallo del Experimento)
**Objetivo:** Limpiar el rastro de una prueba fallida y volver a la estabilidad sin dejar código basura.

👇 **COPIA ESTE PROMPT** 👇
```text
El experimento falló. Ejecuta el Ritual de Aborto:

1. Snapshot de Errores: Explica brevemente por qué falló la implementación.
2. Retorno: Cambia a la rama principal y elimina la rama fallida (`git branch -D`).
3. Lección Aprendida: Registra en el Journal por qué se abortó para evitar repetirlo.
4. Restauración: Limpia cachés y confirma estado estable.
```

---

## FASE 4: REVISIÓN Y CIERRE

### 🔍 8. Ritual de Auditoría (Revisión de Calidad)
**Objetivo:** Evaluar la salud técnica del sistema antes de dar por terminada una gran fase de desarrollo.

👇 **COPIA ESTE PROMPT** 👇
```text
Actúa como Auditor Senior. Realiza una Auditoría Estática en 5 pilares:

1. Arquitectura: Separación de responsabilidades y acoplamiento.
2. Calidad: Code smells, principios SOLID y DRY.
3. Seguridad: Manejo de .env, validación de inputs y riesgos de inyección.
4. Performance: Cuellos de botella y consultas ineficientes.
5. Dependencias: Librerías obsoletas o riesgosas.

Entrega los hallazgos en Markdown ordenados por severidad (🔴 Crítico a 🔵 Bajo) con soluciones sugeridas.
```

### 🌙 9. Ritual de Cierre Maestro (Fin de Jornada)
**Objetivo:** Limpiar el entorno, asegurar el código en Git y persistir la memoria para el día siguiente.

👇 **COPIA ESTE PROMPT** 👇
```text
Finalizamos la sesión. Procede con el Ritual de Cierre Maestro:

1. Code Cleanup: Elimina logs de depuración, comentarios innecesarios y código muerto.
2. Integrity Check: Ejecuta un build/test rápido para confirmar estabilidad.
3. Git Consolidación: Realiza `git add .` y un `git commit` estructurado (Conventional Commits).
4. Update JOURNAL.md: Crea la entrada de hoy incluyendo el Changelog, Deuda Técnica y Handover Note.
5. Shutdown: Confirma que la persistencia está asegurada y el entorno limpio.
```