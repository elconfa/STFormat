using System;
using System.Collections.Generic;
using STFormat.Core.Lexing;

namespace STFormat.Core.Formatting
{
    /// <summary>Esito del calcolo di indentazione per una riga logica.</summary>
    public readonly struct LineLayout
    {
        /// <summary>Livello di indentazione (numero di unità) con cui rendere la riga.</summary>
        public int Level { get; }

        /// <summary>True se la riga è un'etichetta di CASE (per la spaziatura del ':' finale).</summary>
        public bool IsCaseLabel { get; }

        /// <summary>True se la riga è dentro un blocco di dichiarazione (VAR*/STRUCT/UNION).</summary>
        public bool InDeclarationBlock { get; }

        /// <summary>True se la riga è un membro dentro il corpo di un ENUM ( ... ).</summary>
        public bool InEnumBody { get; }

        /// <summary>True se la riga è un parametro dentro una chiamata/espressione multi-riga fra parentesi.</summary>
        public bool InCallArgs { get; }

        public LineLayout(int level, bool isCaseLabel, bool inDeclarationBlock, bool inEnumBody, bool inCallArgs)
        {
            Level = level;
            IsCaseLabel = isCaseLabel;
            InDeclarationBlock = inDeclarationBlock;
            InEnumBody = inEnumBody;
            InCallArgs = inCallArgs;
        }
    }

    /// <summary>
    /// Calcola l'indentazione riga per riga tramite uno stack di blocchi aperti e uno stack di
    /// parentesi aperte (per le continuazioni multi-riga: corpo ENUM e parametri di chiamata FB).
    /// Non è un parser: riconosce le keyword strutturali e i loro END_*, i "mid" ELSE/ELSIF/UNTIL,
    /// le etichette di CASE, e le continuazioni fra parentesi.
    /// Le intestazioni di POU e TYPE non indentano il proprio corpo (convenzione TwinCAT).
    /// </summary>
    public sealed class IndentEngine
    {
        private enum FrameType { DeclBlock, CtrlBlock, Case, CaseArm }

        private readonly List<FrameType> _stack = new List<FrameType>();
        private readonly List<bool> _parens = new List<bool>(); // per ogni '(' aperta: true se corpo ENUM
        private int _typeDepth;
        private bool _openStatement; // true se la riga precedente ha lasciato uno statement/header non chiuso

        // Keyword che, se sono la PRIMA della riga, la rendono comunque "completa" (non è una continuazione).
        private static readonly HashSet<string> HeaderFirstKeywords =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "TYPE", "UNTIL", "ELSE", "REPEAT",
                "FUNCTION_BLOCK", "FUNCTION", "PROGRAM", "METHOD", "PROPERTY",
                "ACTION", "INTERFACE", "CONFIGURATION", "RESOURCE"
            };

