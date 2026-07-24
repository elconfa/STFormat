using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace STFormat.Cli
{
    /// <summary>Diff unificato minimale (LCS su righe) per la modalità --diff.</summary>
    internal static class Diff
    {
        private static string[] SplitLines(string s) => Regex.Split(s, "\r\n|\r|\n");

        public static string Unified(string oldText, string newText, string label)
        {
            string[] a = SplitLines(oldText);
            string[] b = SplitLines(newText);

            // LCS
            int n = a.Length, m = b.Length;
            var lcs = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
                for (int j = m - 1; j >= 0; j--)
                    lcs[i, j] = a[i] == b[j] ? lcs[i + 1, j + 1] + 1
                                             : (lcs[i + 1, j] >= lcs[i, j + 1] ? lcs[i + 1, j] : lcs[i, j + 1]);

            // Script di edit
            var ops = new List<(char op, string line)>();
            int x = 0, y = 0;
            while (x < n && y < m)
            {
                if (a[x] == b[y]) { ops.Add((' ', a[x])); x++; y++; }
                else if (lcs[x + 1, y] >= lcs[x, y + 1]) { ops.Add(('-', a[x])); x++; }
                else { ops.Add(('+', b[y])); y++; }
            }
            while (x < n) { ops.Add(('-', a[x])); x++; }
            while (y < m) { ops.Add(('+', b[y])); y++; }

            bool anyChange = false;
            foreach (var op in ops) if (op.op != ' ') { anyChange = true; break; }
            if (!anyChange) return string.Empty;

            var sb = new StringBuilder();
            sb.Append("--- ").Append(label).Append('\n');
            sb.Append("+++ ").Append(label).Append(" (formattato)\n");
            foreach (var (op, line) in ops)
                sb.Append(op).Append(line).Append('\n');
            return sb.ToString();
        }
    }
}
