# Plan de implementación: actualizaciones desde GitHub

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Actualizar automáticamente la instalación Windows desde Releases sin interrumpir ventas ni depender de internet para abrir caja.

**Architecture:** Un servicio .NET independiente consulta y verifica Releases. Un coordinador Windows administra conexión, periodicidad y actualización pendiente; un auxiliar externo espera el cierre, instala y reabre. El arranque consume una actualización pendiente antes de cargar la caja y reutiliza el respaldo SQLite existente.

**Tech Stack:** .NET 8, MAUI Windows, HttpClient, System.Text.Json, SHA256, PowerShell e Inno Setup.

**Spec:** [Diseño aprobado](../specs/2026-10-06-github-updates-design.md).

## Global Constraints

- Repositorio `IcedDino/HeladeriaVillegas`; instalador `HeladeriaVillegas-Setup-x64.exe`.
- Consultar al iniciar, cada hora y al recuperar conexión; solo releases estables.
- Versiones `major.minor.patch`, tags `vmajor.minor.patch`; nunca reinstalar o bajar versión.
- Manifiesto Windows `requireAdministrator`; no desactivar UAC.
- Sin conexión permitir vender normalmente; no bloquear la interfaz con consultas.
- Instalar al siguiente inicio, antes de cargar caja; sin reiniciar Windows ni forzar otras instancias.
- Validar tamaño y SHA-256 publicado por GitHub; sin digest válido no ejecutar.
- Conservar datos; generar respaldo verificado antes de instalar; aplazar si falla.
- Solo actualizar instalaciones del instalador; nunca desarrollo.
- Reintentos tras fallos separados por al menos una hora.

## Review Focus

- Estado pendiente manipulado: rechazar rutas fuera del directorio de actualizaciones y URLs ajenas al repositorio.
- Apagado durante descarga: ignorar archivos parciales y conservar una actualización válida previa.
- Reconexiones repetidas: una sola consulta concurrente, respetando límites de API y plazo de reintento.
- Dos instancias abiertas: no sustituir archivos utilizados por otra caja.
- Fallo del auxiliar o del instalador: reabrir la versión disponible y evitar ciclos de reintentos.

### Task 1: Consulta, descarga y estado verificable

**Files:** Crear `Services/GitHubUpdateService.cs`, `Services/UpdateStateStore.cs`, `Verification/UpdateChecks.cs`; modificar `Verification/Verification.csproj` y `Verification/Program.cs`.

**Interfaces:** `UpdatePackage(string Version, string DownloadUrl, long Size, string Sha256)`; `GitHubUpdateService(HttpClient client)`; `Task<UpdatePackage?> FindUpdateAsync(Version currentVersion, CancellationToken cancellationToken)`; `Task DownloadAsync(UpdatePackage package, string destination, CancellationToken cancellationToken)`; `Task<bool> VerifyAsync(UpdatePackage package, string path, CancellationToken cancellationToken)`.

`PendingUpdate(UpdatePackage Package, DateTimeOffset? LastAttemptUtc)`; `UpdateStateStore(string directory)` con `PendingUpdate? Read()`, `void Write(PendingUpdate state)`, `void Clear()` y `string InstallerPath`. El nombre del instalador local es fijo y no se toma del JSON.

- [ ] Escribir pruebas con HttpMessageHandler simulado: 1.10.0 supera 1.9.0; igual/antigua/prerelease/tag inválido no actualizan; ausencia de asset o digest no actualiza; 404/403/429 y timeout se manejan sin detener caja.
- [ ] Probar tamaño/hash erróneos, cancelación y descarga parcial: no generan instalador pendiente. Probar URL ajena al repositorio, JSON corrupto y rutas manipuladas: se rechazan. Probar que una descarga fallida no elimina el instalador válido anterior.
- [ ] Ejecutar `dotnet run --project Verification/Verification.csproj` con el SDK local; confirmar fallo de las nuevas pruebas antes de implementar.
- [ ] Implementar consulta HTTPS con User-Agent, parseo estricto de versiones y digest, descarga temporal por streaming, límites de tamaño y reemplazo atómico solo tras verificación. Persistir JSON de forma atómica y validar nuevamente al leer.
- [ ] Ejecutar verificaciones; confirmar que pasan. Guardar únicamente archivos propios de esta tarea en un commit.

### Task 2: Coordinación Windows y auxiliar de instalación

