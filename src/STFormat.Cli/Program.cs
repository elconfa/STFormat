using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using STFormat.Core.Formatting;

namespace STFormat.Cli
{
    internal static class Program
    {
        private static readonly HashSet<string> XmlExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".tcpou", ".tcgvl", ".tcdut", ".tcio" };

        private static readonly HashSet<string> ScanExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".tcpou", ".tcgvl", ".tcdut", ".tcio", ".exp", ".st", ".txt" };

        private static int Main(string[] args)
        {
            CliOptions opts = CliOptions.Parse(args);

            if (opts.Help || args.Length == 0)
            {
                PrintUsage();
                return opts.Error != null ? 2 : 0;
            }
            if (opts.Error != null)
            {
                Console.Error.WriteLine("stformat: " + opts.Error);
                return 2;
            }

            if (opts.Stdin)
                return RunStdin(opts);

            List<string> files = CollectFiles(opts.Paths, out string? collectError);
            if (collectError != null)
            {
                Console.Error.WriteLine("stformat: " + collectError);
                return 2;
            }
            if (files.Count == 0)
            {
                Console.Error.WriteLine("stformat: nessun file da elaborare");
                return 2;
            }

            int changed = 0, unchanged = 0, errors = 0;
            foreach (string file in files)
            {
                try
                {
                    if (ProcessFile(file, opts)) changed++;
                    else unchanged++;
                }
                catch (Exception ex)
                {
                    errors++;
                    Console.Error.WriteLine("stformat: errore su " + file + ": " + ex.Message);
                }
            }

            if (opts.Mode != OutputMode.Stdout)
                Console.Error.WriteLine(Summary(opts.Mode, changed, unchanged, errors));

            if (errors > 0) return 2;
            if (opts.Mode == OutputMode.Check && changed > 0) return 1;
            return 0;
        }

        // Ritorna true se il contenuto è (o sarebbe) cambiato.
        private static bool ProcessFile(string path, CliOptions opts)
        {
            string original = ReadText(path, out bool bom);
            bool isXml = XmlExtensions.Contains(Path.GetExtension(path))
                         || TcPouFormatter.LooksLikeTwinCatXml(original);

            string formatted = isXml
                ? TcPouFormatter.FormatDocument(original, opts.Format)
                : StFormatter.Format(original, opts.Format);

            if (formatted == original)
                return false;

            switch (opts.Mode)
            {
                case OutputMode.Check:
                    Console.Error.WriteLine("cambierebbe: " + path);
                    break;
                case OutputMode.Diff:
                    Console.Out.Write(Diff.Unified(original, formatted, path));
                    break;
                case OutputMode.Stdout:
                    Console.Out.Write(formatted);
                    break;
                case OutputMode.Write:
                    WriteText(path, formatted, bom);
                    Console.Error.WriteLine("formattato: " + path);
                    break;
            }
            return true;
        }

        private static int RunStdin(CliOptions opts)
        {
            string input = Console.In.ReadToEnd();
            bool isXml = TcPouFormatter.LooksLikeTwinCatXml(input);
            string formatted = isXml
                ? TcPouFormatter.FormatDocument(input, opts.Format)
                : StFormatter.Format(input, opts.Format);

            if (opts.Mode == OutputMode.Check)
                return formatted == input ? 0 : 1;

            Console.Out.Write(formatted);
            return 0;
        }

        private static List<string> CollectFiles(List<string> paths, out string? error)
        {
            error = null;
            var files = new List<string>();
            foreach (string p in paths)
            {
                if (Directory.Exists(p))
                {
                    foreach (string f in Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories))
                        if (ScanExtensions.Contains(Path.GetExtension(f)))
                            files.Add(f);
                }
                else if (File.Exists(p))
                {
                    files.Add(p);
                }
                else
                {
                    error = "percorso inesistente: " + p;
                    return files;
                }
            }
            return files;
        }

        // ---- I/O con gestione del BOM UTF-8 ----

        private static string ReadText(string path, out bool bom)
        {
            byte[] bytes = File.ReadAllBytes(path);
            bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            int start = bom ? 3 : 0;
            return new UTF8Encoding(false).GetString(bytes, start, bytes.Length - start);
        }

        private static void WriteText(string path, string text, bool bom)
        {
            var enc = new UTF8Encoding(encoderShouldEmitUTF8Identifier: bom);
            File.WriteAllText(path, text, enc);
        }

        private static string Summary(OutputMode mode, int changed, int unchanged, int errors)
        {
            string verb = mode == OutputMode.Check ? "da formattare" : "formattati";
            var sb = new StringBuilder();
            sb.Append(changed).Append(' ').Append(verb).Append(", ")
              .Append(unchanged).Append(" già a posto");
            if (errors > 0) sb.Append(", ").Append(errors).Append(" errori");
            return sb.ToString();
        }

        private static void PrintUsage()
        {
            Console.Out.Write(
@"stformat — formatter di Structured Text (IEC 61131-3)

Uso:
  stformat [opzioni] <file|cartella>...
  stformat [opzioni] --stdin < input

Opzioni:
  --check           Non scrive; esce con 1 se qualcosa cambierebbe (per CI).
  --diff            Stampa un diff unificato invece di scrivere.
  --stdout          Stampa il risultato su stdout invece di scrivere il file.
  --stdin           Legge da stdin e scrive su stdout.
  --use-tabs        Indenta con TAB invece che con spazi.
  --indent-size N   Indentazione con N spazi (default 4).
  --tab-width N     Ampiezza del TAB per l'allineamento (default 4).
  --keywords MODE   upper | lower | preserve (default upper).
  -h, --help        Mostra questo aiuto.

File riconosciuti nelle cartelle: .TcPOU .TcGVL .TcDUT .TcIO .exp .st .txt
Nei file XML TwinCAT viene formattato solo il codice ST dentro le CDATA.
");
        }
    }
}
