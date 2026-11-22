using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DarkLegion.Core
{
    /// <summary>
    /// 외부 라이브러리 없는 JSON 읽기 (`38` §2 형식, #35 Unity 비의존 부분).
    /// Core는 UnityEngine·패키지를 참조하지 않으므로(noEngineReferences) Unity와 dotnet 테스트에서 같은 코드로 읽는다.
    /// 결과: 객체 → Dictionary&lt;string, object&gt;(키 순서 유지), 배열 → List&lt;object&gt;, 문자열, 숫자 → double, true/false, null.
    /// 형식 오류는 위치와 함께 FormatException.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            if (text == null) throw new System.FormatException("JSON: null text");
            var p = new Parser(text);
            p.SkipWhitespace();
            var value = p.ReadValue();
            p.SkipWhitespace();
            if (!p.AtEnd) throw p.Error("trailing characters");
            return value;
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s)
            {
                _s = s;
            }

            public bool AtEnd => _i >= _s.Length;

            public System.FormatException Error(string message) => new System.FormatException($"JSON: {message} at {_i}");

            public void SkipWhitespace()
            {
                while (_i < _s.Length && (_s[_i] == ' ' || _s[_i] == '\t' || _s[_i] == '\n' || _s[_i] == '\r')) _i++;
            }

            private char Peek()
            {
                if (AtEnd) throw Error("unexpected end");
                return _s[_i];
            }

            private void Expect(char c)
            {
                if (Peek() != c) throw Error($"expected '{c}'");
                _i++;
            }

            public object ReadValue()
            {
                switch (Peek())
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': return ReadWord("true", true);
                    case 'f': return ReadWord("false", false);
                    case 'n': return ReadWord("null", null);
                    default: return ReadNumber();
                }
            }

            private object ReadWord(string word, object value)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw Error($"expected {word}");
                _i += word.Length;
                return value;
            }

            private Dictionary<string, object> ReadObject()
            {
                var o = new Dictionary<string, object>();
                Expect('{');
                SkipWhitespace();
                if (Peek() == '}')
                {
                    _i++;
                    return o;
                }
                while (true)
                {
                    SkipWhitespace();
                    string key = ReadString();
                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    o[key] = ReadValue();
                    SkipWhitespace();
                    if (Peek() == ',')
                    {
                        _i++;
                        continue;
                    }
                    Expect('}');
                    return o;
                }
            }

            private List<object> ReadArray()
            {
                var a = new List<object>();
                Expect('[');
                SkipWhitespace();
                if (Peek() == ']')
                {
                    _i++;
                    return a;
                }
                while (true)
                {
                    SkipWhitespace();
                    a.Add(ReadValue());
                    SkipWhitespace();
                    if (Peek() == ',')
                    {
                        _i++;
                        continue;
                    }
                    Expect(']');
                    return a;
                }
            }

            private string ReadString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (true)
                {
                    char c = Peek();
                    _i++;
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    char e = Peek();
                    _i++;
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Error("bad \\u escape");
                            if (!int.TryParse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code)) throw Error("bad \\u escape");
                            sb.Append((char)code);
                            _i += 4;
                            break;
                        default: throw Error($"bad escape \\{e}");
                    }
                }
            }

            private double ReadNumber()
            {
                int start = _i;
                if (!AtEnd && _s[_i] == '-') _i++;
                while (!AtEnd && (char.IsDigit(_s[_i]) || _s[_i] == '.' || _s[_i] == 'e' || _s[_i] == 'E' || _s[_i] == '+' || _s[_i] == '-')) _i++;
                if (_i == start) throw Error("unexpected character");
                string token = _s.Substring(start, _i - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) throw Error($"bad number {token}");
                return v;
            }
        }
    }

    /// <summary>시트 한 행 (`38` §2: 열 이름 = Excel 1행 머리글). null·없는 열은 기본값을 돌려준다.</summary>
    public sealed class DataRow
    {
        private readonly IReadOnlyDictionary<string, object> _values;

        public DataRow(IReadOnlyDictionary<string, object> values)
        {
            _values = values ?? new Dictionary<string, object>();
        }

        public IEnumerable<string> Columns => _values.Keys;

        /// <summary>값이 있다 (null이 아니다).</summary>
        public bool Has(string column) => _values.TryGetValue(column, out var v) && v != null;

        public bool IsNumber(string column) => _values.TryGetValue(column, out var v) && v is double;

        public string Str(string column)
        {
            if (!_values.TryGetValue(column, out var v) || v == null) return "";
            if (v is double d) return d.ToString(CultureInfo.InvariantCulture);
            if (v is bool b) return b ? "true" : "false";
            return v as string ?? v.ToString();
        }

        public double Num(string column, double fallback = 0) => _values.TryGetValue(column, out var v) && v is double d ? d : fallback;

        public int Int(string column, int fallback = 0) => _values.TryGetValue(column, out var v) && v is double d ? (int)System.Math.Round(d) : fallback;

        public bool Bool(string column) => _values.TryGetValue(column, out var v) && v is bool b && b;
    }

    /// <summary>export_game_data.py가 쓴 시트 하나 (`{"source": …, "rows": […]}`).</summary>
    public sealed class DataSheet
    {
        public string Name { get; }
        public string Source { get; }
        public IReadOnlyList<DataRow> Rows { get; }

        public DataSheet(string name, string source, IReadOnlyList<DataRow> rows)
        {
            Name = name;
            Source = source ?? "";
            Rows = rows ?? new List<DataRow>();
        }

        public static DataSheet FromJson(string name, string json)
        {
            if (!(Json.Parse(json) is Dictionary<string, object> root) || !(root.TryGetValue("rows", out var rowsObj) && rowsObj is List<object> rows))
            {
                throw new System.FormatException($"{name}: expected {{\"source\", \"rows\"}}");
            }
            var list = new List<DataRow>(rows.Count);
            foreach (var r in rows)
            {
                if (!(r is Dictionary<string, object> d)) throw new System.FormatException($"{name}: row is not an object");
                list.Add(new DataRow(d));
            }
            return new DataSheet(name, root.TryGetValue("source", out var s) ? s as string : "", list);
        }
    }
}
