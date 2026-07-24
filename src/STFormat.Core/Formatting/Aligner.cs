using System;
using System.Collections.Generic;
using System.Text;
using STFormat.Core.Lexing;

namespace STFormat.Core.Formatting
{
    /// <summary>Classe di una riga ai fini del raggruppamento per allineamento.</summary>
    internal enum LineClass
    {
        Blank,
        CommentOnly,
        Declaration, // dentro VAR/STRUCT, con ':' di livello 0
        Assignment,  // statement "lhs := rhs ;" con ':=' di livello 0
        EnumMember,  // membro di ENUM: "NAME := valore ," (allineato come le assegnazioni)
        CallParam,   // parametro di chiamata FB multi-riga: "NAME := / => valore ,"
        Normal
    }

    /// <summary>Modello di una riga già "risolta" (indent + token da emettere).</summary>
    internal sealed class LineModel
    {
        public bool Blank;
        public int Level;
        public bool IsCaseLabel;
        public LineClass Class;
        public List<Token> Emit = new List<Token>();
    }

    /// <summary>
    /// Allineamento a colonne per gruppi di righe omogenee (dichiarazioni o assegnazioni).
    /// Il riempimento è fatto con TAB (mai spazi): ogni anchor (':', ':=', commento) viene portato
    /// su un tab stop comune, calcolato come il primo multiplo di <c>TabWidth</c> oltre il contenuto
    /// più lungo del gruppo. Gli spazi "normali" fra token restano spazi.
    /// </summary>
    internal static class Aligner
    {
        // ---- Ricerca degli anchor (usata anche per la classificazione) ----

        /// <summary>Indice del ':' di dichiarazione (livello parentesi 0), o -1.</summary>
        public static int FindDeclColon(IReadOnlyList<Token> emit)
        {
            int depth = 0;
            for (int i = 0; i < emit.Count; i++)
            {
                if (emit[i].Kind != TokenKind.Operator) continue;
                depth += BracketDelta(emit[i].Text);
                if (depth == 0 && emit[i].Text == ":") return i;
            }
            return -1;
        }

        /// <summary>Indice del primo ':=' di livello 0, o -1.</summary>
        public static int FindTopAssign(IReadOnlyList<Token> emit)
        {
            int depth = 0;
            for (int i = 0; i < emit.Count; i++)
            {
                if (emit[i].Kind != TokenKind.Operator) continue;
                depth += BracketDelta(emit[i].Text);
                if (depth == 0 && emit[i].Text == ":=") return i;
            }
            return -1;
        }

        /// <summary>Indice del primo ':=' o '=>' di livello 0, o -1 (per i parametri di chiamata).</summary>
        public static int FindArrowOrAssign(IReadOnlyList<Token> emit)
        {
            int depth = 0;
            for (int i = 0; i < emit.Count; i++)
            {
                if (emit[i].Kind != TokenKind.Operator) continue;
                depth += BracketDelta(emit[i].Text);
                if (depth == 0 && (emit[i].Text == ":=" || emit[i].Text == "=>")) return i;
            }
            return -1;
        }

        /// <summary>Quanti ':=' di livello 0 ci sono nella riga.</summary>
        public static int CountTopAssign(IReadOnlyList<Token> emit)
        {
            int depth = 0, count = 0;
            for (int i = 0; i < emit.Count; i++)
            {
                if (emit[i].Kind != TokenKind.Operator) continue;
                depth += BracketDelta(emit[i].Text);
                if (depth == 0 && emit[i].Text == ":=") count++;
            }
            return count;
        }

        private static int FindInitAssign(IReadOnlyList<Token> emit, int afterIndex)
        {
            int depth = 0;
            for (int i = 0; i < emit.Count; i++)
            {
                if (emit[i].Kind != TokenKind.Operator) continue;
                depth += BracketDelta(emit[i].Text);
                if (depth == 0 && i > afterIndex && emit[i].Text == ":=") return i;
            }
            return -1;
        }

        /// <summary>Indice di un commento a fine riga (ultimo token è un commento), o -1.</summary>
        public static int TrailingCommentIndex(IReadOnlyList<Token> emit)
        {
            if (emit.Count == 0) return -1;
            Token last = emit[emit.Count - 1];
            return IsComment(last) ? emit.Count - 1 : -1;
        }

        public static bool IsCommentOnly(IReadOnlyList<Token> emit)
        {
            if (emit.Count == 0) return false;
            foreach (Token t in emit)
                if (!IsComment(t)) return false;
            return true;
        }

        // ---- Rendering di un gruppo allineato ----

