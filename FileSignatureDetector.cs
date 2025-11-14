using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HBPakEditor
{
    public static class FileSignatureDetector
    {
        private static readonly Dictionary<string, byte[][]> FileSignatures = new()
    {
        // Images
        { "PNG", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } } },
        { "JPG", new[] { new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, new byte[] { 0xFF, 0xD8, 0xFF, 0xE1 }, new byte[] { 0xFF, 0xD8, 0xFF, 0xE2 } } },
        { "GIF", new[] { new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 } } },
        { "BMP", new[] { new byte[] { 0x42, 0x4D } } },
        { "WEBP", new[] { new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 } } },
        { "ICO", new[] { new byte[] { 0x00, 0x00, 0x01, 0x00 } } },
        { "TIFF", new[] { new byte[] { 0x49, 0x49, 0x2A, 0x00 }, new byte[] { 0x4D, 0x4D, 0x00, 0x2A } } },
        
        // Archives
        { "ZIP", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04 }, new byte[] { 0x50, 0x4B, 0x05, 0x06 }, new byte[] { 0x50, 0x4B, 0x07, 0x08 } } },
        { "RAR", new[] { new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07 } } },
        { "7Z", new[] { new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C } } },
        { "GZIP", new[] { new byte[] { 0x1F, 0x8B } } },
        { "TAR", new[] { new byte[] { 0x75, 0x73, 0x74, 0x61, 0x72 } } },
        
        // Documents
        { "PDF", new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } } },
        { "DOC", new[] { new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } } },
        { "DOCX", new[] { new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00 } } },
        { "RTF", new[] { new byte[] { 0x7B, 0x5C, 0x72, 0x74, 0x66 } } },
        
        // Executables
        { "EXE", new[] { new byte[] { 0x4D, 0x5A } } },
        { "DLL", new[] { new byte[] { 0x4D, 0x5A } } },
        { "ELF", new[] { new byte[] { 0x7F, 0x45, 0x4C, 0x46 } } },
        
        // Audio
        { "MP3", new[] { new byte[] { 0xFF, 0xFB }, new byte[] { 0x49, 0x44, 0x33 } } },
        { "WAV", new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } } },
        { "OGG", new[] { new byte[] { 0x4F, 0x67, 0x67, 0x53 } } },
        { "FLAC", new[] { new byte[] { 0x66, 0x4C, 0x61, 0x43 } } },
        
        // Video
        { "AVI", new[] { new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x41, 0x56, 0x49, 0x20 } } },
        { "MP4", new[] { new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70 }, new byte[] { 0x00, 0x00, 0x00, 0x1C, 0x66, 0x74, 0x79, 0x70 } } },
        { "MKV", new[] { new byte[] { 0x1A, 0x45, 0xDF, 0xA3 } } },
        
        // Text
        { "XML", new[] { new byte[] { 0x3C, 0x3F, 0x78, 0x6D, 0x6C } } },
        { "HTML", new[] { new byte[] { 0x3C, 0x21, 0x44, 0x4F, 0x43, 0x54, 0x59, 0x50, 0x45 }, new byte[] { 0x3C, 0x68, 0x74, 0x6D, 0x6C } } },
        
        // Fonts
        { "TTF", new[] { new byte[] { 0x00, 0x01, 0x00, 0x00 } } },
        { "OTF", new[] { new byte[] { 0x4F, 0x54, 0x54, 0x4F } } },
        { "WOFF", new[] { new byte[] { 0x77, 0x4F, 0x46, 0x46 } } },
    };

        public static string? DetectFileType(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            foreach (var (fileType, signatures) in FileSignatures)
            {
                foreach (var signature in signatures)
                {
                    if (MatchesSignature(data, signature))
                        return fileType;
                }
            }

            return null;
        }

        public static bool IsFileType(byte[] data, string expectedType)
        {
            string? detected = DetectFileType(data);
            return detected?.Equals(expectedType, StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool MatchesSignature(byte[] data, byte[] signature)
        {
            if (data.Length < signature.Length)
                return false;

            for (int i = 0; i < signature.Length; i++)
            {
                // 0x00 in signature means "any byte" (wildcard)
                if (signature[i] != 0x00 && data[i] != signature[i])
                    return false;
            }

            return true;
        }

        public static string GetFileExtension(byte[] data)
        {
            string? fileType = DetectFileType(data);
            return fileType != null ? $".{fileType.ToLower()}" : string.Empty;
        }
    }
}