        private static readonly HashSet<string> DeclOpeners =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "VAR", "VAR_INPUT", "VAR_OUTPUT", "VAR_IN_OUT", "VAR_TEMP",
                "VAR_GLOBAL", "VAR_EXTERNAL", "VAR_STAT", "VAR_INST",
                "VAR_CONFIG", "VAR_ACCESS",
                "STRUCT", "UNION"
            };

        private static readonly HashSet<string> CtrlOpeners =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "IF", "FOR", "WHILE", "REPEAT"
            };

        private static readonly HashSet<string> Closers =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "END_VAR", "END_STRUCT", "END_UNION",
                "END_IF", "END_FOR", "END_WHILE", "END_REPEAT", "END_CASE"
            };

        private int BlockDepth => _stack.Count;
        private int ParenDepth => _parens.Count;
        private FrameType? Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : (FrameType?)null;
        private bool TopParenIsEnum() => _parens.Count > 0 && _parens[_parens.Count - 1];

        private void Push(FrameType f) => _stack.Add(f);

        private void Pop()
        {
            if (_stack.Count > 0) _stack.RemoveAt(_stack.Count - 1);
        }

        public LineLayout ProcessLine(IReadOnlyList<Token> significant)
        {
            // Riga vuota o solo-commento: non cambia lo stato dello statement.
            if (significant.Count == 0)
            {
                int cont = _openStatement && ParenDepth == 0 ? 1 : 0;
                return new LineLayout(BlockDepth + ParenDepth + cont, false, Top == FrameType.DeclBlock,
                    TopParenIsEnum(), ParenDepth > 0 && !TopParenIsEnum());
            }

            // --- Continuazione fra parentesi (parametri di chiamata / membri di enum / espressioni) ---
            if (ParenDepth > 0)
            {
                int leadClose = LeadingCloseParens(significant);
                int render = BlockDepth + Max0(ParenDepth - leadClose);
                bool closesLine = leadClose > 0;
                bool inEnum = !closesLine && TopParenIsEnum();
                bool inCall = !closesLine && !TopParenIsEnum();
                UpdateParens(significant);
                _openStatement = ParenDepth == 0 && !LineIsComplete(significant);
                return new LineLayout(Max0(render), false, false, inEnum, inCall);
            }

            Token first = significant[0];
            string? kw = first.Kind == TokenKind.Keyword ? first.Text.ToUpperInvariant() : null;

            if (kw != null && Closers.Contains(kw))
            {
                if (kw == "END_CASE" && Top == FrameType.CaseArm) Pop();
                Pop();
                int r = BlockDepth;
                ApplyStructural(significant, 1);
                _openStatement = false;
                return new LineLayout(Max0(r), false, false, false, false);
            }

            if (kw == "ELSE")
            {
                if (Top == FrameType.CaseArm)
                {
                    Pop();
                    int r = BlockDepth;
                    Push(FrameType.CaseArm);
                    ApplyStructural(significant, 1);
                    _openStatement = false;
                    return new LineLayout(Max0(r), false, false, false, false);
                }

                int rIf = BlockDepth - 1;
                ApplyStructural(significant, 1);
                _openStatement = false;
                return new LineLayout(Max0(rIf), false, false, false, false);
            }

            if (kw == "ELSIF" || kw == "UNTIL")
            {
                int r = BlockDepth - 1;
                ApplyStructural(significant, 1);
                _openStatement = ParenDepth == 0 && !LineIsComplete(significant); // ELSIF può proseguire su più righe
                return new LineLayout(Max0(r), false, false, false, false);
            }

            bool inCase = Top == FrameType.Case || Top == FrameType.CaseArm;
            if (inCase && IsCaseLabel(significant))
            {
                if (Top == FrameType.CaseArm) Pop();
                int r = BlockDepth;
                Push(FrameType.CaseArm);
                _openStatement = false;
                return new LineLayout(Max0(r), true, false, false, false);
            }

            // Riga normale (fuori dalle parentesi).
            bool isContinuation = _openStatement && ParenDepth == 0;
            bool inDecl = Top == FrameType.DeclBlock;
            int render2 = BlockDepth + (isContinuation ? 1 : 0);
            ApplyStructural(significant, 0);
            UpdateParens(significant); // eventuali '(' aperte qui iniziano una continuazione
            _openStatement = ParenDepth == 0 && !LineIsComplete(significant);
            return new LineLayout(Max0(render2), false, inDecl && !isContinuation, false, false);
        }

        // True se la riga chiude lo statement/header corrente (la riga successiva NON è una continuazione).
        private static bool LineIsComplete(IReadOnlyList<Token> sig)
        {
            Token last = sig[sig.Count - 1];
            if (last.Kind == TokenKind.Operator && (last.Text == ";" || last.Text == ":")) return true;
            if (last.Kind == TokenKind.Keyword)
            {
                string u = last.Text.ToUpperInvariant();
                if (u == "THEN" || u == "DO" || u == "OF" || u == "ELSE" || u == "REPEAT") return true;
            }

            Token first = sig[0];
            if (first.Kind == TokenKind.Keyword)
            {
                string f = first.Text.ToUpperInvariant();
                if (f.StartsWith("END_", StringComparison.OrdinalIgnoreCase)) return true;
                if (DeclOpeners.Contains(f)) return true;
                if (HeaderFirstKeywords.Contains(f)) return true;
            }
            return false;
        }

        private void ApplyStructural(IReadOnlyList<Token> sig, int start)
        {
            for (int i = start; i < sig.Count; i++)
            {
                if (sig[i].Kind != TokenKind.Keyword) continue;
                string k = sig[i].Text.ToUpperInvariant();

                if (k == "TYPE") _typeDepth++;
                else if (k == "END_TYPE") { if (_typeDepth > 0) _typeDepth--; }
                else if (k == "CASE") Push(FrameType.Case);
                else if (DeclOpeners.Contains(k)) Push(FrameType.DeclBlock);
                else if (CtrlOpeners.Contains(k)) Push(FrameType.CtrlBlock);
                else if (Closers.Contains(k))
                {
                    if (k == "END_CASE" && Top == FrameType.CaseArm) Pop();
                    Pop();
                }
            }
        }

        // Apre/chiude le parentesi incontrate sulla riga (per l'indentazione delle righe successive).
        private void UpdateParens(IReadOnlyList<Token> sig)
        {
            foreach (Token t in sig)
            {
                if (t.Kind != TokenKind.Operator) continue;
                if (t.Text == "(")
                {
                    bool isEnum = _typeDepth > 0 && _parens.Count == 0; // parentesi esterna dentro un TYPE = enum
                    _parens.Add(isEnum);
                }
                else if (t.Text == ")")
                {
                    if (_parens.Count > 0) _parens.RemoveAt(_parens.Count - 1);
                }
            }
        }

        private static int LeadingCloseParens(IReadOnlyList<Token> sig)
        {
            int n = 0;
            foreach (Token t in sig)
            {
                if (t.Kind == TokenKind.Operator && t.Text == ")") n++;
                else break;
            }
            return n;
        }

        private static bool IsCaseLabel(IReadOnlyList<Token> sig)
        {
            Token last = sig[sig.Count - 1];
            return last.Kind == TokenKind.Operator && last.Text == ":";
        }

        private static int Max0(int x) => x < 0 ? 0 : x;
    }
}
