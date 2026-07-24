using System.Text.RegularExpressions;

namespace STFormat.Core.Formatting
{
    /// <summary>
    /// Formatta il codice ST contenuto nei file XML di TwinCAT (.TcPOU/.TcGVL/.TcDUT).
    /// Il codice vive nelle CDATA degli elementi &lt;Declaration&gt; e &lt;ST&gt;; qui viene
    /// sostituito SOLO il testo dentro quelle CDATA, lasciando il resto dell'XML intatto
    /// (diff minimi). Vengono preservati i fine riga del sorgente e la presenza/assenza
    /// del newline finale prima di <c>]]&gt;</c>.
    /// </summary>
    public static class TcPouFormatter
    {
        // Cattura: apertura (tag + eventuale whitespace + "<![CDATA["), codice, chiusura ("]]></tag>").
        private static readonly Regex CodeBlock = new Regex(
            @"(?<open><(?<tag>Declaration|ST)>\s*<!\[CDATA\[)(?<code>.*?)(?<close>\]\]></\k<tag>>)",
            RegexOptions.Singleline);

        /// <summary>Formatta tutte le sezioni di codice ST di un documento XML TwinCAT.</summary>
        public static string FormatDocument(string xml, FormatOptions? options = null)
        {
            options = options ?? FormatOptions.Default;
            return CodeBlock.Replace(xml, m =>
            {
                string formatted = FormatCode(m.Groups["code"].Value, options);
                return m.Groups["open"].Value + formatted + m.Groups["close"].Value;
            });
        }

        /// <summary>True se il testo sembra un documento XML TwinCAT (contiene POU/GVL/DUT).</summary>
        public static bool LooksLikeTwinCatXml(string text)
        {
            return text.IndexOf("<TcPlcObject", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FormatCode(string code, FormatOptions options)
        {
            if (code.Length == 0) return code;

            bool hadTrailingNewline = EndsWithNewline(code);
            string formatted = StFormatter.Format(code, options);
            if (!hadTrailingNewline)
                formatted = TrimOneTrailingNewline(formatted);
            return formatted;
        }

        private static bool EndsWithNewline(string s)
        {
            if (s.Length == 0) return false;
            char c = s[s.Length - 1];
            return c == '\n' || c == '\r';
        }

        private static string TrimOneTrailingNewline(string s)
        {
            if (s.EndsWith("\r\n")) return s.Substring(0, s.Length - 2);
            if (s.EndsWith("\n") || s.EndsWith("\r")) return s.Substring(0, s.Length - 1);
            return s;
        }
    }
}
