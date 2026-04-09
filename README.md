# freETarget MAUI

[![.NET MAUI](https://img.shields.io/badge/.NET_MAUI-8.0-blue.svg)](https://dotnet.microsoft.com/en-us/apps/maui)
[![Android](https://img.shields.io/badge/Android-Supported-green.svg)](https://www.android.com/)

**freETarget MAUI** is a modern, cross-platform companion application designed specifically for the [freETarget](https://github.com/ten-point-nine/freETarget) open-source electronic scoring system. Built taking advantage of the .NET MAUI framework, it provides a stable, responsive, and visually premium interface optimized for Android tablets, allowing sport shooters to monitor and interact with their targets.

## 🎯 Features

* **Real-time Shot Visualization**: Accurately calculate and render bullet impacts on various target faces in real-time.
* **Stable TCP Connection**: Robust socket communication with the freETarget ESP32/Wi-Fi hardware over the local network.
* **Multiple Target Types**: Supports various official ISSF & NRA target profiles (10m Air Pistol, 10m Air Rifle, 50m Rifle, etc.).
* **Premium Dashboard UI**: A card-based, modern user interface displaying the current score, session statistics, and a scrollable list of up to 60 individual shots per string.
* **Hardware Controls**: Remotely manage target hardware features, including adjusting LED lighting intensity directly from the navigation bar.
* **History & Data Tracking**: Record shot metadata (X/Y coordinates, decimal scores, direction) using robust view models.

## 📱 Screenshots

*(Consider placing your application screenshots in a `/Docs` or `/Screenshots` folder and referencing them here)*
<!-- ![Dashboard Preview](Docs/dashboard_preview.png) -->

## 🚀 Getting Started

### Prerequisites

* **IDE**: Visual Studio 2022 setup with the **.NET Multi-platform App UI development** workload installed.
* **SDK**: .NET 8.0 SDK.
* **Hardware**: An Android device or Android Emulator running Android 7.0 (API 24) or higher (Optimized for tablet horizontal orientations).

### Installation & Build

1. Clone the repository:
   ```bash
   git clone https://github.com/luisleon757/freetarget-android.git
   ```
2. Navigate to the project root and open `freETargetMAUI.slnx` using Visual Studio.
3. Use the startup drop-down menu to select the `net8.0-android` framework and your connected physical device or emulator.
4. Build, deploy, and Run!

## 🛠 Architecture & Tech Stack

This codebase is a complete port and modernization of a legacy Windows Forms C# application. Key aspects include:
* **UI**: XAML-based adaptive UI leveraging structural components like `Grid`, `CollectionView`, and `Border`. Unified theming via MAUI ResourceDictionaries.
* **Graphics**: Utilizes `Microsoft.Maui.Graphics` (specifically `IDrawable` inside `GraphicsView`) to draw SVG-like rendering of precision target rings and dynamic shot impact holes (`TargetDrawable.cs`).
* **Connection**: Advanced asynchronous stream processing (`TargetConnectionService.cs`) utilizing TCP Client to read JSON-driven instructions output by the FreETarget hardware.

## 🤝 Contributing

Contributions, feedback, and issue reporting are absolutely welcome. Feel free to fork the repository, make your changes, and submit a pull request!

## 📄 License

This project is licensed under the [MIT License](LICENSE) - see the LICENSE file for more information.
