using System.Collections.Generic;

namespace STFormat.Core.Lexing
{
    /// <summary>
    /// Lexer "lossless" per Structured Text (IEC 61131-3, con estensioni TwinCAT/CoDeSys).
    /// <para>
    /// Proprietà garantita: la concatenazione dei <see cref="Token.Text"/> di tutti i token
    /// prodotti (la sentinella finale ha testo vuoto) ricostruisce ESATTAMENTE il sorgente.
    /// Questa è la base di sicurezza del formatter: whitespace, a-capo, commenti, stringhe e
    /// letterali restano intatti finché non è la fase di formattazione a riscriverli.
    /// </para>
    /// <para>Assunzioni note: i commenti a blocco "(* *)" sono trattati come annidabili
    /// (comportamento comune in CoDeSys/TwinCAT). Gli "/* */" non sono annidabili.</para>
    /// </summary>
    public sealed class StLexer
    {
        private readonly string _src;
        private int _pos;
        private int _line = 1;
        private int _col = 1;

        // Operatori multi-carattere, ordinati dal più lungo al più corto.
        private static readonly string[] MultiCharOps =
        {
            ":=", "=>", "<=", ">=", "<>", "**", ".."
        };

        // Operatori/segni singoli ammessi.
        private const string SingleCharOps = "+-*/=<>()[],;:.^&@#?";

        public StLexer(string source)
        {
            _src = source ?? string.Empty;
        }

        /// <summary>Scorciatoia: tokenizza un sorgente in un colpo solo.</summary>
        public static IReadOnlyList<Token> Tokenize(string source)
            => new StLexer(source).Lex();

        /// <summary>Produce la lista completa dei token, terminata da <see cref="TokenKind.EndOfFile"/>.</summary>
        public List<Token> Lex()
        {
            var tokens = new List<Token>();
            while (_pos < _src.Length)
            {
                tokens.Add(Next());
            }
            tokens.Add(new Token(TokenKind.EndOfFile, string.Empty, _pos, _line, _col));
            return tokens;
        }

        private Token Next()
        {
            char c = _src[_pos];
            char c1 = Peek(1);

            // A-capo
            if (c == '\r' || c == '\n')
            {
                int nl = (c == '\r' && c1 == '\n') ? 2 : 1;
                return Emit(TokenKind.NewLine, nl);
            }

            // Whitespace (senza a-capo)
            if (IsInlineWhitespace(c))
                return Emit(TokenKind.Whitespace, ReadWhile(IsInlineWhitespace));

            // Commento di riga //
            if (c == '/' && c1 == '/')
                return Emit(TokenKind.LineComment, ReadLineComment());

            // Commento a blocco /* */
            if (c == '/' && c1 == '*')
                return Emit(TokenKind.BlockComment, ReadSlashBlockComment());

            // Commento a blocco (* *) (annidabile)
            if (c == '(' && c1 == '*')
                return Emit(TokenKind.BlockComment, ReadParenBlockComment());

            // Pragma / attributo { ... }
            if (c == '{')
                return Emit(TokenKind.Pragma, ReadPragma());

            // Stringhe: 'testo' e "testo"
            if (c == '\'' || c == '"')
                return Emit(TokenKind.String, ReadString(c));

            // Indirizzo diretto: %IX0.0, %QW10, %MW100 ...
            if (c == '%')
                return Emit(TokenKind.TypedLiteral, ReadDirectAddress());

            // Numero (eventualmente basato: 16#FF, 2#1010)
            if (IsDigit(c))
                return Emit(TokenKind.Number, ReadNumber());

            // Identificatore / keyword / letterale tipizzato (T#..., E_State#Idle, INT#5)
            if (IsIdentStart(c))
            {
                int wordLen = ReadWhile(IsIdentPart);
                // Glue con '#': letterale tipizzato o accesso qualificato (mai spaziato).
                if (Peek(wordLen) == '#')
                {
                    // ReadTypedBody ritorna l'offset assoluto di fine corpo (= lunghezza totale).
                    int total = ReadTypedBody(wordLen + 1);
                    return Emit(TokenKind.TypedLiteral, total);
                }
                string word = _src.Substring(_pos, wordLen);
                var kind = StKeywords.IsKeyword(word) ? TokenKind.Keyword : TokenKind.Identifier;
                return Emit(kind, wordLen);
            }

            // Operatori multi-carattere
            foreach (var op in MultiCharOps)
            {
                if (MatchAt(0, op))
                    return Emit(TokenKind.Operator, op.Length);
            }

            // Operatori/segni singoli
            if (SingleCharOps.IndexOf(c) >= 0)
                return Emit(TokenKind.Operator, 1);

            // Qualsiasi altra cosa: un carattere sconosciuto (lossless).
            return Emit(TokenKind.Unknown, 1);
        }

        // ---- Regole di consumo (ritornano la lunghezza in caratteri) ----

        private int ReadLineComment()
        {
            int i = 2; // salta "//"
            while (_pos + i < _src.Length && !IsNewLine(_src[_pos + i]))
                i++;
            return i;
        }

