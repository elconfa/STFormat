using System;
using System.Collections.Generic;
using STFormat.Core.Lexing;

namespace STFormat.Core.Formatting
{
    /// <summary>
    /// Regole di spaziatura fra token adiacenti su una stessa riga. Puramente cosmetiche:
    /// non spezzano né uniscono token (il lexer ha già reso atomici ':=', 'T#5s', ecc.),
    /// quindi non possono alterare la semantica.
    /// </summary>
    public static class SpacingRules
    {
        private static readonly HashSet<string> FuncLikeKeywords =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ADR", "SIZEOF", "REF", "__NEW", "__DELETE"
            };

        /// <summary>
        /// Per ogni token calcola se un '+'/'-' è unario (in tal caso non ci va spazio dopo il segno).
        /// I commenti vengono saltati nel guardare il token precedente.
        /// </summary>
        public static bool[] ComputeUnaryFlags(IReadOnlyList<Token> tokens)
        {
            var flags = new bool[tokens.Count];
            for (int i = 0; i < tokens.Count; i++)
            {
                if (!IsPlusMinus(tokens[i])) continue;

                int j = i - 1;
                while (j >= 0 && IsComment(tokens[j])) j--;

                flags[i] = j < 0 || !IsValueEnd(tokens[j]);
            }
            return flags;
        }

        /// <summary>True se fra <paramref name="prev"/> e <paramref name="cur"/> va inserito uno spazio.</summary>
        public static bool NeedSpace(Token prev, Token cur, bool prevIsUnarySign)
        {
            string? pc = prev.Kind == TokenKind.Operator ? prev.Text : null;
            string? cc = cur.Kind == TokenKind.Operator ? cur.Text : null;

            // 1) Mai spazio prima di questi (regola più forte).
            if (cc == "," || cc == ";" || cc == ")" || cc == "]") return false;

            // 2) Spazio prima di un commento; 3) spazio dopo un commento a blocco inline.
            if (IsComment(cur)) return true;
            if (prev.Kind == TokenKind.BlockComment) return true;

            // 4) Mai spazio subito dopo '(' o '['.
            if (pc == "(" || pc == "[") return false;

            // 5) Nessuno spazio attorno a accesso membro '.', range '..', deref '^'.
            if (cc == "." || cc == ".." || cc == "^") return false;
            if (pc == "." || pc == ".." || pc == "^") return false;

            // 6) Chiamata/indicizzazione: '(' o '[' incollati a un valore o a una funzione.
            if (cc == "(" || cc == "[")
            {
                if (prev.Kind == TokenKind.Identifier
                    || prev.Kind == TokenKind.TypedLiteral
                    || pc == ")" || pc == "]") return false;
                if (prev.Kind == TokenKind.Keyword && FuncLikeKeywords.Contains(prev.Text)) return false;
                return true;
            }

            // 7) ':' con spazio su entrambi i lati (stile dichiarazione "nome : tipo").
            //    Il caso dell'etichetta CASE è gestito da StFormatter (niente spazio prima del ':').
            if (cc == ":") return true;
            if (pc == ":") return true;

            // 8) Segno unario: nessuno spazio dopo di esso.
            if ((pc == "+" || pc == "-") && prevIsUnarySign) return false;

            // 9) Default: uno spazio.
            return true;
        }

        private static bool IsComment(Token t)
            => t.Kind == TokenKind.LineComment || t.Kind == TokenKind.BlockComment;

        private static bool IsPlusMinus(Token t)
            => t.Kind == TokenKind.Operator && (t.Text == "+" || t.Text == "-");

        /// <summary>True se il token può chiudere un valore (quindi un '+'/'-' successivo è binario).</summary>
        private static bool IsValueEnd(Token t)
        {
            switch (t.Kind)
            {
                case TokenKind.Identifier:
                case TokenKind.Number:
                case TokenKind.String:
                case TokenKind.TypedLiteral:
                    return true;
                case TokenKind.Operator:
                    return t.Text == ")" || t.Text == "]" || t.Text == "^";
                case TokenKind.Keyword:
                    string u = t.Text.ToUpperInvariant();
                    return u == "TRUE" || u == "FALSE" || u == "NULL";
                default:
                    return false;
            }
        }
    }
}
