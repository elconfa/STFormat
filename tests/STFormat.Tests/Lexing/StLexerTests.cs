using System.Linq;
using STFormat.Core.Lexing;
using Xunit;

namespace STFormat.Tests.Lexing
{
    public class StLexerTests
    {
        private static string Reassemble(string src)
            => string.Concat(StLexer.Tokenize(src).Select(t => t.Text));

        private static Token[] Significant(string src)
            => StLexer.Tokenize(src)
                .Where(t => t.Kind != TokenKind.EndOfFile && !t.IsTrivia)
                .ToArray();

        // Tutti i token tranne la sentinella EOF (trivia inclusa: commenti, whitespace...).
        private static Token[] NonEof(string src)
            => StLexer.Tokenize(src)
                .Where(t => t.Kind != TokenKind.EndOfFile)
                .ToArray();

        // ---- Round-trip: proprietà fondamentale del lexer ----

        [Theory]
        [InlineData("")]
        [InlineData("x := 1;")]
        [InlineData("IF a AND b THEN\r\n    c := 1;\r\nEND_IF\n")]
        [InlineData("nMotor : INT := 5; // giri\r\nbEnable : BOOL := TRUE;")]
        [InlineData("(* commento (* annidato *) qui *) x := 1;")]
        [InlineData("s := 'a$'b'; w := \"hi\"; t := T#5s;")]
        [InlineData("arr : ARRAY[1..10] OF INT;")]
        [InlineData("fbTon(IN := TRUE, PT := T#500MS, Q => bDone);")]
        [InlineData("r := 1.0e-3 + 16#FF - 2#1010;")]
        [InlineData("bIn AT %IX0.0 : BOOL;")]
        [InlineData("{attribute 'hide'}\r\nnSecret : INT;")]
        public void RoundTrip_reassembles_source_exactly(string src)
        {
            Assert.Equal(src, Reassemble(src));
        }

        [Fact]
        public void RoundTrip_on_multiline_sample()
        {
            string src =
                "FUNCTION_BLOCK FB_Test\r\n" +
                "VAR_INPUT\r\n" +
                "    bEnable : BOOL := FALSE; (* abilita *)\r\n" +
                "    nSpeed  : INT  := 100;   // rpm\r\n" +
                "END_VAR\r\n" +
                "IF bEnable THEN\r\n" +
                "    nCount := nCount + 1;\r\n" +
                "END_IF\r\n";
            Assert.Equal(src, Reassemble(src));
        }

        // ---- Classificazione ----

        [Fact]
        public void Keywords_are_case_insensitive()
        {
            Assert.Equal(TokenKind.Keyword, Significant("IF")[0].Kind);
            Assert.Equal(TokenKind.Keyword, Significant("if")[0].Kind);
            Assert.Equal(TokenKind.Keyword, Significant("Function_Block")[0].Kind);
        }

        [Fact]
        public void Identifiers_are_not_keywords()
        {
            var t = Significant("myVar_1")[0];
            Assert.Equal(TokenKind.Identifier, t.Kind);
            Assert.Equal("myVar_1", t.Text);
        }

        [Theory]
        [InlineData(":=")]
        [InlineData("=>")]
        [InlineData("<=")]
        [InlineData(">=")]
        [InlineData("<>")]
        [InlineData("..")]
        public void Multichar_operators_are_single_tokens(string op)
        {
            var toks = Significant($"a {op} b");
            Assert.Equal(3, toks.Length);
            Assert.Equal(TokenKind.Operator, toks[1].Kind);
            Assert.Equal(op, toks[1].Text);
        }

        [Fact]
        public void Member_access_dot_is_operator_not_number()
        {
            var toks = Significant("fb.Output");
            Assert.Equal(3, toks.Length);
            Assert.Equal(TokenKind.Identifier, toks[0].Kind);
            Assert.Equal(TokenKind.Operator, toks[1].Kind);
            Assert.Equal(".", toks[1].Text);
            Assert.Equal(TokenKind.Identifier, toks[2].Kind);
        }

