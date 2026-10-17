<p align="center">
  <img src="src/MorpheX/Assets/Branding/morphex-logo.png" width="160" alt="MorpheX Live Logo" />
</p>

<h1 align="center">MorpheX Live</h1>

<p align="center">
  A fast, lightweight live wallpaper engine for Windows 10 and 11.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D6?style=flat-square" alt="Platform" />
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square" alt=".NET 8" />
  <img src="https://img.shields.io/badge/DirectX-Direct3D%2011-green?style=flat-square" alt="DirectX 11" />
  <img src="https://img.shields.io/badge/version-0.1.55-orange?style=flat-square" alt="Version" />
  <img src="https://img.shields.io/badge/license-MIT-blue?style=flat-square" alt="License" />
</p>

---

## Why MorpheX?

Most live wallpaper apps out there chew up 500MB to 1GB of memory and keep your GPU running hot even when you have a game running.

I built **MorpheX** to have something clean, minimal, and resource-friendly:
- When you are on your desktop, you get buttery-smooth 60fps video playback with hardware acceleration.
- The instant you launch a fullscreen game or open a work app, playback pauses immediately and drops to **0.0% CPU and 0.0% GPU**.
- It sits quietly in your system tray without spamming notifications or running background telemetry.

---

## Features

- **Built-in Master CC0 Wallpapers**: Ships with a curated collection of stunning high-resolution CC0 wallpapers out-of-the-box so your desktop looks incredible immediately upon installation.
- **Dedicated Desktop Profiles Hub**: Dedicated page to create, rename, save, and switch between customized desktop profiles (e.g., *Productivity*, *Cyberpunk Setup*, *Late Night Chill*) in a single click from the UI or right from the system tray. Safely restores widget configurations, styling, and wallpaper pairings with graceful fallback if a wallpaper was removed.
- **Dedicated Effects & Appearance Page**: Complete control over visual filters (Brightness, Contrast, Saturation, Color Temperature, Reset), Video Playback speeds, and Ambient Dimming in a clean, uncluttered interface.
- **Polished Fluent Vector UI**: Native Segoe Fluent icons, vector layout diagrams, and styled typography badges across all widget pickers, dropdowns, and settings.
- **Direct3D 11 Hardware Acceleration**: Powered by LibVLC with native GPU decoding (`.mp4`, `.webm`, `.mov`, `.mkv`, `.avi`).
- **Smart Pause Detection**: Automatically freezes video playback when running fullscreen games or applications so your frame rates never take a hit.
- **Multi-Monitor Support**: Assign different wallpapers to each display, adjust volume independently per monitor, or mute individual screens.
- **Wallpaper Playlist & Auto-Rotation**: Shuffle or cycle through your library on a customizable timer. Filter by favorites. Postpones rotation while in a game.
- **Desktop Widgets**: Transparent, drag-and-drop overlays with custom scaling, frosted card options, and dark/light themes.
- **Real-Time Weather Widget**: Live temperature, condition, high/low, humidity, and wind speed powered by free Open-Meteo API (no API key required). Features dynamic Google Weather-style background gradients, automatic IP geolocation or city search, and crisp vector weather icons.
- **Audio Visualizer**: Live audio reactive spectrum analyzer (WASAPI loopback) with vector styles (Equalizer Bars, Mirrored Center, and Smooth Waveform Curve) and vibrant gradient theme presets.
- **Minimalist Clock & Day Widget**: Sleek typography with designer fonts (Anurati, Bahnschrift, Segoe UI, Consolas) and glowing drop shadows.
- **System Monitor Widget (SysMon)**: Real-time hardware rings displaying CPU, GPU, RAM, and Disk metrics with horizontal row, vertical column, and 2x2 grid vector presets.
- **Custom Global Hotkeys**: Set keyboard shortcuts to pause/resume, mute/unmute, or skip wallpapers from anywhere in Windows.
- **Native Windows Shell Integration**: Embeds cleanly behind your desktop icons using the `WorkerW` layer without interfering with desktop clicks.
- **Secure 1-Click Built-in Updates**: Check for and install updates directly inside the app with URL origin validation, SHA-256 integrity verification, and safe executable staging.
- **Fast Startup**: Optimized desktop integration initialization — typically under 1 second on most systems.

---

## Installation

