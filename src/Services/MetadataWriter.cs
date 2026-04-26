using System.IO;
using System.Linq;
using TagLib;

namespace AudioAnalyzer.Services;

public class MetadataWriter
{
    public (bool Success, string Message) WriteMetadata(
        string filePath,
        double bpm,
        string key,
        string mode)
    {
        TagLib.File? file = null;
        
        try
        {
            if (!System.IO.File.Exists(filePath))
            {
                return (false, "Error: El archivo no existe.");
            }

            file = TagLib.File.Create(filePath);
            
            // Standard fallback
            file.Tag.BeatsPerMinute = (uint)Math.Round(bpm);
            
            string keyString = $"{key} {mode}";
            string bpmString = bpm.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

            // Inyectar/Actualizar ID3v2 (Para MP3, WAV, AIFF)
            if (file.GetTag(TagTypes.Id3v2, true) is TagLib.Id3v2.Tag id3v2Tag)
            {
                var tkeyFrame = TagLib.Id3v2.TextInformationFrame.Get(id3v2Tag, "TKEY", true);
                tkeyFrame.Text = new[] { keyString };

                var tbpmFrame = TagLib.Id3v2.TextInformationFrame.Get(id3v2Tag, "TBPM", true);
                tbpmFrame.Text = new[] { bpmString };
            }

            // Inyectar/Actualizar VorbisComment (Para FLAC, OGG)
            if (file.GetTag(TagTypes.Xiph, true) is TagLib.Ogg.XiphComment xiphTag)
            {
                xiphTag.SetField("INITIALKEY", new[] { keyString });
                xiphTag.SetField("BPM", new[] { bpmString });
            }

            // Inyectar en AppleTag (Para M4A, AAC)
            if (file.GetTag(TagTypes.Apple, true) is TagLib.Mpeg4.AppleTag appleTag)
            {
                appleTag.SetDashBox("com.apple.iTunes", "initialkey", keyString);
                appleTag.SetDashBox("com.apple.iTunes", "bpm", bpmString);
            }
            
            file.Save();
            file.Dispose();
            file = null;

            if (!System.IO.File.Exists(filePath))
            {
                return (false, "Error: El archivo desapareció después de escribir.");
            }

            return (true, $"Metadata guardada:\nBPM: {bpmString}\nKey: {keyString}");
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "Error: No tienes permiso para modificar este archivo.");
        }
        catch (IOException ex)
        {
            return (false, $"Error de E/S: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"Error al guardar metadata: {ex.Message}");
        }
        finally
        {
            file?.Dispose();
        }
    }

    public (bool HasMetadata, string CurrentBpm, string CurrentKey) GetCurrentMetadata(string filePath)
    {
        try
        {
            using var file = TagLib.File.Create(filePath);
            
            string currentBpm = "";
            string currentKey = "";

            // 1. Leer ID3v2 (MP3, WAV, AIFF)
            if (file.GetTag(TagTypes.Id3v2, false) is TagLib.Id3v2.Tag id3v2Tag)
            {
                var tkeyFrame = TagLib.Id3v2.TextInformationFrame.Get(id3v2Tag, "TKEY", false);
                if (tkeyFrame != null && tkeyFrame.Text.Length > 0) currentKey = tkeyFrame.Text[0];

                var tbpmFrame = TagLib.Id3v2.TextInformationFrame.Get(id3v2Tag, "TBPM", false);
                if (tbpmFrame != null && tbpmFrame.Text.Length > 0) currentBpm = tbpmFrame.Text[0];
            }

            // 2. Leer VorbisComment (FLAC, OGG)
            if (string.IsNullOrEmpty(currentKey) || string.IsNullOrEmpty(currentBpm))
            {
                if (file.GetTag(TagTypes.Xiph, false) is TagLib.Ogg.XiphComment xiphTag)
                {
                    var keys = xiphTag.GetField("INITIALKEY");
                    if (keys != null && keys.Length > 0) currentKey = keys[0];

                    var bpms = xiphTag.GetField("BPM");
                    if (bpms != null && bpms.Length > 0) currentBpm = bpms[0];
                }
            }

            // 3. Fallbacks Legacy
            if (string.IsNullOrEmpty(currentBpm))
            {
                currentBpm = file.Tag.BeatsPerMinute > 0 ? file.Tag.BeatsPerMinute.ToString() : "No establecido";
            }
            
            if (string.IsNullOrEmpty(currentKey))
            {
                currentKey = "No establecido";
                if (!string.IsNullOrEmpty(file.Tag.Comment))
                {
                    if (file.Tag.Comment.Contains("Key:"))
                    {
                        var parts = file.Tag.Comment.Split(';');
                        foreach (var part in parts)
                        {
                            if (part.Trim().StartsWith("Key:"))
                            {
                                currentKey = part.Trim().Substring(4).Trim();
                                break;
                            }
                        }
                    }
                    else
                    {
                        currentKey = file.Tag.Comment;
                    }
                }
            }
            
            bool hasMetadata = currentBpm != "No establecido" || currentKey != "No establecido";
            
            return (hasMetadata, currentBpm, currentKey);
        }
        catch (Exception ex)
        {
            LoggerService.Log($"MetadataWriter.GetCurrentMetadata - Error: {ex.Message}");
            return (false, "Error", "Error");
        }
    }
}
