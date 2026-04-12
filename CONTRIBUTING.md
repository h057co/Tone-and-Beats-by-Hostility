# Contributing to Tone & Beats by Hostility

Thank you for your interest in contributing to Tone & Beats!

**Note:** This project is currently **closed to external contributions**. This is a personal project by Hostility (Luis Jiménez) developed in Medellín, Colombia.

However, bug reports and feature suggestions are welcome through GitHub Issues.

---

## Development Setup

### Prerequisites

- Windows 10/11 (x64)
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code (recommended)
- Inno Setup 6 (for installer creation)

### Local Development

```bash
# Clone the repository
git clone https://github.com/h057co/Tone-and-Beats-by-Hostility.git
cd Tone-and-Beats-by-Hostility

# Navigate to source
cd src

# Build debug
dotnet build -c Debug

# Run
dotnet run

# Build release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../publish
```

### Project Structure

```
src/
├── Services/      # Business logic (BPM, Key, Waveform analysis)
├── ViewModels/    # MVVM ViewModels
├── Models/        # Data models
├── Controls/      # Custom WPF controls
├── Themes/        # Visual themes
├── Infrastructure/# Support classes
├── Interfaces/    # Service contracts
├── Commands/      # MVVM commands
└── Helpers/       # Utility helpers
```

### Code Standards

1. **MVVM Pattern:** All UI logic goes in ViewModels
2. **Interface Segregation:** Use interfaces for all services
3. **Dependency Injection:** Register services in `App.xaml.cs`
4. **Async/Await:** Use native async for I/O operations
5. **Logging:** Use `LoggerService` for all logging

### Testing

```bash
# Run performance tests
cd src
dotnet build -c Debug BpmTest/BpmTest.csproj
dotnet run --project BpmTest/BpmTest.csproj
```

---

## Bug Reports

When reporting bugs, please include:

1. **Version:** Current app version (About window)
2. **Steps to reproduce:** Clear, numbered steps
3. **Expected behavior:** What should happen
4. **Actual behavior:** What actually happens
5. **Audio file:** If applicable (file format, duration)

---

## Feature Requests

Feature suggestions are welcome but may not be implemented depending on project direction.

---

## License

By contributing, you agree that your contributions will be licensed under the same CC BY-NC-ND 4.0 license as the project.

---

*Developed by Luis Jiménez (Hostility) - Medellín, Colombia*  
*info@hostilitymusic.com*
