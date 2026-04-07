namespace AudioAnalyzer.Models;

public class AudioFileInfo
{
    public string FileType { get; set; } = "";
    public int SampleRate { get; set; }
    public int BitDepth { get; set; }
    public int Channels { get; set; }
    public int Bitrate { get; set; }
    public string BitrateMode { get; set; } = "";

    public string SampleRateDisplay => SampleRate > 0 ? $"{SampleRate} Hz" : "N/A";
    public string BitDepthDisplay => BitDepth > 0 ? $"{BitDepth}-bit" : "N/A";
    public string ChannelsDisplay => Channels switch
    {
        1 => "Mono",
        2 => "Stereo",
        _ => "N/A"
    };
    public string BitrateDisplay => Bitrate > 0 ? $"{Bitrate} kbps" : "N/A";
    public string BitrateModeDisplay => string.IsNullOrEmpty(BitrateMode) ? "N/A" : BitrateMode;
}