        private int ReadSlashBlockComment()
        {
            int i = 2; // salta "/*"
            while (_pos + i < _src.Length)
            {
                if (_src[_pos + i] == '*' && Peek(i + 1) == '/')
                    return i + 2;
                i++;
            }
            return i; // non terminato: consuma fino a EOF
        }

        private int ReadParenBlockComment()
        {
            int i = 2; // salta "(*"
            int depth = 1;
            while (_pos + i < _src.Length && depth > 0)
            {
                char a = _src[_pos + i];
                char b = Peek(i + 1);
                if (a == '(' && b == '*') { depth++; i += 2; }
                else if (a == '*' && b == ')') { depth--; i += 2; }
                else i++;
            }
            return i;
        }

        private int ReadPragma()
        {
            int i = 1; // salta "{"
            while (_pos + i < _src.Length && _src[_pos + i] != '}')
                i++;
            if (_pos + i < _src.Length) i++; // includi "}"
            return i;
        }

        private int ReadString(char quote)
        {
            int i = 1; // salta la virgoletta di apertura
            while (_pos + i < _src.Length)
            {
                char ch = _src[_pos + i];
                if (ch == '$')
                {
                    // "$" fa da escape del carattere seguente ($$, $', $N, ...)
                    i += (_pos + i + 1 < _src.Length) ? 2 : 1;
                }
                else if (ch == quote)
                {
                    return i + 1; // virgoletta di chiusura inclusa
                }
                else if (IsNewLine(ch))
                {
                    return i; // stringa non terminata: fermati prima dell'a-capo
                }
                else
                {
                    i++;
                }
            }
            return i; // non terminata a EOF
        }

        private int ReadDirectAddress()
        {
            int i = 1; // salta "%"
            while (_pos + i < _src.Length && IsLetter(_src[_pos + i]))
                i++;
            while (_pos + i < _src.Length && (IsDigit(_src[_pos + i]) || _src[_pos + i] == '.'))
                i++;
            return i;
        }

        private int ReadNumber()
        {
            int i = ReadRun(0, ch => IsDigit(ch) || ch == '_');

            // Letterale basato: 16#FF, 2#1010, 8#777
            if (Peek(i) == '#')
            {
                i++; // '#'
                i = ReadRun(i, ch => IsHexDigit(ch) || ch == '_' || ch == '.');
                return i;
            }

            // Parte frazionaria: solo se seguita da una cifra (evita di mangiare '..' o member access)
            if (Peek(i) == '.' && IsDigit(Peek(i + 1)))
            {
                i++; // '.'
                i = ReadRun(i, ch => IsDigit(ch) || ch == '_');
            }

            // Esponente: e/E con eventuale segno, seguito da cifre
            if (Peek(i) == 'e' || Peek(i) == 'E')
            {
                int j = i + 1;
                if (Peek(j) == '+' || Peek(j) == '-') j++;
                if (IsDigit(Peek(j)))
                {
                    i = ReadRun(j, ch => IsDigit(ch) || ch == '_');
                }
            }

            return i;
        }

        // Corpo di un letterale tipizzato dopo "word#": tempo/data/enum/base annidata.
        private int ReadTypedBody(int startOffset)
            => ReadRun(startOffset, IsTypedBodyChar);

        // ---- Helper di scansione ----

        private char Peek(int offset)
        {
            int p = _pos + offset;
            return p < _src.Length ? _src[p] : '\0';
        }

        private bool MatchAt(int offset, string s)
        {
            if (_pos + offset + s.Length > _src.Length) return false;
            for (int k = 0; k < s.Length; k++)
                if (_src[_pos + offset + k] != s[k]) return false;
            return true;
        }

        private int ReadWhile(System.Func<char, bool> predicate) => ReadRun(0, predicate);

        private int ReadRun(int startOffset, System.Func<char, bool> predicate)
        {
            int i = startOffset;
            while (_pos + i < _src.Length && predicate(_src[_pos + i]))
                i++;
            return i;
        }

        /// <summary>Crea il token, poi avanza posizione/riga/colonna scorrendo il testo consumato.</summary>
        private Token Emit(TokenKind kind, int length)
        {
            string text = _src.Substring(_pos, length);
            var token = new Token(kind, text, _pos, _line, _col);

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    _line++; _col = 1; i++; // \r\n conta come un solo a-capo
                }
                else if (ch == '\r' || ch == '\n')
                {
                    _line++; _col = 1;
                }
                else
                {
                    _col++;
                }
            }

            _pos += length;
            return token;
        }

        // ---- Classificazione caratteri ----

        private static bool IsInlineWhitespace(char c) => c == ' ' || c == '\t' || c == '\f' || c == '\v';
        private static bool IsNewLine(char c) => c == '\r' || c == '\n';
        private static bool IsDigit(char c) => c >= '0' && c <= '9';
        private static bool IsHexDigit(char c) => IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        private static bool IsLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        private static bool IsIdentStart(char c) => IsLetter(c) || c == '_';
        private static bool IsIdentPart(char c) => IsLetter(c) || IsDigit(c) || c == '_';

        // Caratteri ammessi nel corpo di un letterale tipizzato (tempo/data/enum/base).
        private static bool IsTypedBodyChar(char c)
            => IsIdentPart(c) || c == '.' || c == ':' || c == '+' || c == '-' || c == '#';
    }
}
