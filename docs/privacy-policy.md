# MorpheX Live Privacy Policy

**Effective date: September 9, 2026**

MorpheX Live ("MorpheX", "we", "us", or "our") is a lightweight live wallpaper application for Windows.

## Information We Collect

MorpheX Live does not require an account and does not intentionally collect personal information from users.

MorpheX is designed to process wallpaper files locally on the user's computer.

We do not intentionally collect:
- Names or contact information
- Account credentials
- Personal documents or wallpaper files
- Browsing history
- Personal usage profiles

## Desktop Widgets & Location Data

MorpheX Live includes optional desktop widgets. Most widgets operate strictly on local system data (e.g., CPU, RAM, GPU, audio levels, time, notes).

**Weather Widget:**
- When the Weather widget is active, MorpheX fetches forecast data from **Open-Meteo** (`api.open-meteo.com`) using geographic coordinates.
- If the user clicks **Auto-detect Location**, the application queries **ipapi.co** to determine approximate location (city name and approximate latitude/longitude based on public IP).
- Latitude, longitude, and city name are cached exclusively in the user's local `settings.json` file on their own computer.
- MorpheX does **not** collect, store, or transmit your location to any MorpheX servers or third-party advertisers. Users can manually specify custom coordinates or disable the Weather widget at any time.

## Telemetry and Analytics

MorpheX Live does not include background telemetry, analytics, or behavioral tracking.

The application does not send wallpaper content or personal files to us or any external servers.

## Wallpaper Files

Wallpaper files selected by the user are processed locally by the application for playback and thumbnail generation.

MorpheX does not upload users' wallpaper files to our servers.

## Updates

MorpheX checks for updates via the public **GitHub API** (`api.github.com/repos/MorpheusDark7/MorpheX/releases`). These requests transmit standard HTTP client headers (e.g. User-Agent) to GitHub's servers to determine whether a newer release is available.

## Third-Party Services

MorpheX interacts only with specific external services required for optional features:
- **GitHub** (`api.github.com`, `github.com`): Application release checking and installer updates.
- **ipapi.co**: Approximate IP-based geolocation, queried only when the user explicitly triggers "Auto-detect Location" in the Weather widget.
- **Open-Meteo** (`open-meteo.com`): Free, public weather forecasts, queried on a periodic schedule only while the Weather widget is enabled.

These third-party providers operate under their own privacy policies. MorpheX does not provide users' personal information to any third parties for advertising or profiling.

## Children's Privacy

MorpheX Live is not directed specifically at children and does not knowingly collect personal information from children.

## Changes to This Policy

We may update this Privacy Policy when the application or its data practices change. Any updated version will be published at this location with a new effective date.

## Contact

For privacy-related questions, please open an issue in the MorpheX GitHub repository:

https://github.com/MorpheusDark7/MorpheX/issues

---

**MorpheX Live**  
Developed by **MorpheusDark**
