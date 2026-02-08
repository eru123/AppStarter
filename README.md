# ⚡ AppStarter v2

**AppStarter** is a lightweight, modern Windows process manager and service runner. It allows you to manage, monitor, and automate scripts and console applications with ease.

![AppStarter Screenshot](https://raw.githubusercontent.com/placeholder/appstarter-screenshot.png)

## ✨ Features

- **Process Management**: Start, stop, and restart multiple processes from a single dashboard.
- **Auto-Restart**: Automatically restart processes if they crash or exit unexpectedly.
- **Scheduled Execution**: Run commands on application start or via custom cron expressions.
- **Logging**: Real-time console output monitoring and persistent log storage in a SQLite database.
- **Windows Service Mode**: Run AppStarter as a Windows Service to keep your processes running even when logged out.
- **Modern UI**: A clean, dark-themed WPF interface with a responsive design.
- **Portable Backups**: Export and Import your entire configuration (commands and database) into a single compressed `.asbak` file.
- **Single Executable**: Ships as a single, self-contained `.exe` file—no installer or .NET runtime installation required.
- **Custom Branding**: Supports a custom `icon.ico` for the taskbar and file explorer (simply place a file named `icon.ico` in the root folder before building).

## 🚀 Getting Started

### Prerequisites
- Windows 10/11 (x64)
- Administrator privileges (for Windows Service management)

### Running the App
Since AppStarter is compiled as a self-contained executable, you can simply run `AppStarter_v2.exe`. It will automatically create the necessary configuration folders in your `%ProgramData%\AppStarter` directory.

## 🛠️ Configuration & Backups

You can find the "File" menu in the top bar to:
- **Import Configuration**: Restore a previous environment from an `.asbak` file.
- **Export Configuration**: Create a compressed backup of your commands, logs, and settings.

## 🏗️ Building from Source

To build AppStarter and generate the single-file executables for both x64 and x86 architectures, simply run the included automation script:

```bash
build.bat
```

This script will:
1.  **Auto-increment** the build version in the project file.
2.  **Generate** single-file executables (`AppStarter.exe`) for:
    *   **win-x64**: Located in `./dist/x64`
    *   **win-x86**: Located in `./dist/x86`
3.  **Self-contain** all dependencies so the app runs without a .NET installation.

## 📂 Project Structure

- `MainWindow.xaml`: Main dashboard UI.
- `Services/`: Core logic for Process Management, Database, and Backups.
- `Models/`: Data structures for commands and logs.
- `ViewModels/`: MVVM pattern implementation for UI-logic separation.
- `build.bat`: Windows Build & Version automation script.

## 📄 License
This project is licensed under the MIT License.
