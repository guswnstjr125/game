using System.Collections.Generic;
using DarkLegion.Core;
using NUnit.Framework;

namespace DarkLegion.Tests.EditMode
{
    /// <summary>
    /// Core JSON 읽기 (#35 Unity 비의존 부분, `38` §2 형식). 외부 라이브러리 없이 export_game_data.py 출력을 읽는다.
    /// </summary>
    [Category("Stage1")]
    public class JsonReaderTests
    {
        [Test]
        public void Parse_ObjectArrayAndScalars()
        {
            var o = (Dictionary<string, object>)Json.Parse("{ \"a\": 1, \"b\": [true, false, null], \"c\": \"x\" }");
            Assert.AreEqual(1.0, o["a"]);
            var b = (List<object>)o["b"];
            Assert.AreEqual(3, b.Count);
            Assert.AreEqual(true, b[0]);
            Assert.AreEqual(false, b[1]);
            Assert.IsNull(b[2]);
            Assert.AreEqual("x", o["c"]);
        }

        [Test]
        public void Parse_Numbers()
        {
            var a = (List<object>)Json.Parse("[0, -3, 1.5, 0.14, 2e3, -1.25E-2]");
            Assert.AreEqual(new object[] { 0.0, -3.0, 1.5, 0.14, 2000.0, -0.0125 }, a.ToArray());
        }

        [Test]
        public void Parse_StringEscapesAndKorean()
        {
            var s = (string)Json.Parse("\"뼈 \\\"병사\\\" \\\\ \\n \\u00e9 \\uac00\"");
            Assert.AreEqual("뼈 \"병사\" \\ \n é 가", s);
        }

        [Test]
        public void Parse_KeepsKeyOrderAsWritten()
        {
            var o = (Dictionary<string, object>)Json.Parse("{\"z\":1,\"a\":2,\"m\":3}");
            Assert.AreEqual(new[] { "z", "a", "m" }, new List<string>(o.Keys).ToArray());
        }

        [TestCase("{\"a\":1")]
        [TestCase("[1,2")]
        [TestCase("\"abc")]
        [TestCase("{\"a\" 1}")]
        [TestCase("[1] x")]
        [TestCase("tru")]
        [TestCase("")]
        public void Parse_MalformedThrowsFormatException(string text)
        {
            Assert.Throws<System.FormatException>(() => Json.Parse(text));
        }

        [Test]
        public void DataSheet_FromExportFormat()
        {
            const string text = "{\n  \"source\": \"08_카드_데이터베이스.xlsx/Cards_Master\",\n  \"rows\": [\n    {\"ID\": \"NEC_MON_001\", \"HP\": 10, \"PP\": 8.4, \"Exhaust\": true, \"Note\": null}\n  ]\n}\n";
            var sheet = DataSheet.FromJson("Cards_Master", text);
            Assert.AreEqual("Cards_Master", sheet.Name);
            Assert.AreEqual("08_카드_데이터베이스.xlsx/Cards_Master", sheet.Source);
            Assert.AreEqual(1, sheet.Rows.Count);
            var r = sheet.Rows[0];
            Assert.AreEqual("NEC_MON_001", r.Str("ID"));
            Assert.AreEqual(10, r.Int("HP"));
            Assert.AreEqual(8.4, r.Num("PP"));
            Assert.IsTrue(r.Bool("Exhaust"));
            Assert.IsFalse(r.Has("Note"), "null은 없는 값");
            Assert.AreEqual("", r.Str("Note"));
            Assert.AreEqual(7, r.Int("Missing", 7), "없는 열은 기본값");
        }

        [Test]
        public void DataRow_NumberAsString_AndStringNumberIsNotANumber()
        {
            var r = new DataRow(new Dictionary<string, object> { ["Value"] = "ENE_1F_GHO_001", ["Level"] = 2.0 });
            Assert.AreEqual("2", r.Str("Level"));
            Assert.AreEqual(0, r.Num("Value"), "문자열은 숫자로 읽지 않는다");
            Assert.IsFalse(r.IsNumber("Value"));
            Assert.IsTrue(r.IsNumber("Level"));
        }

        [Test]
        public void Parse_DuplicateKeyThrows_D74()
        {
            var e = Assert.Throws<System.FormatException>(() => Json.Parse("{\"HP\": 10, \"HP\": 12}"));
            StringAssert.Contains("HP", e.Message);
            Assert.DoesNotThrow(() => Json.Parse("[{\"HP\": 10}, {\"HP\": 12}]"), "다른 객체의 같은 키는 괜찮다");
        }

        [Test]
        public void DataSheet_SourceMustBeStringOrNull_D74()
        {
            var e = Assert.Throws<System.FormatException>(() => DataSheet.FromJson("Cards_Master", "{\"source\": {}, \"rows\": []}"));
            StringAssert.Contains("source", e.Message);
            Assert.AreEqual("", DataSheet.FromJson("X", "{\"source\": null, \"rows\": []}").Source);
        }

        [Test]
        public void DataSheet_MissingRowsThrows()
        {
            Assert.Throws<System.FormatException>(() => DataSheet.FromJson("X", "{\"source\":\"x\"}"));
        }
    }
}
