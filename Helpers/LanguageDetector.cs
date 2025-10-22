namespace Flow.Launcher.Plugin.ByteStash.Helpers
{
    /// <summary>
    /// Helper class to detect programming language from code snippets.
    /// </summary>
    internal static class LanguageDetector
    {
        /// <summary>
        /// Detects the programming language from a code snippet.
        /// </summary>
        /// <param name="code">The code snippet to analyze.</param>
        /// <returns>The detected language name, or "plaintext" if no language could be detected.</returns>
        internal static string DetectLanguage(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return "plaintext";

            try
            {
                
                // C#
                if (code.Contains("using System") ||
                    code.Contains("namespace") ||
                    code.Contains("public class") ||
                    code.Contains("private ") ||
                    code.Contains("async Task"))
                    return "csharp";

                // JavaScript/TypeScript
                if (code.Contains("function ") ||
                    code.Contains("const ") ||
                    code.Contains("let ") ||
                    code.Contains("=>") ||
                    code.Contains("import ") ||
                    code.Contains("export "))
                    return "javascript";

                // Python
                if (code.Contains("def ") ||
                    code.Contains("import ") ||
                    code.Contains("class ") ||
                    code.Contains("print("))
                    return "python";

                // Java
                if (code.Contains("public static void main") ||
                    code.Contains("System.out.println") ||
                    code.Contains("import java."))
                    return "java";

                // HTML
                if (code.Contains("<html") ||
                    code.Contains("<div") ||
                    code.Contains("<!DOCTYPE"))
                    return "html";

                // CSS
                if (code.Contains('{') && code.Contains('}') &&
                    (code.Contains("color:") || code.Contains("margin:") || code.Contains("padding:")))
                    return "css";

                // JSON
                if ((code.TrimStart().StartsWith('{') || code.TrimStart().StartsWith('[')) &&
                    code.Contains(':') && code.Contains('\"'))
                    return "json";

                // SQL
                if (code.Contains("SELECT ") ||
                    code.Contains("INSERT INTO") ||
                    code.Contains("UPDATE ") ||
                    code.Contains("DELETE FROM"))
                    return "sql";

                // XML
                if (code.Contains("<?xml") ||
                    (code.Contains('<') && code.Contains("/>") && code.Contains("xmlns")))
                    return "xml";

                // Shell/Bash
                if (code.StartsWith("#!/bin/bash") ||
                    code.StartsWith("#!/bin/sh") ||
                    code.Contains("echo "))
                    return "bash";

                // PowerShell
                if (code.Contains("Get-") ||
                    code.Contains("Set-") ||
                    code.Contains("New-") ||
                    code.Contains("$PSVersionTable"))
                    return "powershell";

                // fallback
                return "plaintext";
            }
            catch
            {
                return "plaintext";
            }
        }
    }
}
