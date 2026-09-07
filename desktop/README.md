# SportsPredictor Desktop (lanzador WPF)

Orquesta la cadena **WPF → Next.js → API C# → Python**: arranca los tres
servicios de backend en orden (Python primero, luego la API, luego el
frontend), espera a que cada uno responda su health check, y muestra el
dashboard de Next.js embebido en un control `WebView2` — no hay que abrir un
navegador ni una terminal aparte.

## Ejecutar desde el repo de desarrollo

1. Copia `launcher.settings.local.json.example` a `launcher.settings.local.json`
   (gitignored — tiene rutas absolutas específicas de tu máquina) y ajusta las
   rutas si tu checkout no está en la ubicación por defecto.
2. `dotnet build` (o abre la solución) y ejecuta `SportsPredictor.Desktop`.

`launcher.settings.local.json` sobreescribe cualquier valor de
`launcher.settings.json` (el que trae el instalador, pensado para la
estructura `Api/`, `Frontend/`, `Ml/` una vez instalado, no para el repo).

## Puertos

- API C#: `20050`
- Next.js: `20051`
- Servicio ML Python: `8001`

## Orden de arranque y por qué

1. **Python (ML service)** — lo más lento en arrancar (importa
   scikit-learn/xgboost/lightgbm), así que se lanza primero para que esté
   listo cuando la API lo necesite.
2. **API C#** — necesita poder llamar al servicio ML para entrenar/predecir,
   así que espera a que Python responda antes de arrancar.
3. **Next.js** — solo necesita la URL de la API, así que va al final.

Cada paso espera (hasta 60s) a que el health check correspondiente responda
antes de continuar con el siguiente. Si algo no arranca, la pantalla de
estado muestra el error en vez de abrir el dashboard contra un backend a
medias.

## Limitación conocida

Si cierras la ventana con la X, el evento `Closing` mata los tres procesos
hijos. Si la app se termina a la fuerza (Administrador de tareas, `taskkill`,
un crash), los procesos hijos pueden quedar huérfanos — es una limitación
conocida de esta iteración, no un bug pasado por alto; la mayoría de
lanzadores de escritorio simples tienen el mismo comportamiento. Si hace
falta un cierre más robusto, el siguiente paso sería usar un Job Object de
Windows para que el sistema operativo mate a los hijos automáticamente
cuando el proceso padre termine, sin importar cómo.
