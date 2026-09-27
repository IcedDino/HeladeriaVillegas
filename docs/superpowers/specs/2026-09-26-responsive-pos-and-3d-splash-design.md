# Heladería POS responsive layout and 3D startup splash

## Goal

Make the existing Windows .NET MAUI POS usable at its supported minimum window size and add an offline 3D welcome splash that runs while the existing POS startup initialization completes.

## Current application

- The app targets .NET 8 MAUI for Windows (`net8.0-windows10.0.19041.0`) and currently allows a 900 × 680 minimum window.
- `App.CreateWindow` immediately creates a `NavigationPage` containing `MainPage`.
- `MainPage.OnAppearing` calls `MainViewModel.LoadAsync`; that loads/initializes the database, products, current draft and daily backup.
- MainPage uses a fixed 420/460 px cart column. Category buttons request a 140 px minimum width. Checkout and product dialogs have fixed two-column content without a short-height reflow.
- `ProductConfiguratorPage` has a vertically scrollable options area, but the recent redesign removed its product-instructions entry even though the model and receipt still support instructions.
- The visible receipt branding is `HELADERÍA VILLEGAS`; the application/window title is `Heladería POS`.

## Requirements

### Responsive POS

- Preserve the current visual style and the current two-pane composition at normal desktop sizes.
- Keep the supported 900 × 680 minimum window usable. No required control may be clipped or inaccessible at that size.
- Remove the category button minimum-width overflow at narrow widths and let labels wrap or scale within their grid cells.
- Adapt checkout and add-product overlays when the available width or height is constrained. Their fields and actions must remain reachable by scrolling; at normal desktop dimensions they should retain their current side-by-side presentation.
- Keep the configurator's scrollable options and fixed action footer usable at minimum window height.
- Restore the product-instructions field and pass its value into `ProductSelection.Instructions`, preserving the existing order and receipt support.
- Do not change POS calculations, persistence, prices, navigation destinations or business workflows.

### Startup splash

- Show a non-interactive splash first, using the existing brand palette and the existing business name `HELADERÍA VILLEGAS`.
- Render a Quaternius ice-cream GLB locally. Use a lightweight local WebView/Three.js renderer because the project is MAUI/WinUI 3 and has no existing native glTF renderer. Bundle all runtime scripts and model data with the app; do not require network access at launch and do not add a NuGet 3D engine.
- Rotate the model smoothly around its vertical axis, approximately one revolution every 4–6 seconds, with a fixed camera and modest lighting. A short 400–600 ms fade/scale entrance is allowed.
- Start the existing `MainViewModel.LoadAsync` initialization while the splash is visible. Run it concurrently with a four-second minimum-duration timer. Show the POS only after both have completed; if startup takes longer, do not add delay beyond its completion.
- Fade out briefly before displaying the existing POS. Preserve the existing startup-error behavior while ensuring initialization is not accidentally repeated on page appearance.
- When leaving the splash, stop its animation loop, dispose of renderer resources, and release the WebView so no 3D rendering continues in the background.
- Do not redesign the POS or change the database schema, sales logic, product pricing, or unrelated features.

## Architecture

- Keep the splash view separate from startup orchestration. The app window initially hosts a dedicated `SplashPage`; a small startup coordinator starts the existing view-model load task and the minimum-duration timer together, handles initialization completion/failure, then requests the transition to the existing `MainPage`.
- Keep the model, HTML, JavaScript renderer and any required local texture files under the existing resource tree in a packaged asset folder. Include the renderer's license notice with the vendored runtime script.
- Keep responsive behavior in the relevant XAML layouts and page sizing logic. Use the existing minimum window dimensions as the primary acceptance size rather than changing the POS layout at normal desktop sizes.
- No new NuGet dependency is expected. The GLB and JavaScript renderer are offline app assets.

## Acceptance checks

1. Build the complete Windows MAUI target with the repository's supported SDK/toolchain.
2. At 900 × 680, the category controls, checkout inputs/actions and add-product form have no clipped mandatory content; constrained overlays can scroll.
3. At a normal desktop size, the current POS composition and styling remain intact.
4. With startup completing in under four seconds, the splash remains visible for at least four seconds. If initialization takes longer, the main page appears as soon as initialization finishes.
5. The model loads with networking disabled, rotates smoothly, and is gone with the splash after transition.
6. Closing and relaunching the app repeats the splash and reaches the existing POS flow.
7. Product instructions can again be entered and appear in the order/receipt data.

## Asset source

The requested Quaternius `Ice Cream` model is listed as GLTF/CC0 on Poly Pizza: https://poly.pizza/m/iZiAPq7Sne. Quaternius publishes its current asset license at https://quaternius.com/license.html. The downloaded GLB must be packaged as an application asset; it must not be fetched at runtime.
