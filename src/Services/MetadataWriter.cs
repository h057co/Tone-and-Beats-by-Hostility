using System.IO;
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
            
            file.Tag.BeatsPerMinute = (uint)Math.Round(bpm);
            
            string keyComment = $"Key: {key} {mode}";
            if (string.IsNullOrEmpty(file.Tag.Comment))
            {
                file.Tag.Comment = keyComment;
            }
            else
            {
                file.Tag.Comment += $"; {keyComment}";
            }
            
            file.Save();
            file.Dispose();
            file = null;

            if (!System.IO.File.Exists(filePath))
            {
                return (false, "Error: El archivo desapareció después de escribir.");
            }

            using var verifyFile = TagLib.File.Create(filePath);
            if (verifyFile.Tag.BeatsPerMinute != (uint)Math.Round(bpm))
            {
                return (false, "Error: La verificación de escritura falló.");
            }

            return (true, $"Metadata guardada:\nBPM: {bpm}\nKey: {key} {mode}");
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
            
            string currentBpm = file.Tag.BeatsPerMinute > 0 ? file.Tag.BeatsPerMinute.ToString() : "No establecido";
            
            string currentKey = "No establecido";
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
            
            bool hasMetadata = file.Tag.BeatsPerMinute > 0 || !string.IsNullOrEmpty(file.Tag.Comment);
            
            return (hasMetadata, currentBpm, currentKey);
        }
        catch (Exception ex)
        {
            LoggerService.Log($"MetadataWriter.GetCurrentMetadata - Error: {ex.Message}");
            return (false, "Error", "Error");
        }
    }
}
