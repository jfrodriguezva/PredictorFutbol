# START HERE — Cómo comenzar con Claude Code

Tienes una cuenta PRO de API-FOOTBALL.

NO pongas tu API key en ningún archivo que vayas a compartir.

## Archivos a enviar a Claude Code

Envía o coloca en la raíz del proyecto:

1. `CLAUDE.md`
2. `.env.example`
3. `PHASE_1_PROMPT.txt`

Después abre Claude Code dentro de la carpeta del proyecto.

## Paso 1

Crea una carpeta vacía:

```bash
mkdir SportsPredictor
cd SportsPredictor
```

Copia ahí `CLAUDE.md`, `.env.example` y `PHASE_1_PROMPT.txt`.

## Paso 2

Ejecuta Claude Code en esa carpeta y pega el contenido de:

`PHASE_1_PROMPT.txt`

No le des todavía tu API key.

## Paso 3

Cuando termine Fase 1, usa:

`PHASE_2_PROMPT.txt`

## Paso 4

Cuando termine Fase 2, configura tu API key SOLO en tu máquina.

Ejemplo:

```text
API_FOOTBALL_KEY=TU_CLAVE_REAL
```

No la pegues en chat, GitHub, capturas o README.

Entonces usa:

`PHASE_3_API_FOOTBALL_PROMPT.txt`

## Importante

Aunque tu plan es PRO, el programa debe leer la cuota real y los límites desde los headers de API-FOOTBALL.

No hardcodear número de requests.

## Objetivo de las primeras tres fases

Al finalizar Fase 3 debes tener:

- frontend arrancando;
- backend arrancando;
- SQL Server conectado;
- servicio ML arrancando;
- API-Football configurada;
- conexión probada;
- countries funcionando;
- leagues funcionando;
- cuota visible;
- tests pasando.

Todavía NO debemos entrenar modelos.

Primero necesitamos una base de datos confiable.
