namespace LocalCursorAgent.Core
{
    internal static class WriteTargetPathExtractor
    {
        public static string ExtractWriteTargetPath(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || !input.StartsWith("write:", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            var payload = input.Substring(6);
            if (string.IsNullOrWhiteSpace(payload))
                return string.Empty;

            if (payload.Length >= 3 &&
                payload[1] == ':' &&
                (payload[2] == '\\' || payload[2] == '/'))
            {
                var separator = payload.IndexOf(':', 3);
                return separator >= 0 ? payload[..separator].Trim() : payload.Trim();
            }

            var idx = payload.IndexOf(':');
            return idx >= 0 ? payload[..idx].Trim() : payload.Trim();
        }
    }
}
