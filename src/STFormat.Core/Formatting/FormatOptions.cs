namespace STFormat.Core.Formatting
{
    /// <summary>Come normalizzare il case delle keyword ST.</summary>
    public enum KeywordCasing
    {
        /// <summary>Lascia il case invariato.</summary>
        Preserve,
        /// <summary>Maiuscolo (stile convenzionale ST: IF, VAR, FUNCTION_BLOCK).</summary>
        Upper,
        /// <summary>Minuscolo.</summary>
        Lower
    }

    /// <summary>Opzioni di formattazione. I default seguono uno stile ST comune e consistente.</summary>
    public sealed class FormatOptions
    {
        /// <summary>Stringa usata per un livello di indentazione. Default: 4 spazi.
        /// Chi preferisce lo stile nativo TwinCAT può impostare "\t".</summary>
        public string IndentUnit { get; set; } = "    ";

        /// <summary>Normalizzazione del case delle keyword. Default: maiuscolo.</summary>
        public KeywordCasing KeywordCasing { get; set; } = KeywordCasing.Upper;

        /// <summary>Sequenza di fine riga da usare in output. Se null, viene rilevata dal sorgente
        /// ("\r\n" se presente, altrimenti "\n"), con fallback a "\r\n".</summary>
        public string? NewLine { get; set; } = null;

        /// <summary>Ampiezza di un tab in colonne, per il calcolo dell'allineamento. Default: 4
        /// (come TwinCAT). L'allineamento a colonne è riempito con TAB, mai con spazi.</summary>
        public int TabWidth { get; set; } = 4;

        /// <summary>Allinea ':' e ':=' nelle dichiarazioni consecutive dei blocchi VAR/STRUCT.</summary>
        public bool AlignDeclarations { get; set; } = true;

        /// <summary>Allinea ':=' nelle assegnazioni consecutive.</summary>
        public bool AlignAssignments { get; set; } = true;

        /// <summary>Allinea i commenti a fine riga nei gruppi di dichiarazioni/assegnazioni.</summary>
        public bool AlignTrailingComments { get; set; } = true;

        public static FormatOptions Default => new FormatOptions();
    }
}
