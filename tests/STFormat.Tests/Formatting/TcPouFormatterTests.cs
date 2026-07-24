using STFormat.Core.Formatting;
using Xunit;

namespace STFormat.Tests.Formatting
{
    public class TcPouFormatterTests
    {
        [Fact]
        public void Formats_declaration_and_implementation_cdata()
        {
            string xml =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<TcPlcObject Version=\"1.1.0.1\">\r\n" +
                "  <POU Name=\"FB_X\">\r\n" +
                "    <Declaration><![CDATA[FUNCTION_BLOCK FB_X\r\nVAR\r\nnA:INT:=5;\r\nEND_VAR]]></Declaration>\r\n" +
                "    <Implementation>\r\n" +
                "      <ST><![CDATA[IF nA>0 THEN\r\nnA:=nA-1;\r\nEND_IF]]></ST>\r\n" +
                "    </Implementation>\r\n" +
                "  </POU>\r\n" +
                "</TcPlcObject>";

            string result = TcPouFormatter.FormatDocument(xml);

            // Il codice dentro le CDATA è stato formattato...
            Assert.Contains("    nA : INT := 5;", result);     // singola decl -> spazi
            Assert.Contains("IF nA > 0 THEN\r\n    nA := nA - 1;\r\nEND_IF", result);
            // ...e la struttura XML attorno è preservata.
            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", result);
            Assert.Contains("<POU Name=\"FB_X\">", result);
            Assert.Contains("</TcPlcObject>", result);
        }

        [Fact]
        public void Preserves_crlf_and_no_trailing_newline_before_cdata_close()
        {
            string xml = "<TcPlcObject><Declaration><![CDATA[VAR\r\na:INT;\r\nEND_VAR]]></Declaration></TcPlcObject>";
            string result = TcPouFormatter.FormatDocument(xml);

            // Nessun newline aggiunto prima di ]]> (l'originale non ne aveva).
            Assert.Contains("END_VAR]]></Declaration>", result);
            // I fine riga restano CRLF.
            Assert.Contains("a : INT;\r\n", result);
        }

        [Fact]
        public void Is_idempotent_on_documents()
        {
            string xml =
                "<TcPlcObject><POU Name=\"P\">" +
                "<Declaration><![CDATA[VAR\r\nnA:INT:=5;\r\nbEnable:BOOL;\r\nEND_VAR]]></Declaration>" +
                "<Implementation><ST><![CDATA[x:=1;\r\nlongVar:=2;]]></ST></Implementation>" +
                "</POU></TcPlcObject>";

            string once = TcPouFormatter.FormatDocument(xml);
            string twice = TcPouFormatter.FormatDocument(once);
            Assert.Equal(once, twice);
        }

        [Fact]
        public void Leaves_non_twincat_text_untouched_structurally()
        {
            // Nessun elemento Declaration/ST: nulla da sostituire.
            string xml = "<Other><Value><![CDATA[x:=1;]]></Value></Other>";
            Assert.Equal(xml, TcPouFormatter.FormatDocument(xml));
        }
    }
}
