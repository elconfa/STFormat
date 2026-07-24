using System.Collections.Generic;
using System.Text;
using STFormat.Core.Lexing;

namespace STFormat.Core.Formatting
{
    /// <summary>
    /// Formattatore base di Structured Text: normalizza indentazione, spaziatura fra token,
    /// case delle keyword, whitespace di riga e righe vuote.
    /// <para>
    /// Garanzie: (1) modifica solo la trivia e il case delle keyword — la sequenza dei token
    /// significativi resta invariata (nessun cambio di semantica); (2) è idempotente.
    /// </para>
    /// </summary>
    public static class StFormatter
    {
        public static string Format(string source, FormatOptions? options = null)
        {
            options = options ?? FormatOptions.Default;
            string newLine = DetectNewLine(source, options);

            List<List<Token>> lines = SplitPhysicalLines(new StLexer(source).Lex());

            var engine = new IndentEngine();
            var outLines = new List<string>(lines.Count);

            foreach (List<Token> line in lines)
            {
                List<Token> significant = Significant(line);
                LineLayout layout = engine.ProcessLine(significant);

                List<Token> emit = WithoutWhitespace(line); // mantiene i commenti, scarta gli spazi
                if (emit.Count == 0)
                {
                    outLines.Add(string.Empty);
                    continue;
                }

                string body = BuildLine(emit, layout.IsCaseLabel, options);
                outLines.Add(Indent(options.IndentUnit, layout.Level) + body);
            }

            List<string> normalized = NormalizeBlankLines(outLines);
            if (normalized.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            foreach (string l in normalized)
            {
                sb.Append(l);
                sb.Append(newLine);
            }
            return sb.ToString();
        }

        // ---- Costruzione di una riga ----

        private static string BuildLine(List<Token> emit, bool isCaseLabel, FormatOptions options)
        {
            bool[] unary = SpacingRules.ComputeUnaryFlags(emit);
            int labelColon = isCaseLabel ? LastSignificantIndex(emit) : -1;

            var sb = new StringBuilder();
            for (int i = 0; i < emit.Count; i++)
            {
                if (i > 0)
                {
                    bool space = SpacingRules.NeedSpace(emit[i - 1], emit[i], unary[i - 1]);
                    // Etichetta CASE: nessuno spazio prima del ':' finale (es. "2, 3:").
                    if (i == labelColon && emit[i].Kind == TokenKind.Operator && emit[i].Text == ":")
                        space = false;
                    if (space) sb.Append(' ');
                }
                sb.Append(Render(emit[i], options));
            }
            return sb.ToString();
        }

        private static string Render(Token token, FormatOptions options)
        {
            if (token.Kind == TokenKind.Keyword)
            {
                switch (options.KeywordCasing)
                {
                    case KeywordCasing.Upper: return token.Text.ToUpperInvariant();
                    case KeywordCasing.Lower: return token.Text.ToLowerInvariant();
                }
            }
            return token.Text;
        }

        private static int LastSignificantIndex(List<Token> emit)
        {
            for (int i = emit.Count - 1; i >= 0; i--)
                if (!emit[i].IsTrivia) return i;
            return -1;
        }

        // ---- Suddivisione / filtri ----

        private static List<List<Token>> SplitPhysicalLines(IReadOnlyList<Token> tokens)
        {
            var lines = new List<List<Token>>();
            var current = new List<Token>();
            foreach (Token t in tokens)
            {
                if (t.Kind == TokenKind.EndOfFile) break;
                if (t.Kind == TokenKind.NewLine)
                {
                    lines.Add(current);
                    current = new List<Token>();
                    continue;
                }
                current.Add(t);
            }
            lines.Add(current);
            return lines;
        }

        private static List<Token> Significant(List<Token> line)
        {
            var result = new List<Token>();
            foreach (Token t in line)
                if (!t.IsTrivia) result.Add(t);
            return result;
        }

        private static List<Token> WithoutWhitespace(List<Token> line)
        {
            var result = new List<Token>();
            foreach (Token t in line)
                if (t.Kind != TokenKind.Whitespace) result.Add(t);
            return result;
        }

        // ---- Righe vuote ----

        private static List<string> NormalizeBlankLines(List<string> lines)
        {
            var result = new List<string>(lines.Count);
            bool prevBlank = false;
            foreach (string line in lines)
            {
                bool blank = line.Length == 0;
                if (blank)
                {
                    if (result.Count == 0 || prevBlank) continue; // niente vuote in testa / doppie
                    result.Add(string.Empty);
                    prevBlank = true;
                }
                else
                {
                    result.Add(line);
                    prevBlank = false;
                }
            }
            while (result.Count > 0 && result[result.Count - 1].Length == 0)
                result.RemoveAt(result.Count - 1); // niente vuote in coda
            return result;
        }

        // ---- Utility ----

        private static string Indent(string unit, int level)
        {
            if (level <= 0) return string.Empty;
            var sb = new StringBuilder(unit.Length * level);
            for (int i = 0; i < level; i++) sb.Append(unit);
            return sb.ToString();
        }

        private static string DetectNewLine(string source, FormatOptions options)
        {
            if (options.NewLine != null) return options.NewLine;
            if (source.IndexOf("\r\n", System.StringComparison.Ordinal) >= 0) return "\r\n";
            if (source.IndexOf('\n') >= 0) return "\n";
            return "\r\n";
        }
    }
}
