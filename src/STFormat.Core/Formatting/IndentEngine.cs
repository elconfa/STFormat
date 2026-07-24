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

        public LineLayout(int level, bool isCaseLabel)
        {
            Level = level;
            IsCaseLabel = isCaseLabel;
        }
    }

    /// <summary>
    /// Calcola l'indentazione riga per riga tramite uno stack di blocchi aperti.
    /// Non è un parser: riconosce le keyword strutturali (IF/FOR/WHILE/REPEAT/CASE, VAR*, STRUCT)
    /// e i loro END_*, più i "mid" ELSE/ELSIF/UNTIL e le etichette di CASE.
    /// Le intestazioni di POU (FUNCTION_BLOCK, PROGRAM, ...) e TYPE non indentano il proprio corpo,
    /// secondo la convenzione TwinCAT (blocchi VAR e implementazione a colonna 0).
    /// </summary>
    public sealed class IndentEngine
    {
        private enum FrameType { Block, Case, CaseArm }

        private readonly List<FrameType> _stack = new List<FrameType>();

        private static readonly HashSet<string> BlockOpeners =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "VAR", "VAR_INPUT", "VAR_OUTPUT", "VAR_IN_OUT", "VAR_TEMP",
                "VAR_GLOBAL", "VAR_EXTERNAL", "VAR_STAT", "VAR_INST",
                "VAR_CONFIG", "VAR_ACCESS",
                "STRUCT", "UNION",
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
        /// Calcola il layout della riga (indent + flag etichetta) a partire dai suoi token
        /// significativi (senza trivia), aggiornando lo stack per le righe successive.
        /// </summary>
        public LineLayout ProcessLine(IReadOnlyList<Token> significant)
        {
            if (significant.Count == 0)
                return new LineLayout(Depth, false); // riga vuota o solo-commento: indent corrente

            Token first = significant[0];
            string? kw = first.Kind == TokenKind.Keyword ? first.Text.ToUpperInvariant() : null;

            // Chiusura di blocco: la riga si dedenta al livello dell'apertura.
            if (kw != null && Closers.Contains(kw))
            {
                if (kw == "END_CASE" && Top == FrameType.CaseArm) Pop(); // arm pendente
                Pop();
                int r = Depth;
                ApplyStructural(significant, 1);
                return new LineLayout(Max0(r), false);
            }

            // ELSE: o "else" di un IF, o "else" di un CASE (in base al frame in cima).
            if (kw == "ELSE")
            {
                if (Top == FrameType.CaseArm)
                {
                    Pop();                       // chiude l'arm precedente
                    int r = Depth;               // livello etichette del CASE
                    Push(FrameType.CaseArm);     // apre il corpo dell'else
                    ApplyStructural(significant, 1);
                    return new LineLayout(Max0(r), false);
                }

                int rIf = Depth - 1;             // dedent al livello dell'IF
                ApplyStructural(significant, 1);
                return new LineLayout(Max0(rIf), false);
            }

            if (kw == "ELSIF")
            {
                int r = Depth - 1;
                ApplyStructural(significant, 1);
                return new LineLayout(Max0(r), false);
            }

            if (kw == "UNTIL")
            {
                int r = Depth - 1;
                ApplyStructural(significant, 1);
                return new LineLayout(Max0(r), false);
            }

            // Etichetta di CASE: dentro un CASE, riga che termina con ':'.
            bool inCase = Top == FrameType.Case || Top == FrameType.CaseArm;
            if (inCase && IsCaseLabel(significant))
            {
                if (Top == FrameType.CaseArm) Pop(); // chiude l'arm precedente
                int r = Depth;                       // livello etichette
                Push(FrameType.CaseArm);             // apre il corpo dell'etichetta
                return new LineLayout(Max0(r), true);
            }

            // Riga normale: rende al livello corrente e applica eventuali aperture/chiusure.
            int render = Depth;
            ApplyStructural(significant, 0);
            return new LineLayout(Max0(render), false);
        }

        /// <summary>Applica allo stack le keyword strutturali della riga (aperture/chiusure).</summary>
        private void ApplyStructural(IReadOnlyList<Token> sig, int start)
        {
            for (int i = start; i < sig.Count; i++)
            {
                if (sig[i].Kind != TokenKind.Keyword) continue;
                string k = sig[i].Text.ToUpperInvariant();

                if (k == "CASE")
                {
                    Push(FrameType.Case);
                }
                else if (BlockOpeners.Contains(k))
                {
                    Push(FrameType.Block);
                }
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

        private static int Max0(int x) => x < 0 ? 0 : x;
    }
}
