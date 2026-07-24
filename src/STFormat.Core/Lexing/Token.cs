namespace STFormat.Core.Lexing
{
    /// <summary>
    /// Un token lessicale con il testo esatto dal sorgente e la sua posizione.
    /// Immutabile. La concatenazione di <see cref="Text"/> di tutti i token
    /// (esclusa la sentinella <see cref="TokenKind.EndOfFile"/>, che ha testo vuoto)
    /// riproduce esattamente il sorgente originale.
    /// </summary>
    public readonly struct Token
    {
        public TokenKind Kind { get; }

        /// <summary>Testo esatto del token, così com'è nel sorgente.</summary>
        public string Text { get; }

        /// <summary>Offset (in caratteri) di inizio nel sorgente.</summary>
        public int Start { get; }

        /// <summary>Riga 1-based dell'inizio del token.</summary>
        public int Line { get; }

        /// <summary>Colonna 1-based dell'inizio del token.</summary>
        public int Column { get; }

        public Token(TokenKind kind, string text, int start, int line, int column)
        {
            Kind = kind;
            Text = text;
            Start = start;
            Line = line;
            Column = column;
        }

        /// <summary>True per whitespace, newline e commenti (la "trivia" fra token significativi).</summary>
        public bool IsTrivia =>
            Kind == TokenKind.Whitespace
            || Kind == TokenKind.NewLine
            || Kind == TokenKind.LineComment
            || Kind == TokenKind.BlockComment;

        public int End => Start + Text.Length;

        public override string ToString() => $"{Kind} @{Line}:{Column} {System.Text.RegularExpressions.Regex.Escape(Text)}";
    }
}
