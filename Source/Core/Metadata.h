#pragma once

#include <JuceHeader.h>
#include "Constants.h"

namespace ToneAndBeats
{

class MetadataWriter
{
public:
    static bool writeMetadata(const File& file, double bpm, const String& key, const String& mode);
    static bool writeMetadata(const String& filePath, double bpm, const String& key, const String& mode);

    static std::tuple<bool, String, String> getCurrentMetadata(const File& file);
    static std::tuple<bool, String, String> getCurrentMetadata(const String& filePath);
};

} // namespace ToneAndBeats