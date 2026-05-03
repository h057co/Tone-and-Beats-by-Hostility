#include "Metadata.h"

// TagLib includes
#include <fileref.h>
#include <tag.h>
#include <tstring.h>
#include <mpeg/mpegfile.h>
#include <mpeg/id3v2/id3v2tag.h>
#include <mpeg/id3v2/frames/textidentificationframe.h>
#include <mpeg/id3v2/frames/commentsframe.h>
#include <riff/wav/wavfile.h>
#include <riff/rifffile.h>
#include <riff/aiff/aifffile.h>

namespace ToneAndBeats
{

static juce::String getCamelotNotation(const juce::String& key, const juce::String& mode)
{
    juce::String k = key + " " + mode;
    if (k == "B Major") return "1B";
    if (k == "F# Major") return "2B";
    if (k == "C# Major" || k == "Db Major") return "3B";
    if (k == "G# Major" || k == "Ab Major") return "4B";
    if (k == "D# Major" || k == "Eb Major") return "5B";
    if (k == "A# Major" || k == "Bb Major") return "6B";
    if (k == "F Major") return "7B";
    if (k == "C Major") return "8B";
    if (k == "G Major") return "9B";
    if (k == "D Major") return "10B";
    if (k == "A Major") return "11B";
    if (k == "E Major") return "12B";

    if (k == "G# Minor" || k == "Ab Minor") return "1A";
    if (k == "D# Minor" || k == "Eb Minor") return "2A";
    if (k == "A# Minor" || k == "Bb Minor") return "3A";
    if (k == "F Minor") return "4A";
    if (k == "C Minor") return "5A";
    if (k == "G Minor") return "6A";
    if (k == "D Minor") return "7A";
    if (k == "A Minor") return "8A";
    if (k == "E Minor") return "9A";
    if (k == "B Minor") return "10A";
    if (k == "F# Minor") return "11A";
    if (k == "C# Minor" || k == "Db Minor") return "12A";

    return "";
}

static void applyID3v2Tags(TagLib::ID3v2::Tag* id3v2tag, double bpm, const juce::String& key, const juce::String& mode)
{
    if (!id3v2tag) return;

    juce::String camelot = getCamelotNotation(key, mode);
    juce::String fullKey = key + " " + (mode == "Major" ? "Maj" : "Min");
    if (camelot.isNotEmpty()) fullKey += " (" + camelot + ")";

    // 1. BPM (TBPM)
    // Remove existing TBPM frames to avoid duplicates
    auto bpmFrames = id3v2tag->frameList("TBPM");
    for (auto* f : bpmFrames) id3v2tag->removeFrame(f);
    
    TagLib::ID3v2::TextIdentificationFrame* bpmFrame = 
        new TagLib::ID3v2::TextIdentificationFrame("TBPM", TagLib::String::Latin1);
    bpmFrame->setText(TagLib::String(juce::String(bpm, 1).toRawUTF8()));
    id3v2tag->addFrame(bpmFrame);

    // 2. KEY (TKEY)
    auto keyFrames = id3v2tag->frameList("TKEY");
    for (auto* f : keyFrames) id3v2tag->removeFrame(f);

    TagLib::ID3v2::TextIdentificationFrame* keyFrame = 
        new TagLib::ID3v2::TextIdentificationFrame("TKEY", TagLib::String::Latin1);
    keyFrame->setText(TagLib::String(fullKey.toRawUTF8()));
    id3v2tag->addFrame(keyFrame);

    // 3. COMMENT (COMM) - Includes Camelot for quick search
    auto commFrames = id3v2tag->frameList("COMM");
    for (auto* f : commFrames) id3v2tag->removeFrame(f);

    TagLib::ID3v2::CommentsFrame* commFrame = new TagLib::ID3v2::CommentsFrame(TagLib::String::UTF8);
    commFrame->setText(TagLib::String(("BPM:" + juce::String(bpm, 1) + " KEY:" + fullKey).toRawUTF8()));
    id3v2tag->addFrame(commFrame);
}

bool MetadataWriter::writeMetadata(const File& file, double bpm, const String& key, const String& mode)
{
    return writeMetadata(file.getFullPathName(), bpm, key, mode);
}

bool MetadataWriter::writeMetadata(const String& filePath, double bpm, const String& key, const String& mode)
{
    try
    {
        bool success = false;

        // MP3 Support
        if (filePath.endsWithIgnoreCase(".mp3"))
        {
            TagLib::MPEG::File mpegFile(filePath.toRawUTF8());
            if (mpegFile.isValid())
            {
                applyID3v2Tags(mpegFile.ID3v2Tag(true), bpm, key, mode);
                success = mpegFile.save();
            }
        }
        // WAV Support (ID3v2 Chunk)
        else if (filePath.endsWithIgnoreCase(".wav"))
        {
            TagLib::RIFF::WAV::File wavFile(filePath.toRawUTF8());
            if (wavFile.isValid())
            {
                applyID3v2Tags(wavFile.ID3v2Tag(), bpm, key, mode);
                success = wavFile.save();
            }
        }
        // AIFF Support (ID3v2 Chunk)
        else if (filePath.endsWithIgnoreCase(".aiff") || filePath.endsWithIgnoreCase(".aif"))
        {
            TagLib::RIFF::AIFF::File aiffFile(filePath.toRawUTF8());
            if (aiffFile.isValid())
            {
                applyID3v2Tags(aiffFile.tag(), bpm, key, mode); // AIFF TagLib tag() is ID3v2
                success = aiffFile.save();
            }
        }
        // Generic Fallback
        else
        {
            TagLib::FileRef f(filePath.toRawUTF8());
            if (!f.isNull() && f.tag())
            {
                juce::String camelot = getCamelotNotation(key, mode);
                juce::String comm = "BPM:" + juce::String(bpm, 1) + " KEY:" + key + " " + mode;
                if (camelot.isNotEmpty()) comm += " (" + camelot + ")";
                
                f.tag()->setComment(TagLib::String(comm.toRawUTF8()));
                success = f.save();
            }
        }

        return success;
    }
    catch (const std::exception& e)
    {
        juce::Logger::writeToLog("MetadataWriter Exception: " + juce::String(e.what()));
        return false;
    }
}

std::tuple<bool, String, String> MetadataWriter::getCurrentMetadata(const File& file)
{
    return getCurrentMetadata(file.getFullPathName());
}

std::tuple<bool, String, String> MetadataWriter::getCurrentMetadata(const String& filePath)
{
    try
    {
        TagLib::FileRef f(filePath.toRawUTF8());
        if (!f.isNull() && f.tag())
        {
            // Simple check in the generic tag
            juce::String comment = juce::String(f.tag()->comment().to8Bit(true));
            // simplified parsing logic could go here
        }
    }
    catch (...) {}
    
    return {false, "Not set", "Not set"};
}

} // namespace ToneAndBeats