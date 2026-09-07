# Instalador de SportsPredictor

Empaqueta todo el stack (API C#, dashboard Next.js, servicio ML Python, lanzador
WPF) para instalarse en cualquier equipo Windows.

## Prerrequisitos

**En la máquina donde compilas el instalador:**
- .NET 10 SDK, Node.js 22+, Python 3.12 (los mismos que usas para desarrollo).
- [Inno Setup 6+](https://jrsoftware.org/isdl.php) para compilar el `.iss` final.

**En la máquina donde se instala** (documentado, no empaquetado — ver
"Qué NO empaqueta este instalador" abajo):
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
  (trae ASP.NET Core Runtime + el runtime de WPF).
- [Node.js 22+](https://nodejs.org/) (para correr el build standalone de Next.js).
- [Python 3.12](https://www.python.org/downloads/) (el instalador crea el
  virtualenv del servicio ML automáticamente en el primer arranque).
- WebView2 Runtime — ya viene con Windows 11 por defecto.

## Cómo generar el instalador

```powershell
cd installer
./build-release.ps1
```

Esto publica la API, el lanzador WPF, y el build standalone de Next.js, y copia
el código fuente del servicio ML, todo dentro de `installer/staging/`.

Luego abre `installer/SportsPredictor.iss` con Inno Setup y compílalo
(`Build > Compile`, o `ISCC.exe SportsPredictor.iss` por línea de comandos). El
instalador queda en `installer/output/SportsPredictor-Setup-<version>.exe`.

## Qué hace el instalador

1. Copia `Api/`, `Desktop/`, `Frontend/`, `Ml/` y crea `database/` (vacía) bajo
   el directorio de instalación elegido.
2. Corre `setup-ml-venv.ps1` automáticamente — crea `Ml/.venv` e instala
   `requirements.txt`. Puede tardar unos minutos la primera vez.
3. Crea accesos directos (menú inicio + escritorio opcional) apuntando a
   `Desktop/SportsPredictor.Desktop.exe`.
4. Al desinstalar, borra `Ml/.venv` y `database/` (los datos locales).

## Qué NO empaqueta este instalador (alcance de esta iteración)

No embebe los runtimes de .NET, Node ni una distribución de Python — el
usuario debe tenerlos instalados. Empaquetar Python embebido (o un runtime de
.NET self-contained) es viable pero es un instalador bastante más grande y
complejo; se dejó fuera deliberadamente para no comprometer la calidad del
resto del proyecto en esta pasada. Documentado explícitamente, no es un
olvido — si se necesita un instalador 100% autocontenido más adelante, el
siguiente paso natural es:
- Publicar la API con `--self-contained true` (ya casi gratis, solo cambia una
  flag en `build-release.ps1`).
- Empaquetar `frontend/` como ejecutable con `pkg`/`nexe`, o simplemente
  documentar Node como prerequisito (como se hizo aquí).
- Descargar el "embeddable Python" oficial de python.org y pre-instalar las
  wheels de `requirements.txt` dentro del instalador mismo.

## Configuración de API-FOOTBALL después de instalar

La clave sigue sin ir en ningún archivo — configúrala como variable de
entorno de usuario en Windows (`API_FOOTBALL_KEY`) antes de abrir la app, o
edita el acceso directo para que la fije antes de lanzar el `.exe`. Ver
`.env.example` (copiado junto al instalador) y `docs/api-football.md`.