        [Fact]
        public void Range_is_two_numbers_and_dotdot()
        {
            var toks = Significant("1..10");
            Assert.Equal(3, toks.Length);
            Assert.Equal(TokenKind.Number, toks[0].Kind);
            Assert.Equal("..", toks[1].Text);
            Assert.Equal(TokenKind.Number, toks[2].Kind);
        }

        // ---- Stringhe ----

        [Fact]
        public void String_with_escaped_quote_is_one_token()
        {
            // In ST "$" fa da escape: 'a$'b' è UNA stringa che contiene una virgoletta.
            var toks = Significant("'a$'b'");
            Assert.Single(toks);
            Assert.Equal(TokenKind.String, toks[0].Kind);
            Assert.Equal("'a$'b'", toks[0].Text);
        }

        [Fact]
        public void Wide_string_uses_double_quotes()
        {
            var toks = Significant("\"ciao\"");
            Assert.Single(toks);
            Assert.Equal(TokenKind.String, toks[0].Kind);
        }

        [Fact]
        public void Assignment_inside_string_is_not_an_operator()
        {
            // Il ':=' dentro la stringa non deve diventare un operatore.
            var toks = Significant("msg := ':=';");
            Assert.Equal(TokenKind.Identifier, toks[0].Kind);
            Assert.Equal(TokenKind.Operator, toks[1].Kind);   // :=
            Assert.Equal(TokenKind.String, toks[2].Kind);     // ':='
            Assert.Equal(TokenKind.Operator, toks[3].Kind);   // ;
            Assert.Equal(4, toks.Length);
        }

        // ---- Commenti ----

        [Fact]
        public void Line_comment_excludes_newline()
        {
            var all = StLexer.Tokenize("x // hi\n");
            var comment = all.First(t => t.Kind == TokenKind.LineComment);
            Assert.Equal("// hi", comment.Text);
            Assert.Contains(all, t => t.Kind == TokenKind.NewLine);
        }

        [Fact]
        public void Nested_block_comment_is_one_token()
        {
            var toks = NonEof("(* a (* b *) c *)");
            Assert.Single(toks);
            Assert.Equal(TokenKind.BlockComment, toks[0].Kind);
            Assert.Equal("(* a (* b *) c *)", toks[0].Text);
        }

        [Fact]
        public void Assignment_inside_comment_is_not_an_operator()
        {
            var toks = NonEof("(* x := 1 *)");
            Assert.Single(toks);
            Assert.Equal(TokenKind.BlockComment, toks[0].Kind);
        }

        // ---- Letterali tipizzati ----

        [Theory]
        [InlineData("T#5s")]
        [InlineData("TIME#500ms")]
        [InlineData("DT#2020-01-01-12:00:00")]
        [InlineData("E_State#Idle")]
        public void Typed_literals_are_single_tokens(string literal)
        {
            var toks = Significant(literal);
            Assert.Single(toks);
            Assert.Equal(TokenKind.TypedLiteral, toks[0].Kind);
            Assert.Equal(literal, toks[0].Text);
        }

        [Fact]
        public void Based_integer_is_a_number()
        {
            var toks = Significant("16#FF");
            Assert.Single(toks);
            Assert.Equal(TokenKind.Number, toks[0].Kind);
            Assert.Equal("16#FF", toks[0].Text);
        }

        [Fact]
        public void Direct_address_is_atomic()
        {
            var toks = Significant("%IX0.0");
            Assert.Single(toks);
            Assert.Equal(TokenKind.TypedLiteral, toks[0].Kind);
            Assert.Equal("%IX0.0", toks[0].Text);
        }

        // ---- Posizioni ----

        [Fact]
        public void Tracks_line_and_column()
        {
            var toks = StLexer.Tokenize("a\r\nbb");
            var bb = toks.First(t => t.Text == "bb");
            Assert.Equal(2, bb.Line);
            Assert.Equal(1, bb.Column);
        }
    }
}