        public static List<string> RenderGroup(
            IReadOnlyList<LineModel> group, bool isDeclaration,
            FormatOptions opts, Func<Token, string> render)
        {
            int n = group.Count;
            int slotCount = isDeclaration ? 3 : 2;
            int tabWidth = opts.TabWidth < 1 ? 1 : opts.TabWidth;

            var spaces = new bool[n][];
            var anchors = new int[n][];
            var fills = new int[n][];
            var indents = new string[n];

            for (int li = 0; li < n; li++)
            {
                var emit = group[li].Emit;
                spaces[li] = ComputeSpaceBefore(emit);
                indents[li] = Indent(opts.IndentUnit, group[li].Level);

                var a = new int[slotCount];
                var f = new int[slotCount];
                for (int s = 0; s < slotCount; s++) { a[s] = -1; f[s] = -1; }

                if (isDeclaration)
                {
                    a[0] = FindDeclColon(emit);
                    a[1] = FindInitAssign(emit, a[0]);
                    a[2] = TrailingCommentIndex(emit);
                }
                else
                {
                    a[0] = FindArrowOrAssign(emit); // ':=' per assegnazioni/enum, ':='/'=>' per parametri
                    a[1] = TrailingCommentIndex(emit);
                }
                anchors[li] = a;
                fills[li] = f;
            }

            for (int slot = 0; slot < slotCount; slot++)
            {
                if (!SlotEnabled(slot, isDeclaration, opts)) continue;

                int maxCol = -1;
                for (int li = 0; li < n; li++)
                {
                    int ti = anchors[li][slot];
                    if (ti < 0) continue;
                    int col = ColumnBefore(indents[li], group[li].Emit, spaces[li], anchors[li], fills[li], ti, render, tabWidth);
                    if (col > maxCol) maxCol = col;
                }
                if (maxCol < 0) continue;

                int target = NextTabStop(maxCol, tabWidth);
                for (int li = 0; li < n; li++)
                {
                    int ti = anchors[li][slot];
                    if (ti < 0) continue;
                    int col = ColumnBefore(indents[li], group[li].Emit, spaces[li], anchors[li], fills[li], ti, render, tabWidth);
                    fills[li][slot] = TabsToReach(col, target, tabWidth);
                }
            }

            var result = new List<string>(n);
            for (int li = 0; li < n; li++)
                result.Add(RenderPrefix(indents[li], group[li].Emit, spaces[li], anchors[li], fills[li], group[li].Emit.Count, render));
            return result;
        }

        private static bool SlotEnabled(int slot, bool isDecl, FormatOptions opts)
        {
            if (isDecl)
            {
                if (slot == 0 || slot == 1) return opts.AlignDeclarations;
                return opts.AlignTrailingComments; // slot 2
            }
            if (slot == 0) return opts.AlignAssignments;
            return opts.AlignTrailingComments; // slot 1
        }

        // ---- Rendering di una riga con i fill correnti ----

        private static string RenderPrefix(
            string indent, List<Token> emit, bool[] spaceBefore,
            int[] anchors, int[] fills, int upto, Func<Token, string> render)
        {
            var sb = new StringBuilder(indent);
            for (int i = 0; i < upto; i++)
            {
                int slot = AnchorSlotAt(anchors, i);
                if (slot >= 0 && fills[slot] >= 0)
                {
                    sb.Append('\t', fills[slot]);
                    sb.Append(render(emit[i]));
                }
                else
                {
                    if (i > 0 && spaceBefore[i]) sb.Append(' ');
                    sb.Append(render(emit[i]));
                }
            }
            return sb.ToString();
        }

        private static int ColumnBefore(
            string indent, List<Token> emit, bool[] spaceBefore,
            int[] anchors, int[] fills, int anchorIndex, Func<Token, string> render, int tabWidth)
        {
            string prefix = RenderPrefix(indent, emit, spaceBefore, anchors, fills, anchorIndex, render);
            return VisualColumn(prefix, tabWidth);
        }

        private static int AnchorSlotAt(int[] anchors, int tokenIndex)
        {
            for (int s = 0; s < anchors.Length; s++)
                if (anchors[s] == tokenIndex) return s;
            return -1;
        }

        // ---- Colonne / tab ----

        internal static int VisualColumn(string s, int tabWidth)
        {
            int col = 0;
            foreach (char c in s)
                col = c == '\t' ? (col / tabWidth + 1) * tabWidth : col + 1;
            return col;
        }

        private static int NextTabStop(int col, int tabWidth) => (col / tabWidth + 1) * tabWidth;

        private static int TabsToReach(int col, int target, int tabWidth)
            => target / tabWidth - col / tabWidth;

        // ---- Spaziatura di riga (come StFormatter, senza il caso etichetta CASE) ----

        internal static bool[] ComputeSpaceBefore(IReadOnlyList<Token> emit)
        {
            bool[] unary = SpacingRules.ComputeUnaryFlags(emit);
            var sp = new bool[emit.Count];
            for (int i = 1; i < emit.Count; i++)
                sp[i] = SpacingRules.NeedSpace(emit[i - 1], emit[i], unary[i - 1]);
            return sp;
        }

        private static string Indent(string unit, int level)
        {
            if (level <= 0) return string.Empty;
            var sb = new StringBuilder(unit.Length * level);
            for (int i = 0; i < level; i++) sb.Append(unit);
            return sb.ToString();
        }

        private static int BracketDelta(string op)
        {
            if (op == "(" || op == "[") return 1;
            if (op == ")" || op == "]") return -1;
            return 0;
        }

        private static bool IsComment(Token t)
            => t.Kind == TokenKind.LineComment || t.Kind == TokenKind.BlockComment;
    }
}
