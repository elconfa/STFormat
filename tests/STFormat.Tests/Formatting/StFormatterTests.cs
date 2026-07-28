using System.Linq;
using STFormat.Core.Formatting;
using STFormat.Core.Lexing;
using Xunit;

namespace STFormat.Tests.Formatting
{
    public class StFormatterTests
    {
        private static string Fmt(string src) => StFormatter.Format(src);

        // ---- Spaziatura ----

        [Fact]
        public void Normalizes_spacing_around_assignment_and_operators()
        {
            Assert.Equal("x := 1;\n", Fmt("x:=1;\n"));
            Assert.Equal("y := a + b * c;\n", Fmt("y:=a+b*c;\n"));
            Assert.Equal("b := x >= 1 AND y <> 2;\n", Fmt("b:=x>=1 AND y<>2;\n"));
        }

        [Fact]
        public void Keeps_calls_members_and_index_tight()
        {
            Assert.Equal("fb.DoStep(a, b);\n", Fmt("fb . DoStep ( a , b ) ;\n"));
            Assert.Equal("x := arr[i] + ptr^.field;\n", Fmt("x:=arr [ i ]+ptr ^ . field;\n"));
        }

        [Fact]
        public void Unary_minus_stays_attached()
        {
            Assert.Equal("x := -1;\n", Fmt("x := - 1;\n"));
            Assert.Equal("y := a * -b;\n", Fmt("y:=a*-b;\n"));
            Assert.Equal("z := (-1) + a;\n", Fmt("z:=(-1)+a;\n"));
        }

        // ---- Case delle keyword ----

        [Fact]
        public void Uppercases_keywords_but_not_identifiers()
        {
            Assert.Equal("IF x THEN y := 1; END_IF\n", Fmt("if x then y:=1; end_if\n"));
        }

        [Fact]
        public void Lowercase_option_is_respected()
        {
            // Nota: 'Y' è un identificatore, non viene toccato dal case delle keyword.
            var opts = new FormatOptions { KeywordCasing = KeywordCasing.Lower };
            Assert.Equal("if x then\n    Y := 1;\nend_if\n",
                StFormatter.Format("IF x THEN\nY:=1;\nEND_IF\n", opts));
        }

        // ---- Indentazione ----

        [Fact]
        public void Indents_if_block()
        {
            Assert.Equal(
                "IF a THEN\n    x := 1;\nEND_IF\n",
                Fmt("IF a THEN\nx := 1;\nEND_IF\n"));
        }

        [Fact]
        public void Indents_nested_if_with_else()
        {
            string input =
                "IF a THEN\nIF b THEN\nx:=1;\nELSE\nx:=2;\nEND_IF\nEND_IF\n";
            string expected =
                "IF a THEN\n" +
                "    IF b THEN\n" +
                "        x := 1;\n" +
                "    ELSE\n" +
                "        x := 2;\n" +
                "    END_IF\n" +
                "END_IF\n";
            Assert.Equal(expected, Fmt(input));
        }

        [Fact]
        public void Indents_for_loop()
        {
            Assert.Equal(
                "FOR i := 1 TO 10 DO\n    sum := sum + i;\nEND_FOR\n",
                Fmt("FOR i:=1 TO 10 DO\nsum:=sum+i;\nEND_FOR\n"));
        }

        [Fact]
        public void Indents_repeat_until()
        {
            Assert.Equal(
                "REPEAT\n    x := x + 1;\nUNTIL x > 10\nEND_REPEAT\n",
                Fmt("REPEAT\nx:=x+1;\nUNTIL x>10\nEND_REPEAT\n"));
        }

        [Fact]
        public void Indents_case_with_labels_and_else()
        {
            string input =
                "CASE n OF\n1:\nx:=1;\n2,3:\nx:=2;\nELSE\nx:=0;\nEND_CASE\n";
            string expected =
                "CASE n OF\n" +
                "    1:\n" +
                "        x := 1;\n" +
                "    2, 3:\n" +
                "        x := 2;\n" +
                "    ELSE\n" +
                "        x := 0;\n" +
                "END_CASE\n";
            Assert.Equal(expected, Fmt(input));
        }

        [Fact]
        public void Indents_var_block_contents()
        {
            // I membri consecutivi vengono allineati a colonne con TAB (':' e ':=').
            string input = "VAR\nnA:INT:=5;\nbEnable:BOOL;\nEND_VAR\n";
            string expected = "VAR\n    nA\t\t: INT\t:= 5;\n    bEnable\t: BOOL;\nEND_VAR\n";
            Assert.Equal(expected, Fmt(input));
        }

        [Fact]
        public void Struct_indents_members_type_does_not()
        {
            string input = "TYPE ST_Data :\nSTRUCT\na:INT;\nb:BOOL;\nEND_STRUCT\nEND_TYPE\n";
            string expected =
                "TYPE ST_Data :\n" +
                "STRUCT\n" +
                "    a\t: INT;\n" +
                "    b\t: BOOL;\n" +
                "END_STRUCT\n" +
                "END_TYPE\n";
            Assert.Equal(expected, Fmt(input));
        }

