# Responsive POS and 3D Splash Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep the existing POS usable at 900 × 680 and add an offline 3D welcome splash that overlaps its four-second minimum display with POS initialization.

**Architecture:** Make targeted XAML and page-sizing changes for the constrained layouts while retaining the normal desktop composition. Add a standalone splash page with packaged Three.js/GLTF assets and a testable startup coordinator; the app window starts on the splash and swaps to the existing POS after initialization and the minimum duration finish.

**Tech Stack:** .NET 8 MAUI/WinUI 3, C#, XAML, packaged JavaScript/GLTF assets, existing Verification console.

**Spec:** `docs/superpowers/specs/2026-09-26-responsive-pos-and-3d-splash-design.md`

## Global Constraints

- Preserve the normal desktop POS appearance and its existing business logic, persistence, prices and navigation.
- Keep 900 × 680 as the minimum supported app window and make required controls reachable at that size.
- Use only locally packaged model and renderer assets; add no NuGet 3D engine and require no network at runtime.
- Run POS initialization concurrently with a four-second minimum splash timer; do not add delay after slower initialization.
- Dispose of splash rendering resources at transition and retain the current startup error message behavior.
- Restore product instructions input without changing how orders are priced or saved.

## Review Focus

- Minimum-size window: category chips, payment overlay, and add-product controls must not clip; confirm with a 900 × 680 UI run.
- Short-height checkout: cash and mixed payment fields plus both actions must remain reachable by scroll.
- Startup failure: the splash must transition after the minimum time and surface the existing initialization error without starting database initialization twice.
- Offline startup and relaunch: local model loads, rotates, then releases; a second app launch repeats the flow.
- Product notes: configured instructions must reach the order and receipt while calculations remain unchanged.

## Files and responsibilities

- `Views/MainPage.xaml` and `.xaml.cs`: responsive categories, checkout scrolling, and preservation of the add-product form's existing scrolling.
- `App.xaml`: category control sizing so its requested minimum width no longer forces overflow.
- `Views/ProductConfiguratorPage.xaml` and `.xaml.cs`: restore user-entered instructions in the existing options scroll area and selection.
- `Services/StartupCoordinator.cs`: start initialization and minimum-duration delay together, await both, and return initialization failure without throwing away the splash transition.
- `Views/SplashPage.xaml` and `.xaml.cs`: branded splash UI and WebView renderer shutdown.
- `Resources/Raw/Splash/*`: local model, renderer entry page/scripts and their license notices; MAUI includes these as packaged assets by default.
- `App.xaml.cs` and `MauiProgram.cs`: register/start the splash flow and replace the startup page after the coordinated work completes.
- `Verification/Program.cs` and `.csproj`: exercise the startup coordinator's parallel wait and failure result; include instruction persistence coverage if a small production mapping seam is needed.

## Tasks

### Task 1: Make the main POS and checkout responsive

**Files:**
- Modify: `App.xaml`
- Modify: `Views/MainPage.xaml`
- Modify: `Views/MainPage.xaml.cs`
- Modify: `Views/ProductConfiguratorPage.xaml`
- Modify: `Views/ProductConfiguratorPage.xaml.cs`

**Interfaces:**
- Keep the existing `ProductSelection.Instructions` property as the transfer field into the order draft.
- Use the existing `MainPage.OnSizeAllocated` callback for width-dependent category sizing; retain its current two-pane composition and product-card span behavior.
- Keep the checkout's two columns at all supported widths, but place its body in a vertical `ScrollView` so low-height windows expose the complete payment choices and summary actions.
- Keep the add-product form's existing vertical `ScrollView` and fixed footer; verify it fits the supported minimum viewport without adding redundant scrolling.

- [ ] Give the three category buttons names in `MainPage.xaml`; in `App.xaml`, set their style's `MinimumWidthRequest` to zero; in `MainPage.xaml.cs`, set font size 13 and padding 8 below 1024 px, restoring font size 17 and padding 16 above the breakpoint.
- [ ] Name the checkout body grid in `MainPage.xaml`, wrap it in a vertical `ScrollView`, and reduce popup margins to 16 px below 1024 px while retaining the two-column checkout layout.
- [ ] Add an `InstructionsEntry` in the configurator's existing options `ScrollView` and set `ProductSelection.Instructions = InstructionsEntry.Text?.Trim()`.
- [ ] At 900 × 680 and 1440 × 900, confirm category labels do not overflow, all checkout actions are reachable, the add-product footer remains visible while form content scrolls, and one configured instruction appears in the cart and receipt.

