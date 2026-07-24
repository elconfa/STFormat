using STFormat.Core.Formatting;
using Xunit;

namespace STFormat.Tests.Formatting
{
    public class AlignmentTests
    {
        private static string Fmt(string src) => StFormatter.Format(src);

        // Colonna visiva (0-based) fino all'indice dato, con tab da 4.
        private static int VisualColumn(string line, int upto, int tabWidth = 4)
        {
            int col = 0;
            for (int i = 0; i < upto; i++)
                col = line[i] == '\t' ? (col / tabWidth + 1) * tabWidth : col + 1;
            return col;
        }

        // ---- Allineamento con TAB (non spazi) ----

        [Fact]
        public void Aligns_declaration_colon_and_init_with_tabs()
        {
            string input = "VAR\nnA:INT:=5;\nbEnable:BOOL:=TRUE;\nEND_VAR\n";
            string expected =
                "VAR\n" +
                "    nA\t\t: INT\t:= 5;\n" +
                "    bEnable\t: BOOL\t:= TRUE;\n" +
                "END_VAR\n";
            Assert.Equal(expected, Fmt(input));
        }

        [Fact]
        public void Aligns_consecutive_assignments_with_tabs()
        {
            string input = "x:=1;\nlongVar:=2;\n";
            string expected = "x\t\t:= 1;\nlongVar\t:= 2;\n";
            Assert.Equal(expected, Fmt(input));
        }

        [Fact]
        public void Alignment_padding_uses_tabs_not_spaces()
        {
            string output = Fmt("VAR\nnA:INT;\nbEnable:BOOL;\nEND_VAR\n");
            // Il riempimento di allineamento deve essere fatto con TAB...
            Assert.Contains("\t", output);
            // ...quindi il ':' allineato è preceduto da un tab, mai da spazi (nessun " :").
            Assert.DoesNotContain(" :", output);
            Assert.Contains("\t:", output);
        }

        [Fact]
        public void Trailing_comments_align_to_same_column()
        {
            string output = Fmt(
                "VAR\nnA:INT:=5;// giri\nbEnable:BOOL:=TRUE;// on\nEND_VAR\n");
            string[] lines = output.Split('\n');
            string l1 = lines[1];
            string l2 = lines[2];

            int c1 = VisualColumn(l1, l1.IndexOf("//"));
            int c2 = VisualColumn(l2, l2.IndexOf("//"));
            Assert.Equal(c1, c2);
            Assert.True(c1 % 4 == 0, "la colonna del commento deve essere un tab stop");
        }

        [Fact]
        public void Declaration_colons_align_to_same_column()
        {
            string output = Fmt("VAR\nnA:INT;\nbEnable:BOOL;\nnLongName:DINT;\nEND_VAR\n");
            string[] lines = output.Split('\n');
            int c1 = VisualColumn(lines[1], lines[1].IndexOf(':'));
            int c2 = VisualColumn(lines[2], lines[2].IndexOf(':'));
            int c3 = VisualColumn(lines[3], lines[3].IndexOf(':'));
            Assert.Equal(c1, c2);
            Assert.Equal(c2, c3);
        }

        // ---- Non allineare quando non ha senso ----

        [Fact]
        public void Single_declaration_is_not_tab_aligned()
        {
            Assert.Equal("VAR\n    nA : INT;\nEND_VAR\n", Fmt("VAR\nnA:INT;\nEND_VAR\n"));
        }

        [Fact]
        public void Single_assignment_is_not_tab_aligned()
        {
            Assert.Equal("x := 1;\n", Fmt("x:=1;\n"));
        }

        [Fact]
        public void Named_arguments_are_not_treated_as_alignable_assignments()
        {
            // I ':=' dentro le parentesi (parametri) non sono assegnazioni di livello 0.
            string input = "fbA(IN:=TRUE, PT:=T#5s);\nfbLongName(EN:=FALSE);\n";
            string expected = "fbA(IN := TRUE, PT := T#5s);\nfbLongName(EN := FALSE);\n";
            Assert.Equal(expected, Fmt(input));
        }

        [Fact]
        public void Alignment_can_be_disabled()
        {
            var opts = new FormatOptions
            {
                AlignDeclarations = false,
                AlignAssignments = false,
                AlignTrailingComments = false
            };
            Assert.Equal(
                "VAR\n    nA : INT := 5;\n    bEnable : BOOL;\nEND_VAR\n",
                StFormatter.Format("VAR\nnA:INT:=5;\nbEnable:BOOL;\nEND_VAR\n", opts));
        }

        // ---- Sicurezza ----

        private const string AlignSample =
            "VAR\n" +
            "nA:INT:=5;// giri\n" +
            "bEnable:BOOL;// on\n" +
            "nLongName:DINT:=100;\n" +
            "END_VAR\n" +
            "x:=1;\n" +
            "longVar:=nA+nLongName;\n";

        [Fact]
        public void Alignment_is_idempotent()
        {
            string once = Fmt(AlignSample);
            string twice = Fmt(once);
            Assert.Equal(once, twice);
        }
    }
}
