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

        public LineLayout(int level, bool isCaseLabel, bool inDeclarationBlock, bool inEnumBody)
        {
            Level = level;
            IsCaseLabel = isCaseLabel;
            InDeclarationBlock = inDeclarationBlock;
            InEnumBody = inEnumBody;
        }
    }

    /// <summary>
    /// Calcola l'indentazione riga per riga tramite uno stack di blocchi aperti.
    /// Non è un parser: riconosce le keyword strutturali (IF/FOR/WHILE/REPEAT/CASE, VAR*, STRUCT)
    /// e i loro END_*, i "mid" ELSE/ELSIF/UNTIL, le etichette di CASE, e il corpo degli ENUM
    /// definiti con "TYPE Name : ( ... ) BASE;".
    /// Le intestazioni di POU (FUNCTION_BLOCK, PROGRAM, ...) e TYPE non indentano il proprio corpo,
    /// secondo la convenzione TwinCAT.
    /// </summary>
    public sealed class IndentEngine
    {
        private enum FrameType { DeclBlock, CtrlBlock, EnumBody, Case, CaseArm }

        private readonly List<FrameType> _stack = new List<FrameType>();
        private int _typeDepth; // profondità dei blocchi TYPE ... END_TYPE aperti

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

        private int Depth => _stack.Count;

        private FrameType? Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : (FrameType?)null;

        private void Push(FrameType f) => _stack.Add(f);

        private void Pop()
        {
            if (_stack.Count > 0) _stack.RemoveAt(_stack.Count - 1);
        }

        /// <summary>
        /// Calcola il layout della riga (indent + flag) a partire dai suoi token significativi
        /// (senza trivia), aggiornando lo stato per le righe successive.
        /// </summary>
        public LineLayout ProcessLine(IReadOnlyList<Token> significant)
        {
            if (significant.Count == 0)
                return new LineLayout(Depth, false, Top == FrameType.DeclBlock, Top == FrameType.EnumBody);

            // Chiusura del corpo ENUM: la riga ")..." si dedenta al livello dell'apertura.
            if (Top == FrameType.EnumBody && NetParen(significant) < 0)
            {
                Pop();
                return new LineLayout(Max0(Depth), false, false, false);
            }

            Token first = significant[0];
            string? kw = first.Kind == TokenKind.Keyword ? first.Text.ToUpperInvariant() : null;

            // Chiusura di blocco: la riga si dedenta al livello dell'apertura.
            if (kw != null && Closers.Contains(kw))
            {
                if (kw == "END_CASE" && Top == FrameType.CaseArm) Pop(); // arm pendente
                Pop();
                int r = Depth;
                ApplyStructural(significant, 1);
                return new LineLayout(Max0(r), false, false, false);
            }

            // ELSE: o "else" di un IF, o "else" di un CASE (in base al frame in cima).
            if (kw == "ELSE")
            {
                if (Top == FrameType.CaseArm)
                {
                    Pop();
                    int r = Depth;
                    Push(FrameType.CaseArm);
                    ApplyStructural(significant, 1);
                    return new LineLayout(Max0(r), false, false, false);
                }

                int rIf = Depth - 1;
                ApplyStructural(significant, 1);
                return new LineLayout(Max0(rIf), false, false, false);
            }

            if (kw == "ELSIF" || kw == "UNTIL")
            {
                int r = Depth - 1;
                ApplyStructural(significant, 1);
                return new LineLayout(Max0(r), false, false, false);
            }

            // Etichetta di CASE: dentro un CASE, riga che termina con ':'.
            bool inCase = Top == FrameType.Case || Top == FrameType.CaseArm;
            if (inCase && IsCaseLabel(significant))
            {
                if (Top == FrameType.CaseArm) Pop();
                int r = Depth;
                Push(FrameType.CaseArm);
                return new LineLayout(Max0(r), true, false, false);
            }

            // Riga normale.
            bool inDecl = Top == FrameType.DeclBlock;
            bool inEnum = Top == FrameType.EnumBody;
            int render = Depth;

            ApplyStructural(significant, 0);

            // Apertura del corpo ENUM: dentro un TYPE, una riga che apre più parentesi di quante ne chiude.
            if (_typeDepth > 0 && Top != FrameType.EnumBody && NetParen(significant) > 0)
                Push(FrameType.EnumBody);

            return new LineLayout(Max0(render), false, inDecl, inEnum);
        }

        /// <summary>Applica allo stato le keyword strutturali della riga (aperture/chiusure, TYPE).</summary>
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

        private static bool IsCaseLabel(IReadOnlyList<Token> sig)
        {
            Token last = sig[sig.Count - 1];
            return last.Kind == TokenKind.Operator && last.Text == ":";
        }

        private static int NetParen(IReadOnlyList<Token> sig)
        {
            int net = 0;
            foreach (Token t in sig)
            {
                if (t.Kind != TokenKind.Operator) continue;
                if (t.Text == "(") net++;
                else if (t.Text == ")") net--;
            }
            return net;
        }

        private static int Max0(int x) => x < 0 ? 0 : x;
    }
}