        [Fact]
        public void Pou_header_does_not_indent_body()
        {
            string input =
                "FUNCTION_BLOCK FB_X\nVAR_INPUT\nbEnable:BOOL;\nEND_VAR\nEND_FUNCTION_BLOCK\n";
            string expected =
                "FUNCTION_BLOCK FB_X\n" +
                "VAR_INPUT\n" +
                "    bEnable : BOOL;\n" +
                "END_VAR\n" +
                "END_FUNCTION_BLOCK\n";
            Assert.Equal(expected, Fmt(input));
        }

        // ---- Continuazione di statement multi-riga ----

        [Fact]
        public void Indents_multiline_statement_continuation()
        {
            // Le righe che continuano uno statement non chiuso vengono rientrate (+1), non lasciate a colonna 0.
            Assert.Equal(
                "xMoving := a\n    AND b\n    AND c;\n",
                Fmt("xMoving := a\nAND b\nAND c;\n"));
        }

        [Fact]
        public void Indents_multiline_if_condition_past_the_body()
        {
            string input = "IF a\nAND b THEN\nx:=1;\nEND_IF\n";
            string expected =
                "IF a\n" +
                "        AND b THEN\n" +   // continuazione della condizione: più a fondo del corpo
                "    x := 1;\n" +
                "END_IF\n";
            Assert.Equal(expected, Fmt(input));
        }

        [Fact]
        public void Pragma_line_does_not_indent_the_next_line()
        {
            // Un pragma {attribute ...} è una riga a sé: non deve far rientrare la riga successiva.
            string input = "{attribute 'qualified_only'}\nVAR_GLOBAL CONSTANT\nnA:INT:=1;\nEND_VAR\n";
            string expected = "{attribute 'qualified_only'}\nVAR_GLOBAL CONSTANT\n    nA : INT := 1;\nEND_VAR\n";
            Assert.Equal(expected, Fmt(input));
        }

        [Fact]
        public void Continuation_indent_is_idempotent()
        {
            string once = Fmt("xMoving := a\nAND b\nAND c;\n");
            Assert.Equal(once, Fmt(once));
        }

        // ---- Commenti ----

        [Fact]
        public void Preserves_and_indents_comments()
        {
            string input = "IF a THEN // check\n// note\nx:=1; (* set *)\nEND_IF\n";
            string expected =
                "IF a THEN // check\n" +
                "    // note\n" +
                "    x := 1; (* set *)\n" +
                "END_IF\n";
            Assert.Equal(expected, Fmt(input));
        }

        // ---- Righe vuote ----

        [Fact]
        public void Collapses_and_trims_blank_lines()
        {
            Assert.Equal("x := 1;\n\ny := 2;\n", Fmt("x:=1;\n\n\n\ny:=2;\n"));
            Assert.Equal("x := 1;\n", Fmt("\n\nx:=1;\n\n\n"));
        }

        [Fact]
        public void Empty_input_yields_empty()
        {
            Assert.Equal("", Fmt(""));
            Assert.Equal("", Fmt("   \n\n  \n"));
        }

        // ---- Fine riga ----

        [Fact]
        public void Detects_and_preserves_crlf()
        {
            Assert.Equal("x := 1;\r\n", Fmt("x:=1;\r\n"));
        }

        [Fact]
        public void Ensures_single_trailing_newline_when_missing()
        {
            // Nessun a-capo nel sorgente: si usa il fallback "\r\n" (stile TwinCAT).
            Assert.Equal("x := 1;\r\n", Fmt("x:=1;"));
        }

        // ---- Proprietà di sicurezza ----

        private const string Sample =
            "FUNCTION_BLOCK FB_Demo\r\n" +
            "VAR\r\n" +
            "nCount:INT;\r\n" +
            "END_VAR\r\n" +
            "IF nCount<10 THEN\r\n" +
            "nCount:=nCount+1;\r\n" +
            "CASE nCount OF\r\n" +
            "1:\r\n" +
            "bFlag:=TRUE;\r\n" +
            "ELSE\r\n" +
            "bFlag:=FALSE;\r\n" +
            "END_CASE\r\n" +
            "END_IF\r\n" +
            "END_FUNCTION_BLOCK\r\n";

        [Fact]
        public void Is_idempotent()
        {
            string once = Fmt(Sample);
            string twice = Fmt(once);
            Assert.Equal(once, twice);
        }

        [Fact]
        public void Preserves_significant_tokens_modulo_keyword_case()
        {
            string formatted = Fmt(Sample);
            var before = SignificantTokens(Sample);
            var after = SignificantTokens(formatted);

            Assert.Equal(before.Count, after.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.Equal(before[i].Kind, after[i].Kind);
                if (before[i].Kind == TokenKind.Keyword)
                    Assert.Equal(before[i].Text, after[i].Text, ignoreCase: true);
                else
                    Assert.Equal(before[i].Text, after[i].Text);
            }
        }

        private static System.Collections.Generic.List<Token> SignificantTokens(string src)
            => StLexer.Tokenize(src)
                .Where(t => t.Kind != TokenKind.EndOfFile && !t.IsTrivia)
                .ToList();
    }
}
