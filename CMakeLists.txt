cmake_minimum_required(VERSION 3.22)
project("Tone And Beats By Hostility" VERSION 1.2.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

if(APPLE)
    set(CMAKE_OSX_DEPLOYMENT_TARGET "11.0" CACHE STRING "Minimum OS X deployment version" FORCE)
endif()

option(TONE_AND_BEATS_USE_LIBSOUNDTOUCH "Use SoundTouch for BPM detection" ON)

# External Libraries
set(BUILD_EXAMPLES OFF CACHE BOOL "Build TagLib examples")
set(BUILD_TESTING OFF CACHE BOOL "Build TagLib tests")
set(BUILD_BINDINGS OFF CACHE BOOL "Build TagLib bindings")
set(WITH_ZLIB OFF CACHE BOOL "Build TagLib with ZLIB")
add_subdirectory(modules/taglib)

set(ENABLE_TESTS OFF CACHE BOOL "Build libebur128 tests")
add_subdirectory(modules/libebur128)

add_subdirectory(modules/soundtouch)

set(JUCE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/modules/JUCE")
if(EXISTS "${JUCE_DIR}/CMakeLists.txt")
    add_subdirectory(${JUCE_DIR} juce)
    set(JUCE_FOUND TRUE)
endif()

find_package(Threads REQUIRED)

if(NOT JUCE_FOUND)
    message(FATAL_ERROR "JUCE framework not found")
endif()

juce_add_gui_app(ToneAndBeats
    PRODUCT_NAME "Tone And Beats By Hostility"
    COMPANY_NAME "Hostility"
    VERSION ${PROJECT_VERSION}
    IS_STANDALONE ON
    ICON_BIG "Assets/Icono.jpg"
    ICON_SMALL "Assets/Icono.jpg"

    JUCE_USE_FLAC ON
    JUCE_USE_OGGVORBIS ON
    JUCE_USE_MP3DLL OFF
    JUCE_USE_LAME OFF
)

juce_add_binary_data(AssetsData
    SOURCES
        "Assets/qrdonaciones.png"
        "Assets/Icono.jpg"
        "Assets/HOST_BLANCO.png"
        "Assets/HOST_NEGRO.png"
)

juce_generate_juce_header(ToneAndBeats)

target_include_directories(ToneAndBeats PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/Source"
    "${CMAKE_CURRENT_SOURCE_DIR}/Source/Core"
    "${CMAKE_CURRENT_SOURCE_DIR}/Source/UI"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/taglib/taglib"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/taglib/taglib/toolkit"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/taglib/taglib/mpeg"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/taglib/taglib/mpeg/id3v2"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/taglib/taglib/mpeg/id3v2/frames"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/taglib/taglib/riff"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/taglib/taglib/riff/wav"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/taglib/taglib/riff/aiff"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/libebur128/ebur128"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/soundtouch/include"
)

target_sources(ToneAndBeats PRIVATE
    Source/Main.cpp
    Source/MainComponent.cpp
    Source/Core/AudioDataProvider.cpp
    Source/Core/BpmDetector.cpp
    Source/Core/KeyDetector.cpp
    Source/Core/Loudness.cpp
    Source/Core/Metadata.cpp
    Source/Core/WaveformData.cpp
    Source/Core/ScalePlayerSource.cpp
    Source/UI/LookAndFeel.cpp
    Source/UI/WaveformView.cpp
    Source/UI/PianoScaleView.cpp
    Source/Core/DspUtils.cpp
    Source/Core/UpdateManager.cpp
    Source/Core/UpdateManager.h
)

target_link_libraries(ToneAndBeats
    PRIVATE
        juce::juce_audio_formats
        juce::juce_audio_devices
        juce::juce_audio_utils
        juce::juce_gui_extra
        juce::juce_dsp
        SoundTouch
        tag
        ebur128
        AssetsData
    PUBLIC
        juce::juce_recommended_config_flags
        juce::juce_recommended_lto_flags
        juce::juce_recommended_warning_flags)

if(WIN32)
    set_target_properties(ToneAndBeats PROPERTIES
        OUTPUT_NAME "Tone And Beats By Hostility"
        WIN32_EXECUTABLE ON
    )
else()
    set_target_properties(ToneAndBeats PROPERTIES
        OUTPUT_NAME "Tone And Beats By Hostility"
    )
endif()

install(TARGETS ToneAndBeats DESTINATION bin)

# === BPM Test Harness (CLI) ===
juce_add_console_app(BpmTestHarness
    PRODUCT_NAME "BPM Test Harness"
    COMPANY_NAME "Hostility"
    VERSION ${PROJECT_VERSION}
)

juce_generate_juce_header(BpmTestHarness)

target_include_directories(BpmTestHarness PRIVATE
    "${CMAKE_CURRENT_SOURCE_DIR}/Source"
    "${CMAKE_CURRENT_SOURCE_DIR}/Source/Core"
    "${CMAKE_CURRENT_SOURCE_DIR}/Source/UI"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/taglib/taglib"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/taglib/taglib/toolkit"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/taglib/taglib/mpeg"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/taglib/taglib/mpeg/id3v2"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/taglib/taglib/mpeg/id3v2/frames"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/taglib/taglib/riff"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/taglib/taglib/riff/wav"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/taglib/taglib/riff/aiff"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/libebur128/ebur128"
    "${CMAKE_CURRENT_SOURCE_DIR}/modules/soundtouch/include"
)

target_sources(BpmTestHarness PRIVATE
    Source/BpmTestHarness.cpp
    Source/Core/AudioDataProvider.cpp
    Source/Core/BpmDetector.cpp
    Source/Core/DspUtils.cpp
    Source/Core/WaveformData.cpp
)

target_link_libraries(BpmTestHarness
    PRIVATE
        juce::juce_audio_formats
        juce::juce_audio_devices
        juce::juce_audio_utils
        juce::juce_gui_extra
        juce::juce_dsp
        SoundTouch
        tag
        ebur128
    PUBLIC
        juce::juce_recommended_config_flags)

set_target_properties(BpmTestHarness PROPERTIES
    OUTPUT_NAME "BpmTestHarness"
)

message(STATUS "Tone & Beats v${PROJECT_VERSION} configured successfully")