**Files:** Crear `Services/WindowsUpdateCoordinator.cs`, `installer/apply-update.ps1`, `Verification/UpdateCoordinatorChecks.cs`; modificar el proyecto de verificación para enlazar la lógica portable.

**Interfaces:** `Task<bool> TryInstallPendingAsync(CancellationToken cancellationToken)` devuelve true únicamente cuando el auxiliar se lanzó y el proceso de caja debe terminar; `void Start()` y `void Dispose()` administran comprobaciones periódicas y reconexión. Inyectar reloj, conectividad, detección de instalación y lanzamiento mediante delegados para verificar decisiones sin lanzar instaladores.

- [ ] Escribir pruebas: sin conexión no consulta; reconexiones simultáneas no duplican consultas; fallo respeta una hora; estado corrupto no instala; otra instancia abierta aplaza; respaldo fallido aplaza; build de desarrollo no descarga ni instala.
- [ ] Ejecutar verificaciones y confirmar fallo de las pruebas nuevas.
- [ ] Implementar con HttpClient independiente, semáforo y cancelación. Detectar instalación con `unins000.exe` junto al ejecutable y excluir DEBUG. Persistir hora de intento antes de lanzar auxiliar. Crear respaldo con `BackupService.CreateBackup()` solo si existe base; una primera instalación sin datos no necesita respaldo.
- [ ] Implementar auxiliar con parámetros explícitos y rutas absolutas; validar archivo y hash; esperar cierre del PID original con límite de 60 segundos y comprobar otras instancias de la misma instalación. Ejecutar Inno con `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOCLOSEAPPLICATIONS /NORESTARTAPPLICATIONS`, preservando carpeta instalada mediante `/DIR`. Nunca iniciar el instalador si sigue habiendo una instancia abierta.
- [ ] Registrar salida y código del instalador; limpiar estado pendiente solo ante éxito. Reabrir el ejecutable disponible incluso ante fallo. No repetir automáticamente el instalador dentro del auxiliar. Lanzar PowerShell con ventana oculta y argumentos escapados como código PowerShell, evitando interpolación insegura.
- [ ] Ejecutar pruebas del coordinador. Verificar sintaxis del auxiliar con el parser PowerShell y simulaciones que no sustituyan la instalación real. Guardar archivos propios en commit.

### Task 3: Arranque, elevación y empaquetado versionado

**Files:** Modificar `App.xaml.cs`, `MauiProgram.cs`, `Platforms/Windows/app.manifest`, `HeladeriaPOS.Maui.csproj`, `installer/HeladeriaPOS.iss`, `installer/build-installer.ps1`, `installer/README.md`, `README.md`; crear `AppVersion.props`.

**Interfaces:** `AppVersion.props` define `ReleaseVersion=1.0.0` por defecto y admite override MSBuild. El script exige `-Version major.minor.patch`, transmite esa versión a publish y a ISCC mediante `/DAppVersion`. Inno usa `{#AppVersion}` para `AppVersion`; incluir el auxiliar en la publicación como archivo de contenido.

- [ ] Incorporar coordinador al contenedor de servicios Windows. Antes de cargar datos, consumir pendiente; si se lanza auxiliar, terminar app ordenadamente. En caso de excepción registrar y continuar caja. Empezar consultas después de mostrar la caja y detener tareas al cerrar.
- [ ] Cambiar manifiesto a `requireAdministrator`. Usar ReleaseVersion para ApplicationDisplayVersion, Version, AssemblyVersion y FileVersion; verificar compatibilidad de ApplicationVersion numérico MAUI y conservar su valor si no se necesita para Windows sin paquete.
- [ ] Versionar instalador y empaquetado; validar argumento antes de descargar dependencias. Documentar publicación de tag y asset, requisito de digest, elevación, demora hasta siguiente inicio y ubicación de logs.
- [ ] Ejecutar verificaciones y compilar Windows con SDK local: `dotnet build HeladeriaPOS.Maui.csproj -f net8.0-windows10.0.19041.0 -c Release -p:Platform=x64`.
- [ ] Generar instalador de prueba versionado; inspeccionar manifiesto y versiones del ejecutable/instalador. Probar ciclo completo en Windows con dos versiones de prueba, respaldo, reapertura, fallo del instalador y segunda instancia. No publicar Release ni sustituir una instalación real sin autorización específica. Indicar con precisión cualquier prueba interactiva que no pueda realizarse.
- [ ] Revisar diff para asegurar que se preservan cambios previos del usuario. Guardar solo cambios propios; entregar resultados de verificaciones y requisitos para el primer Release compatible.
