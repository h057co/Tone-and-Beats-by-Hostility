#!/bin/bash
# Tone & Beats - Dependency Setup Script for macOS/Linux
# This script clones and sets up all required dependencies

echo "Setting up Tone & Beats dependencies..."

# Check if git is available
if ! command -v git &> /dev/null; then
    echo "Error: Git is required but not found. Please install Git first."
    exit 1
fi

# Create modules directory if it doesn't exist
mkdir -p modules

echo "Cloning JUCE framework..."
if [ ! -d "modules/JUCE" ]; then
    git clone --depth 1 --branch master https://github.com/juce-framework/JUCE.git modules/JUCE
else
    echo "JUCE already exists, skipping..."
fi

echo "Cloning TagLib..."
if [ ! -d "modules/taglib" ]; then
    git clone --depth 1 --branch master https://github.com/taglib/taglib.git modules/taglib
else
    echo "TagLib already exists, skipping..."
fi

echo "Cloning libebur128..."
if [ ! -d "modules/libebur128" ]; then
    git clone --depth 1 --branch master https://github.com/jiixyj/libebur128.git modules/libebur128
else
    echo "libebur128 already exists, skipping..."
fi

echo "Cloning SoundTouch..."
if [ ! -d "modules/soundtouch" ]; then
    git clone --depth 1 --branch master https://codeberg.org/soundtouch/soundtouch.git modules/soundtouch
else
    echo "SoundTouch already exists, skipping..."
fi

echo ""
echo "Dependencies setup complete!"
echo ""
echo "To build the project:"
echo "  mkdir build && cd build"
echo "  cmake .. -DJUCE_DIR=../modules/JUCE"
echo "  cmake --build ."
echo ""

# Make this script executable
chmod +x setup_dependencies.sh