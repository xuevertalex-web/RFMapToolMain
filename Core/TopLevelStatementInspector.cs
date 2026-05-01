using System.Text.RegularExpressions;

namespace LocalCursorAgent.Core
{
    internal static class TopLevelStatementInspector
    {
        public static bool ContainsTopLevelStatements(string programPath)
        {
            var text = File.ReadAllText(programPath);
            return text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                .Select(line => line.Trim())
                .Any(line => !string.IsNullOrWhiteSpace(line) &&
                             !line.StartsWith("using ", StringComparison.Ordinal) &&
                             !line.StartsWith("namespace ", StringComparison.Ordinal) &&
                             !line.StartsWith("//", StringComparison.Ordinal) &&
                             !line.StartsWith("/*", StringComparison.Ordinal) &&
                             line != "{" &&
                             line != "}" &&
                             !line.StartsWith("[", StringComparison.Ordinal));
        }

        public static bool ContainsMainEntryPoint(string text) =>
            Regex.IsMatch(text, @"\bstatic\s+(?:async\s+)?(?:void|int|Task(?:<int>)?)\s+Main\s*\(", RegexOptions.Multiline);

        public static string NormalizeHelperClassWithoutMain(string text)
        {
            var withoutMain = Regex.Replace(
                text,
                @"^\s*(?:public|private|protected|internal)?\s*static\s+(?:async\s+)?(?:void|int|Task(?:<int>)?)\s+Main\s*\([^)]*\)\s*\{[\s\S]*?^\s*\}",
                string.Empty,
                RegexOptions.Multiline);

            return withoutMain.Trim() + Environment.NewLine;
        }
    }
}
