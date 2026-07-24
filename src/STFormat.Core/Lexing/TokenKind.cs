namespace STFormat.Core.Lexing
{
    /// <summary>
    /// Categoria lessicale di un token di Structured Text (IEC 61131-3).
    /// Il lexer è "lossless": ogni carattere del sorgente finisce in un token,
    /// così che la concatenazione dei <see cref="Token.Text"/> ricostruisca l'input.
    /// </summary>
    public enum TokenKind
    {
        /// <summary>Sequenza di spazi/tab (nessun a-capo).</summary>
        Whitespace,

        /// <summary>Fine riga: "\r\n", "\n" oppure "\r" (preservata così com'è).</summary>
        NewLine,

        /// <summary>Commento di riga: da "//" a fine riga (esclusa la newline).</summary>
        LineComment,

        /// <summary>Commento a blocco: "(* ... *)" oppure "/* ... */" (annidabile).</summary>
        BlockComment,

        /// <summary>Direttiva/attributo "{ ... }".</summary>
        Pragma,

        /// <summary>Stringa: 'testo' (singola) oppure "testo" (wide), con escape "$".</summary>
        String,

        /// <summary>Letterale numerico, incluse le basi (16#FF, 2#1010) ed esponenti.</summary>
        Number,

        /// <summary>Letterale tipizzato di tempo/data: T#1s, DT#2020-01-01-12:00:00, ecc.</summary>
        TypedLiteral,

        /// <summary>Identificatore che NON è una keyword ST.</summary>
        Identifier,

        /// <summary>Keyword del linguaggio (IF, VAR, FUNCTION_BLOCK, ...).</summary>
        Keyword,

        /// <summary>Operatore o segno di punteggiatura (:=, =>, +, ;, (, ), ...).</summary>
        Operator,

        /// <summary>Carattere non riconosciuto (non dovrebbe accadere su ST valido).</summary>
        Unknown,

        /// <summary>Sentinella di fine input (Text vuoto).</summary>
        EndOfFile
    }
}
