using System.Collections.Generic;

namespace STFormat.Core.Lexing
{
    /// <summary>
    /// Keyword del linguaggio Structured Text (IEC 61131-3 + estensioni TwinCAT/CoDeSys).
    /// Il confronto è case-insensitive: in ST le keyword non distinguono maiuscole/minuscole.
    /// </summary>
    public static class StKeywords
    {
        private static readonly HashSet<string> Words = new HashSet<string>(
            System.StringComparer.OrdinalIgnoreCase)
        {
            // Unità di programma
            "PROGRAM", "END_PROGRAM",
            "FUNCTION", "END_FUNCTION",
            "FUNCTION_BLOCK", "END_FUNCTION_BLOCK",
            "INTERFACE", "END_INTERFACE",
            "METHOD", "END_METHOD",
            "PROPERTY", "END_PROPERTY",
            "ACTION", "END_ACTION",
            "TYPE", "END_TYPE",
            "STRUCT", "END_STRUCT",
            "UNION", "END_UNION",
            "CONFIGURATION", "END_CONFIGURATION",
            "RESOURCE", "END_RESOURCE",

            // Blocchi di dichiarazione variabili
            "VAR", "VAR_INPUT", "VAR_OUTPUT", "VAR_IN_OUT", "VAR_TEMP",
            "VAR_GLOBAL", "VAR_EXTERNAL", "VAR_ACCESS", "VAR_CONFIG",
            "VAR_STAT", "VAR_INST", "END_VAR",
            "CONSTANT", "RETAIN", "NON_RETAIN", "PERSISTENT",
            "AT", "WITH",

            // Controllo di flusso
            "IF", "THEN", "ELSIF", "ELSE", "END_IF",
            "CASE", "OF", "END_CASE",
            "FOR", "TO", "BY", "DO", "END_FOR",
            "WHILE", "END_WHILE",
            "REPEAT", "UNTIL", "END_REPEAT",
            "EXIT", "CONTINUE", "RETURN", "JMP", "GOTO",

            // Operatori booleani/logici testuali
            "AND", "OR", "XOR", "NOT", "MOD",

            // Estensioni OO / accesso
            "EXTENDS", "IMPLEMENTS", "ABSTRACT", "FINAL",
            "PUBLIC", "PRIVATE", "PROTECTED", "INTERNAL",
            "THIS", "SUPER", "__NEW", "__DELETE",
            "GET", "SET",

            // Letterali booleani
            "TRUE", "FALSE", "NULL",

            // Tipi elementari
            "BOOL", "BYTE", "WORD", "DWORD", "LWORD",
            "SINT", "USINT", "INT", "UINT", "DINT", "UDINT", "LINT", "ULINT",
            "REAL", "LREAL",
            "STRING", "WSTRING", "CHAR", "WCHAR",
            "TIME", "LTIME", "DATE", "TIME_OF_DAY", "TOD",
            "DATE_AND_TIME", "DT", "LDT", "LTOD",
            "POINTER", "REFERENCE", "ARRAY", "ANY",

            // Estensioni/istruzioni comuni
            "REF", "ADR", "SIZEOF", "TASK"
        };

        public static bool IsKeyword(string word) => Words.Contains(word);
    }
}