### Task 2: Add the local 3D splash renderer

**Files:**
- Create: `Views/SplashPage.xaml`
- Create: `Views/SplashPage.xaml.cs`
- Create: `Resources/Raw/Splash/index.html`
- Create: `Resources/Raw/Splash/splash.js`
- Create: `Resources/Raw/Splash/three.module.js` and local GLTF loader module (or equivalent self-contained scripts)
- Create: `Resources/Raw/Splash/ice_cream.glb` and model/source license notes

**Interfaces:**
- `SplashPage.ReleaseRendererAsync()` stops the animation loop, disposes scene/renderer resources, and releases the WebView.
- The WebView reads every script/model from packaged assets and performs no runtime network request.

- [ ] Download and package the selected Quaternius GLTF/GLB model and the minimal Three.js modules with their license notices; use MAUI's default `Resources/Raw` asset packaging.
- [ ] Build the no-controls splash using the existing brand palette and `HELADERÍA VILLEGAS`; set a fixed camera, modest lights, 4–6 second Y rotation, and a 400–600 ms entrance animation.
- [ ] Verify the model renders from packaged assets with networking unavailable, then call `ReleaseRendererAsync()` and verify the loop is cancelled and WebView removed.

### Task 3: Coordinate app startup and transition

**Files:**
- Create: `Services/StartupCoordinator.cs`
- Modify: `MauiProgram.cs`
- Modify: `App.xaml.cs`
- Modify: `Views/MainPage.xaml.cs`
- Modify: `Verification/Verification.csproj`
- Modify: `Verification/Program.cs`

**Interfaces:**
- `StartupCoordinator.RunAsync(Func<Task> initializeAsync, TimeSpan minimumDuration, CancellationToken cancellationToken = default) -> Task<Exception?>` starts initialization and delay before awaiting both, returning any initialization exception for the UI to report.
- `MainViewModel.LoadAsync` remains the existing initialization entry point and must be invoked once per app startup.
- `MainPage.ShowStartupErrorAsync(Exception exception) -> Task` displays the existing startup alert after MainPage becomes visible.
- `App` owns the window transition and calls `SplashPage.ReleaseRendererAsync()` before replacing its page with the existing `NavigationPage(MainPage)`.

- [ ] Add Verification checks proving initialization starts before the minimum timer completes, completion waits for both tasks, and initialization errors are returned after the minimum duration.
- [ ] Run the new Verification checks and confirm they fail before adding `StartupCoordinator`.
- [ ] Implement `StartupCoordinator` and pass all Verification checks.
- [ ] Register `StartupCoordinator` in `MauiProgram.cs`; start the window on `SplashPage`; on `Window.Created`, run the coordinator with `MainViewModel.LoadAsync`; after it completes, fade the splash, release the renderer, install the existing POS page, and call `MainPage.ShowStartupErrorAsync` once if an error was captured. Remove data initialization from `MainPage.OnAppearing` so the coordinator is the only startup caller.
- [ ] Build the Windows target and launch twice. Confirm a splash duration of at least four seconds for fast startup, smooth fade and rotation, no duplicate database initialization, and a working second launch.

## Execution notes

- Execute inline in this session; do not delegate.
- Use the user's approved specification as binding scope. Do not change the POS database schema, prices, checkout calculations, sales semantics, or normal desktop styling.
- Expected build command from a directory outside the repo's `global.json` SDK lookup: `dotnet build C:\Users\Donnet\Desktop\HeladeriaVillegas\HeladeriaPOS.Maui.csproj -f net8.0-windows10.0.19041.0 --no-restore`.
- Run the existing Verification console and the new coordinator checks after each relevant task. Finish with the Windows build and a UI review at minimum and desktop window sizes.
