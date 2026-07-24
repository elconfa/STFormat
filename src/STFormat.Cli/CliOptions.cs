using System.Collections.Generic;
using STFormat.Core.Formatting;

namespace STFormat.Cli
{
    internal enum OutputMode { Write, Check, Diff, Stdout }

    internal sealed class CliOptions
    {
        public List<string> Paths { get; } = new List<string>();
        public OutputMode Mode { get; set; } = OutputMode.Write;
        public bool Stdin { get; set; }
        public bool Help { get; set; }
        public string? Error { get; set; }
        public FormatOptions Format { get; } = new FormatOptions();

        public static CliOptions Parse(string[] args)
        {
            var o = new CliOptions();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                switch (a)
                {
                    case "-h":
                    case "--help":
                        o.Help = true;
                        break;
                    case "--check":
                        o.Mode = OutputMode.Check;
                        break;
                    case "--diff":
                        o.Mode = OutputMode.Diff;
                        break;
                    case "--stdout":
                        o.Mode = OutputMode.Stdout;
                        break;
                    case "--stdin":
                        o.Stdin = true;
                        break;
                    case "--use-tabs":
                        o.Format.IndentUnit = "\t";
                        break;
                    case "--tab-width":
                        if (!TryNextInt(args, ref i, out int tw)) { o.Error = "--tab-width richiede un numero"; return o; }
                        o.Format.TabWidth = tw;
                        break;
                    case "--indent-size":
                        if (!TryNextInt(args, ref i, out int sz)) { o.Error = "--indent-size richiede un numero"; return o; }
                        o.Format.IndentUnit = new string(' ', sz < 0 ? 0 : sz);
                        break;
                    case "--keywords":
                        if (i + 1 >= args.Length) { o.Error = "--keywords richiede: upper|lower|preserve"; return o; }
                        string k = args[++i].ToLowerInvariant();
                        if (k == "upper") o.Format.KeywordCasing = KeywordCasing.Upper;
                        else if (k == "lower") o.Format.KeywordCasing = KeywordCasing.Lower;
                        else if (k == "preserve") o.Format.KeywordCasing = KeywordCasing.Preserve;
                        else { o.Error = "--keywords: valore non valido '" + args[i] + "'"; return o; }
                        break;
                    default:
                        if (a.StartsWith("-")) { o.Error = "opzione sconosciuta: " + a; return o; }
                        o.Paths.Add(a);
                        break;
                }
            }
            return o;
        }

        private static bool TryNextInt(string[] args, ref int i, out int value)
        {
            value = 0;
            if (i + 1 >= args.Length) return false;
            return int.TryParse(args[++i], out value);
        }
    }
}
