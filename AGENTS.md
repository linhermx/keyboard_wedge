# AGENTS.md

Reglas obligatorias para cualquier agente que trabaje en este repositorio.

## Procedimiento

- Leer este archivo antes de analizar, modificar, compilar, versionar, etiquetar o preparar releases.
- No hacer push, crear tags remotos ni publicar releases sin aprobación explícita del usuario.
- No modificar ni reemplazar tags o releases ya publicados.
- Antes de cerrar cambios de código, ejecutar compilación y validar los artefactos generados.
- No versionar builds, logs, zips temporales, caches, SDKs locales ni evidencia local.

## Idioma y documentación

- `README.md` debe mantenerse en español salvo instrucción explícita distinta.
- Las notas públicas de `GitHub Releases` deben redactarse en inglés.
- La documentación para usuario final debe asumir un operador normal, no a quien desarrolló la app.
- Evitar referencias a pruebas internas, herramientas usadas durante desarrollo o rutas personales que no aporten al usuario final.

## Branding y naming

- Nombre visible de la aplicación: `LINHER Keyboard Wedge`.
- Ejecutable principal: `LinherKeyboardWedge.exe`.
- Launcher estable: `LinherKeyboardWedgeLauncher.exe`.
- El icono oficial vive en:
  - `assets/branding/linher-keyboard-wedge.png`
  - `assets/branding/linher-keyboard-wedge.ico`
- El icono debe conservar:
  - isotipo de LINHER con `2 flamas`,
  - referencia clara al rhino,
  - referencia clara a teclado/input,
  - uso moderado del rojo.

## Rutas críticas

- Configuración y logs por usuario:
  - `%LOCALAPPDATA%\LINHER\KeyboardWedge`
- Carpeta heredada a migrar:
  - `%LOCALAPPDATA%\RhinoKeyboardWedge`
- Instalación estable del launcher:
  - `%LOCALAPPDATA%\Programs\LINHER Keyboard Wedge`
- Raíz runtime del launcher:
  - `%LOCALAPPDATA%\LINHER\KeyboardWedge`
- Override para pruebas del launcher:
  - `LINHER_KEYBOARD_WEDGE_RUNTIME_ROOT`

## Releases y assets

- Repositorio esperado para actualización:
  - `linhermx/keyboard_wedge`
- Asset obligatorio para auto-actualización:
  - `linher_keyboard_wedge_windows.zip`
- Asset portable del launcher:
  - `linher_keyboard_wedge_launcher_portable.zip`
- Instalador para usuario final:
  - `linher_keyboard_wedge_setup.exe`
- Metadata embebida del launcher:
  - `bundled_assets/linher_keyboard_wedge_release.json`
- El launcher compara releases contra carpetas instaladas con el patrón:
  - `app/linher_keyboard_wedge_vX.Y.Z`

## Versionado

- Este proyecto debe usar `SEMVER`.
- Versión preparada actualmente: `1.0.0`.
- Regla:
  - `PATCH`: corrección de bugs sin cambios funcionales relevantes.
  - `MINOR`: mejoras backward-compatible de UI, branding, instalador, launcher o funcionalidad.
  - `MAJOR`: cambios incompatibles en distribución, configuración o comportamiento operativo esperado.
- Antes de release, alinear la misma versión en:
  - `RhinoKeyboardWedge.App.csproj`
  - `LinherKeyboardWedge.Launcher.csproj`
  - `RhinoKeyboardWedge.Setup.csproj`
  - `installer/KeyboardWedge.iss`
  - metadata de release embebida por `installer/build-setup.ps1`

## Convención de commits

- Usar commits pequeños, atómicos y enfocados.
- Preferir más commits pequeños sobre pocos commits grandes cuando eso mejore trazabilidad y rollback.
- No mezclar código, documentación, versionado, branding y release prep en un solo commit si se pueden separar limpiamente.
- Si un cambio toca áreas distintas, partirlo por intención técnica, no por comodidad.
- Convenciones permitidas para el prefijo:
  - `feat:`
  - `fix:`
  - `docs:`
  - `chore:`
  - `test:`
- El mensaje debe ir en minúsculas, conciso y en estilo imperativo o descriptivo corto.
- Ejemplos válidos:
  - `feat: add launcher-based update flow`
  - `fix: preserve legacy config migration`
  - `docs: update README for installer distribution`
  - `chore: prepare v1.0.0 release`
  - `test: add launcher packaging checks`
- Separar, cuando aplique:
  - inicialización del repositorio,
  - reglas de ignorado,
  - branding/assets,
  - cambios funcionales,
  - launcher,
  - instalador y packaging,
  - ajustes de documentación,
  - bump de versión,
  - preparación de release.

## Validación mínima antes de release

- Ejecutar:
  - `.\installer\build-setup.ps1`
- Verificar salida:
  - `dist\LinherKeyboardWedge\`
  - `dist\LinherKeyboardWedgeLauncher\`
  - `dist\linher_keyboard_wedge_windows.zip`
  - `dist\linher_keyboard_wedge_launcher_portable.zip`
  - `dist\linher_keyboard_wedge_setup.exe`
- Verificar que el launcher portable incluya:
  - `LinherKeyboardWedgeLauncher.exe`
  - `bundled_assets/linher_keyboard_wedge_windows.zip`
  - `bundled_assets/linher_keyboard_wedge_release.json`
- Verificar que el setup instale el launcher, no el ejecutable versionado directo.

## Restricciones de implementación

- No romper la migración automática desde `%LOCALAPPDATA%\RhinoKeyboardWedge`.
- `Iniciar con Windows` debe apuntar al launcher estable cuando exista.
- No cambiar nombres de assets de release sin actualizar launcher, README, build scripts y este archivo.
