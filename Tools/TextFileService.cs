using System.Text;

#pragma warning disable CS0162
namespace LocalCursorAgent.Tools
{
    public enum LineEndingStyle
    {
        Unknown,
        Unix,
        Windows,
        Mixed
    }

    public sealed class TextFileSnapshot
    {
        public required string FilePath { get; init; }
        public required string TextContent { get; init; }
        public required string NormalizedText { get; init; }
        public required Encoding Encoding { get; init; }
        public required bool HasBom { get; init; }
        public required LineEndingStyle LineEndingStyle { get; init; }
        public required bool NormalizationApplied { get; init; }
    }

    /// <summary>
    /// Safe text file handling for deterministic encoding-aware reads and writes.
    /// </summary>
    public sealed class TextFileService
    {
        private readonly Encoding _fallbackEncoding;

        public TextFileService(Encoding? fallbackEncoding = null)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _fallbackEncoding = fallbackEncoding ?? Encoding.GetEncoding(1251);
        }

        public async Task<TextFileSnapshot> ReadAsync(string filePath)
        {
            var bytes = await File.ReadAllBytesAsync(filePath);
            return Read(bytes, filePath);
        }

        public TextFileSnapshot Read(string filePath)
        {
            var bytes = File.ReadAllBytes(filePath);
            return Read(bytes, filePath);
        }

        public TextFileSnapshot Read(byte[] bytes, string filePath = "")
        {
            var (encoding, hasBom, text) = DetectEncoding(bytes);
            var lineEndingStyle = DetectLineEndingStyle(text);
            var normalizedText = NormalizeLineEndings(text, out var normalizationApplied);

            Trace(filePath, encoding, hasBom, lineEndingStyle, normalizationApplied);

            return new TextFileSnapshot
            {
                FilePath = filePath,
                TextContent = text,
                NormalizedText = normalizedText,
                Encoding = encoding,
                HasBom = hasBom,
                LineEndingStyle = lineEndingStyle,
                NormalizationApplied = normalizationApplied
            };
        }

        public async Task WriteAsync(string filePath, string content, TextFileSnapshot? existing = null)
        {
            var encoding = existing?.Encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            var hasBom = existing?.HasBom ?? false;
            var lineEndingStyle = existing?.LineEndingStyle ?? LineEndingStyle.Unix;
            var materialized = MaterializeLineEndings(content, lineEndingStyle);

            var finalEncoding = EnsureBomState(encoding, hasBom);
            var bytes = finalEncoding.GetBytes(materialized);
            await File.WriteAllBytesAsync(filePath, bytes);

            Trace(filePath, finalEncoding, hasBom, lineEndingStyle, materialized != content);
        }

        public string NormalizeForComparison(string text)
        {
            return NormalizeLineEndings(text, out _);
        }

        private (Encoding encoding, bool hasBom, string text) DetectEncoding(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
                return (encoding, true, encoding.GetString(bytes, 3, bytes.Length - 3));
            }

            if (TryDecodeUtf8(bytes, out var utf8Text))
            {
                return (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true), false, utf8Text);
            }

            var fallbackText = _fallbackEncoding.GetString(bytes);
            return (_fallbackEncoding, false, fallbackText);
        }

        private static bool TryDecodeUtf8(byte[] bytes, out string text)
        {
            try
            {
                var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                text = utf8.GetString(bytes);
                return !LooksSuspicious(text);
            }
            catch
            {
                text = string.Empty;
                return false;
            }
        }

        private static bool LooksSuspicious(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            var replacementCount = text.Count(ch => ch == '\uFFFD');
            return replacementCount > 0;
        }

        private static LineEndingStyle DetectLineEndingStyle(string text)
        {
            var hasCrLf = text.Contains("\r\n");
            var hasLf = text.Contains('\n');
            var hasLoneCr = text.Contains('\r') && !hasCrLf;

            if (hasCrLf && hasLf && text.Replace("\r\n", string.Empty).Contains('\n'))
                return LineEndingStyle.Mixed;
            if (hasCrLf)
                return LineEndingStyle.Windows;
            if (hasLf || hasLoneCr)
                return LineEndingStyle.Unix;
            return LineEndingStyle.Unknown;
        }

        private static string NormalizeLineEndings(string text, out bool applied)
        {
            var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            applied = normalized != text;
            return normalized;
        }

        private static string MaterializeLineEndings(string text, LineEndingStyle style)
        {
            return style switch
            {
                LineEndingStyle.Windows => text.Replace("\r\n", "\n").Replace("\n", "\r\n"),
                LineEndingStyle.Unix => text.Replace("\r\n", "\n").Replace('\r', '\n'),
                _ => text
            };
        }

        private static Encoding EnsureBomState(Encoding encoding, bool hasBom)
        {
            if (encoding is UTF8Encoding)
            {
                return new UTF8Encoding(encoderShouldEmitUTF8Identifier: hasBom, throwOnInvalidBytes: true);
            }

            return encoding;
        }

        private static void Trace(string filePath, Encoding encoding, bool hasBom, LineEndingStyle lineEndingStyle, bool normalizationApplied)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            // Verbose debug output disabled by default
            const bool VERBOSE_ENCODING = false;
            if (!VERBOSE_ENCODING)
                return;

            Console.WriteLine("[FILE_ENCODING]");
            Console.WriteLine($"- File path: {filePath}");
            Console.WriteLine($"- Encoding: {encoding.WebName}");
            Console.WriteLine($"- BOM present: {hasBom}");
            Console.WriteLine($"- Line ending style: {lineEndingStyle}");
            Console.WriteLine($"- Was normalization applied: {normalizationApplied}");
        }
    }
#pragma warning restore CS0162
}