### Installer (Recommended)
Grab the latest setup from the [**Releases**](https://github.com/MorpheusDark7/MorpheX/releases) page:
1. Download `MorpheX-Live-Setup-v*.exe`.
2. Run the installer and click Next.
3. Launch MorpheX from your Start Menu or Desktop.

*(Future updates can be installed with a single click right inside the app settings.)*

---

## Supported Formats

| Format | Extensions | Engine |
|---|---|---|
| **Video** | `.mp4`, `.webm`, `.mkv`, `.mov`, `.avi` | LibVLC with Direct3D 11 GPU decode |
| **Static Images** | `.png`, `.jpg`, `.jpeg`, `.webp`, `.bmp` | Fast WIC image loader |

---

## Building from Source

### Requirements
- Windows 10 (1809+) or Windows 11 (64-bit)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022, JetBrains Rider, or VS Code

### Steps
```powershell
# 1. Clone the repository
git clone https://github.com/MorpheusDark7/MorpheX.git
cd MorpheX

# 2. Restore NuGet dependencies
dotnet restore

# 3. Build in Release mode
dotnet build -c Release

# 4. (Optional) Publish a self-contained single executable
dotnet publish src/MorpheX/MorpheX.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish
```

To compile the setup installer, open `installer.iss` in [Inno Setup 6](https://jrsoftware.org/isinfo.php) and click **Compile**.

---

## Architecture Overview

```
MorpheX/
├── src/
│   ├── MorpheX/             # WPF Front-end UI (MVVM, Fluent Design, Views, Widgets)
│   └── MorpheX.Core/        # Core Engine
│       ├── Configuration/   # App settings & library manifests
│       ├── Desktop/         # Win32 WorkerW shell integration & host windows
│       ├── Detection/       # Fullscreen, battery, and Explorer restart watchers
│       ├── Models/          # Monitor, wallpaper, and performance data models
│       ├── Providers/       # Video (LibVLC) and image wallpaper providers
│       ├── Services/        # Playback, playlist, audio, metrics, and update services
│       └── Utilities/       # Win32 memory trimming & shell thumbnail helpers
├── installer.iss            # Inno Setup packaging script
└── .github/workflows/       # GitHub Actions CI/CD pipeline
```

---

## Changelog

### v0.1.55
- **Dynamic System Accent Theme Adaptation** — Real-time Windows 11 system accent color synchronization (e.g. Razer Green `#44D62C`, Slate Blue, Purple) with automated WCAG contrast protection that dynamically flips button text to dark charcoal `#0F172A` on bright/neon accents and crisp white `#FFFFFF` on deep tones.
- **Subtle Opacity Accent Styling** — Introduced refined translucent 15% opacity accent washes (`MxAccentSubtleBrush`) and 35% accent borders so vibrant accent themes never overpower dark desktop aesthetics.
- **Video Resolution & Metadata Probing Fix** — Resolved an asynchronous LibVLC probing race condition so video wallpaper resolutions (4K, 1440p, 1080p) and durations reliably populate on Library and Favorites cards with reactive `INotifyPropertyChanged` UI updates.
- **Clean System Status Bar** — Removed the application working set memory pill (`APP: 0 MB`) from the bottom status bar and set system metrics to disabled by default, keeping background resource usage minimal.

### v0.1.54
- **Active Internet Connection Warning** — Added a distinct red warning badge (`[ 🌐 Requires Internet ]`) to the Weather Widget in Widgets settings so users immediately know network connectivity is required.
- **Video Resolution Detection** — Added hardware video track probing using LibVLC to accurately detect and display video resolution (e.g. 1920x1080, 2560x1440, 3840x2160) in the Library and Favorites cards for all video wallpapers on import and retroactively.
- **Rich Smoked Glass & Contrast Boost** — Overhauled frosted dark card backgrounds to rich smoked glass (80% opacity matching Weather) and boosted typography contrast so all widgets are crisp and legible over bright anime or light wallpapers.
- **Notes Widget Interaction Fix** — Moved window drag handling strictly to the header title bar, eliminating text selection interference and enabling smooth, reliable typing and editing in desktop sticky notes.
- **Aesthetic Color Palettes** — Introduced popular curated color presets (Soft Lavender `#A78BFA`, Rose Pink `#FB7185`, Frost Sky `#89B4FA`, Crimson Red `#F43F5E`) inspired by Catppuccin, JaxCore, and Rainmeter without altering existing profile color indexes.
- **Weekday & Custom Text Color Immunity** — Decoupled the weekday/custom text card from global theme flipping so chosen custom colors are always preserved.
- **Clean Context Menus & UI Alignment** — Removed legacy emojis and redundant theme toggles from right-click widget menus, centered the active profile badge, and enhanced profile count visibility.

### v0.1.53
- **Independent Widget Profiles & Live Positioning** — Desktop profiles now reliably save, restore, and reposition all desktop widgets (Clock, System Monitor, Media Visualizer, Day, Calendar, Notes, Quote, Weather) with independent coordinates and configurations per profile.
- **Immediate Drag-and-Drop Auto-Save** — Moving widgets across your desktop now automatically persists the latest coordinates in real-time to both configuration and the active profile.
- **Responsive Library Header** — Redesigned the Library page header into a two-tier layout so the title and wallpaper count are never clipped or hidden when the application is windowed.
- **Theme Harmonization & Icon Polish** — Aligned Lock widget positions border and icon styling with Desktop Profiles cards, and updated Favorites page heart icons and card badges to match MorpheX green accent.

### v0.1.52
- **Dedicated Desktop Profiles Hub** — Moved Profiles out of widgets into its own dedicated full-page dashboard (`ProfilesPage`) with hero active banner, tag summaries, instant switching, and wallpaper presets.
- **Dedicated Effects & Appearance Page** — Separated visual filters (Brightness, Contrast, Saturation, Color Temperature, Reset), playback speed rate, and ambient dimming into an independent navigation page (`EffectsPage`).
- **Vector UI & Fluent Visuals** — Replaced legacy unicode characters and emojis with clean vector layout previews (Horizontal Row, Vertical Column, 2x2 Grid), font preview badges (`Aa`, `08`, `>_`, `MON`), casing chips (`AA`, `Aa`, `aa`), and native Segoe Fluent icons (`SymbolIcon`).
- **Library Hover Card Fix** — Removed the oversized semi-transparent hover preview tooltip in Library that caused text bleed-through and obscured adjacent wallpaper cards.
- **Clean Lightweight Engine** — Removed experimental taskbar hooks and background SWCA loops, keeping MorpheX 100% stable, safe, and zero-overhead.

### v0.1.51
- **Bundled CC0 Master Wallpapers** — Ships with high-resolution curated CC0 wallpapers out-of-the-box for a rich first-run experience.
- **Desktop Profiles System** — Core profiles engine saving and restoring per-monitor wallpapers, taskbar styles, and widget configurations with missing-file safety fallbacks.
- **Updater Hardening & SHA-256 Sidecar** — Automated `.sha256` checksum generation in CI and strict mandatory checksum verification before executing update installers.

### v0.1.50
- **Automated Update Pipeline** — Integrated release checks with staging path containment verification.
- **Performance Hardening** — Minimized background memory and CPU impact with efficient timer scheduling.

### v0.1.38
- **Simplified Format Support** — Deprecated GIF and Scene package formats in favor of high-performance, GPU-accelerated video formats (`.mp4`, `.webm`, `.mov`, `.mkv`, `.avi`) and static wallpapers.
- **Bulk Library Management** — Added a "Delete All" button in the wallpaper library to clear imported media in one click.
- **Modern Dialogs & UI Polish** — Replaced native Windows message boxes with custom Fluent dark dialogs and text prompts across all widgets and settings.
- **Battery & Widget Optimizations** — Stabilized battery pause detection, refined widget context menus, and cleaned up filter options.

### v0.1.31
- **Windows 11 Taskbar Fix** — Implemented an automatic 900ms DWM composition refresher loop, corrected unmanaged memory layout, and set `AccentFlags = 2` to keep Clear, Blur, and Acrylic taskbar effects persistently active against Windows 11 shell repaints.
- **Light Theme for Widgets** — Added a dedicated Light Mode option for widgets (Notes, Daily Quotes, Calendar, Clock, System Monitor, and Media). Renders frosted ivory/cream glass with high-contrast typography optimized for bright wallpapers.
- **Widget Context Menu Theme Switcher** — Quickly toggle between Dark and Light mode directly from widget right-click menus or from the Widgets settings page.

### v0.1.30
- **Taskbar Styling** — Built-in Clear, Blur, and Acrylic taskbar effects (primary + all secondary monitors). Resets to default when MorpheX closes.
- **Faster Startup** — Reduced WorkerW initialization overhead: single spawn message, shorter sleep intervals, and fewer retry attempts.
- **Widget Tips** — Cleaned up the Desktop Tips card in the Widgets page.
- **CI Fix** — Fixed GitHub Actions deploy step that was dropping the installer before copying to `docs/`.

### v0.1.29
- Fixed CI deploy step (copy installer to TEMP before branch switch).

### v0.1.28
- Toggleable neon glow for the audio visualizer.
- Frosted glass acrylic background option for the Media widget.
- Optimized installer size by stripping unused VLC architecture runtimes.
- Auto-deploy installer to GitHub Pages for direct download.

### v0.1.27
- Acrylic frosted glass widget backgrounds.
- Multi-GPU engine tracking (dGPU load for discrete GPUs).
- Hide resize grips when widgets are locked.
- Mond / Anurati font support for Day widget.

---

## License

Released under the [MIT License](LICENSE). Free for personal and commercial use.
