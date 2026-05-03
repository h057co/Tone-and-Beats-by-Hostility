@echo off
REM Tone & Beats - Dependency Setup Script for Windows/macOS/Linux
REM This script clones and sets up all required dependencies

echo Setting up Tone & Beats dependencies...

REM Check if git is available
where git >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo Error: Git is required but not found. Please install Git first.
    exit /b 1
)

REM Create modules directory if it doesn't exist
if not exist "modules" mkdir modules

echo Cloning JUCE framework...
if not exist "modules\JUCE" (
    git clone --depth 1 --branch master https://github.com/juce-framework/JUCE.git modules\JUCE
) else (
    echo JUCE already exists, skipping...
)

echo Cloning TagLib...
if not exist "modules\taglib" (
    git clone --depth 1 --branch master https://github.com/taglib/taglib.git modules\taglib
) else (
    echo TagLib already exists, skipping...
)

echo Cloning libebur128...
if not exist "modules\libebur128" (
    git clone --depth 1 --branch master https://github.com/jiixyj/libebur128.git modules\libebur128
) else (
    echo libebur128 already exists, skipping...
)

echo Cloning SoundTouch...
if not exist "modules\soundtouch" (
    git clone --depth 1 --branch master https://codeberg.org/soundtouch/soundtouch.git modules\soundtouch
) else (
    echo SoundTouch already exists, skipping...
)

echo.
echo Dependencies setup complete!
echo.
echo To build the project:
echo   mkdir build ^& cd build
echo   cmake .. -DJUCE_DIR=..\modules\JUCE
echo   cmake --build .
echo.
pause