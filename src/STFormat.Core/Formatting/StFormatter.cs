using System.Collections.Generic;
using System.Text;
using STFormat.Core.Lexing;

namespace STFormat.Core.Formatting
{
    /// <summary>
    /// Formattatore di Structured Text: normalizza indentazione, spaziatura fra token,
    /// case delle keyword, whitespace/righe vuote, e allinea a colonne (con TAB) le dichiarazioni
    /// e le assegnazioni consecutive più i relativi commenti a fine riga.
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

            List<LineModel> models = BuildModels(new StLexer(source).Lex());
            List<string> outLines = RenderModels(models, options);

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

        // ---- Modello delle righe ----

        private static List<LineModel> BuildModels(IReadOnlyList<Token> tokens)
        {
            var engine = new IndentEngine();
            var models = new List<LineModel>();

            foreach (List<Token> line in SplitPhysicalLines(tokens))
            {
                List<Token> significant = Significant(line);
                LineLayout layout = engine.ProcessLine(significant);
                List<Token> emit = WithoutWhitespace(line); // mantiene i commenti, scarta gli spazi

                var model = new LineModel
                {
                    Blank = emit.Count == 0,
                    Level = layout.Level,
                    IsCaseLabel = layout.IsCaseLabel,
                    Emit = emit
                };
                model.Class = Classify(model, layout);
                models.Add(model);
            }
            return models;
        }

        private static LineClass Classify(LineModel model, LineLayout layout)
        {
            if (model.Blank) return LineClass.Blank;
            List<Token> emit = model.Emit;

            if (Aligner.IsCommentOnly(emit)) return LineClass.CommentOnly;

            if (layout.InEnumBody) return LineClass.EnumMember;

            if (layout.InCallArgs) return LineClass.CallParam;

            if (layout.InDeclarationBlock && !model.IsCaseLabel && Aligner.FindDeclColon(emit) >= 0)
                return LineClass.Declaration;

            if (!layout.InDeclarationBlock
                && Aligner.FindTopAssign(emit) >= 0
                && Aligner.CountTopAssign(emit) == 1
                && EndsWithSemicolon(emit))
                return LineClass.Assignment;

            return LineClass.Normal;
        }

        private static bool EndsWithSemicolon(List<Token> emit)
        {
            int i = LastSignificantIndex(emit);
            return i >= 0 && emit[i].Kind == TokenKind.Operator && emit[i].Text == ";";
        }

        // ---- Rendering con raggruppamento per allineamento ----

        private static List<string> RenderModels(List<LineModel> models, FormatOptions options)
        {
            var outLines = new List<string>(models.Count);
            bool alignDecl = options.AlignDeclarations || options.AlignTrailingComments;
            bool alignAsn = options.AlignAssignments || options.AlignTrailingComments;

            int i = 0;
            while (i < models.Count)
            {
                LineModel m = models[i];

                if (m.Class == LineClass.Declaration && alignDecl)
                {
                    int j = RunEnd(models, i, LineClass.Declaration);
                    EmitGroup(outLines, models, i, j, true, options);
                    i = j;
                }
                else if (m.Class == LineClass.Assignment && alignAsn)
                {
                    int j = RunEnd(models, i, LineClass.Assignment);
                    EmitGroup(outLines, models, i, j, false, options);
                    i = j;
                }
                else if (m.Class == LineClass.EnumMember && alignAsn)
                {
                    int j = RunEnd(models, i, LineClass.EnumMember);
                    EmitGroup(outLines, models, i, j, false, options);
                    i = j;
                }
                else if (m.Class == LineClass.CallParam && alignAsn)
                {
                    int j = RunEnd(models, i, LineClass.CallParam);
                    EmitGroup(outLines, models, i, j, false, options);
                    i = j;
                }
                else
                {
                    outLines.Add(RenderPlain(m, options));
                    i++;
                }
            }
            return outLines;
        }

        private static int RunEnd(List<LineModel> models, int start, LineClass kind)
        {
            int j = start;
            while (j < models.Count && models[j].Class == kind) j++;
            return j;
        }

        private static void EmitGroup(
            List<string> outLines, List<LineModel> models, int start, int end,
            bool isDeclaration, FormatOptions options)
        {
            int count = end - start;
            if (count < 2)
            {
                outLines.Add(RenderPlain(models[start], options)); // gruppo di 1: niente allineamento
                return;
            }

            var group = models.GetRange(start, count);
            outLines.AddRange(Aligner.RenderGroup(group, isDeclaration, options, t => Render(t, options)));
        }

        private static string RenderPlain(LineModel model, FormatOptions options)
        {
            if (model.Blank) return string.Empty;
            return Indent(options.IndentUnit, model.Level) + BuildLine(model.Emit, model.IsCaseLabel, options);
        }

        // ---- Costruzione di una riga (spaziatura Fase 2) ----

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
                    if (i == labelColon && emit[i].Kind == TokenKind.Operator && emit[i].Text == ":")
                        space = false; // etichetta CASE: niente spazio prima del ':' finale
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
