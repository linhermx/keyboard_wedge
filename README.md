# LINHER Keyboard Wedge

Aplicación de escritorio para Windows que recibe la salida RS-232 de una báscula RHINO BACO-30 y envía el valor de `QTY` como entrada de teclado a la ventana activa.

Está pensada para capturar cantidades en Excel o en cualquier otro sistema que acepte entrada por teclado, sin desarrollar una integración directa con el sistema destino.

## Qué hace

- Lee datos desde la báscula por puerto serial.
- Detecta y extrae el valor de `QTY`.
- Escribe la cantidad en la ventana activa como si fuera un teclado wedge.
- Permite enviar después `Enter`, `Tab` o nada.
- Guarda configuración local por usuario.
- Registra actividad y errores en logs diarios.
- Puede iniciar minimizada, trabajar en bandeja e iniciar con Windows.
- Usa un launcher para poder recibir actualizaciones por `GitHub Releases`.

## Instalación

La opción recomendada para usuarios finales es instalar desde el asset:

```text
linher_keyboard_wedge_setup.exe
```

Ese instalador deja un launcher estable en el equipo. El launcher abre la aplicación y, cuando exista un release más reciente, puede descargar la actualización publicada.

También existe un paquete portable del launcher:

```text
linher_keyboard_wedge_launcher_portable.zip
```

## Primer uso

1. Conecta la báscula a la computadora.
2. Abre `LINHER Keyboard Wedge`.
3. Selecciona el puerto COM donde quedó conectada la báscula.
4. Usa primero esta configuración:
   - `9600`
   - `8`
   - `None`
   - `1`
   - `None`
5. Presiona `Conectar`.
6. Deja activo el campo destino en Excel o en tu sistema.
7. Presiona `P / PRINT` en la báscula.

Si la báscula envía:

```text
WT:   1.420kg
AWP:207.587g
QTY:        7 pcs
```

la aplicación detecta:

```text
7
```

y lo escribe en la ventana activa.

## Parámetros principales

### Comunicación serial

- `Puerto COM`
- `Baud rate`
- `Data bits`
- `Parity`
- `Stop bits`
- `Flow control`
- `DTR activo`
- `RTS activo`

### Captura y envío

- `Regex QTY`
- `Después de enviar`
- `Anti-duplicado ms`
- `Iniciar minimizada`
- `Minimizar a bandeja`
- `Iniciar con Windows`

## Archivos locales

La aplicación guarda su información en:

```text
%LOCALAPPDATA%\LINHER\KeyboardWedge
```

Archivos principales:

```text
%LOCALAPPDATA%\LINHER\KeyboardWedge\config.json
%LOCALAPPDATA%\LINHER\KeyboardWedge\logs\YYYY-MM-DD.log
```

Si existía una instalación anterior, la configuración previa se migra automáticamente desde:

```text
%LOCALAPPDATA%\RhinoKeyboardWedge
```

## Actualizaciones

El flujo de actualización usa `GitHub Releases`.

Assets de release:

```text
linher_keyboard_wedge_windows.zip
linher_keyboard_wedge_launcher_portable.zip
linher_keyboard_wedge_setup.exe
```

El launcher consulta el release más reciente, compara la versión instalada y descarga `linher_keyboard_wedge_windows.zip` cuando detecta una versión más nueva.

## Diagnóstico básico

Si la aplicación no recibe datos o no escribe en la ventana destino:

1. Verifica que el puerto COM seleccionado sea el correcto.
2. Confirma que la báscula esté transmitiendo al presionar `P / PRINT`.
3. Revisa la última lectura mostrada en pantalla.
4. Abre los logs desde la propia aplicación.
5. Asegúrate de que la ventana destino tenga el foco.
6. Si la aplicación destino se ejecuta como administrador, ejecuta esta app al mismo nivel de permisos.

## Desarrollo

### Compilación

```powershell
$dotnet = ".\.dotnet\dotnet.exe"
& $dotnet build .\RhinoKeyboardWedge.sln -c Release
```

### Generación de artefactos

```powershell
.\installer\build-setup.ps1
```

Salida esperada:

```text
dist\LinherKeyboardWedge\
dist\linher_keyboard_wedge_windows.zip
dist\linher_keyboard_wedge_launcher_portable.zip
dist\linher_keyboard_wedge_setup.exe
```

## Estructura

```text
RhinoKeyboardWedge.sln
RhinoKeyboardWedge.App/
LinherKeyboardWedge.Launcher/
RhinoKeyboardWedge.Setup/
assets/
installer/
config.example.json
```
