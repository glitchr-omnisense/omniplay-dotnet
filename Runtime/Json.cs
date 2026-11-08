using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Omniplay
{
    /// <summary>
    /// A JSON object that keeps its keys in the order they came: a ticket's payload is signed as it is
    /// written, so its order is part of it.
    /// </summary>
    public sealed class JsonObject : IReadOnlyDictionary<string, object>
    {
        private readonly List<KeyValuePair<string, object>> pairs = new List<KeyValuePair<string, object>>();
        private readonly Dictionary<string, int> index = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Sets a key: replaced where it stands, or added last.</summary>
        public JsonObject Set(string key, object value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (index.TryGetValue(key, out var at)) pairs[at] = new KeyValuePair<string, object>(key, value);
            else
            {
                index[key] = pairs.Count;
                pairs.Add(new KeyValuePair<string, object>(key, value));
            }
            return this;
        }

        /// <summary>Collection initialisers: new JsonObject { { "room", "lobby" }, { "exp", 4102444800L } }.</summary>
        public void Add(string key, object value) => Set(key, value);

        public object this[string key] => pairs[index[key]].Value;
        public IEnumerable<string> Keys { get { foreach (var pair in pairs) yield return pair.Key; } }
        public IEnumerable<object> Values { get { foreach (var pair in pairs) yield return pair.Value; } }
        public int Count => pairs.Count;
        public bool ContainsKey(string key) => index.ContainsKey(key);

        public bool TryGetValue(string key, out object value)
        {
            if (index.TryGetValue(key, out var at))
            {
                value = pairs[at].Value;
                return true;
            }
            value = null;
            return false;
        }

        /// <summary>A string value; null when absent or not a string.</summary>
        public string String(string key) => TryGetValue(key, out var value) ? value as string : null;

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => pairs.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public override string ToString() => Json.Write(this);
    }

    /// <summary>
    /// JSON read and written with nothing but the base library. Read: objects as JsonObject (their order
    /// kept), lists as List&lt;object&gt;, integers as long (double past it), other numbers as double,
    /// strings, booleans, null. Written as PHP's json_encode() writes it with its default flags: "/" as
    /// "\/", every character past ASCII as \uXXXX (UTF-16 units: surrogate pairs past U+FFFF), so that
    /// a ticket signed here is byte for byte the one PHP signs - with integers, strings, booleans, null,
    /// lists and objects (a double as "R" writes it: 1.5, and 2 for 2.0 as PHP; past 15 digits PHP's
    /// exponent form may differ).
    /// </summary>
    public static class Json
    {
        private const int MaxDepth = 64;

        public static bool TryParse(string text, out object value)
        {
            try
            {
                value = Parse(text);
                return true;
            }
            catch (FormatException)
            {
                value = null;
                return false;
            }
        }

        /// <exception cref="FormatException">when the text is not one JSON value</exception>
        public static object Parse(string text)
        {
            if (text == null) throw new FormatException("No JSON.");
            var at = 0;
            var value = ReadValue(text, ref at, 0);
            SkipSpace(text, ref at);
            if (at != text.Length) throw new FormatException("Text after the JSON value at " + at + ".");
            return value;
        }

        public static string Write(object value)
        {
            var output = new StringBuilder();
            WriteValue(output, value, 0);
            return output.ToString();
        }

        private static object ReadValue(string s, ref int at, int depth)
        {
            if (depth > MaxDepth) throw new FormatException("JSON nested too deep.");
            SkipSpace(s, ref at);
            if (at >= s.Length) throw new FormatException("JSON ends early.");
            switch (s[at])
            {
                case '{': return ReadObject(s, ref at, depth);
                case '[': return ReadList(s, ref at, depth);
                case '"': return ReadString(s, ref at);
                case 't': Expect(s, ref at, "true"); return true;
                case 'f': Expect(s, ref at, "false"); return false;
                case 'n': Expect(s, ref at, "null"); return null;
                default: return ReadNumber(s, ref at);
            }
        }

        private static JsonObject ReadObject(string s, ref int at, int depth)
        {
            var obj = new JsonObject();
            at++;
            SkipSpace(s, ref at);
            if (at < s.Length && s[at] == '}') { at++; return obj; }
            while (true)
            {
                SkipSpace(s, ref at);
                if (at >= s.Length || s[at] != '"') throw new FormatException("A key expected at " + at + ".");
                var key = ReadString(s, ref at);
                SkipSpace(s, ref at);
                if (at >= s.Length || s[at] != ':') throw new FormatException("':' expected at " + at + ".");
                at++;
                obj.Set(key, ReadValue(s, ref at, depth + 1));
                SkipSpace(s, ref at);
                if (at < s.Length && s[at] == ',') { at++; continue; }
                if (at < s.Length && s[at] == '}') { at++; return obj; }
                throw new FormatException("',' or '}' expected at " + at + ".");
            }
        }

        private static List<object> ReadList(string s, ref int at, int depth)
        {
            var list = new List<object>();
            at++;
            SkipSpace(s, ref at);
            if (at < s.Length && s[at] == ']') { at++; return list; }
            while (true)
            {
                list.Add(ReadValue(s, ref at, depth + 1));
                SkipSpace(s, ref at);
                if (at < s.Length && s[at] == ',') { at++; continue; }
                if (at < s.Length && s[at] == ']') { at++; return list; }
                throw new FormatException("',' or ']' expected at " + at + ".");
            }
        }

        private static string ReadString(string s, ref int at)
        {
            var output = new StringBuilder();
            at++;
            while (at < s.Length)
            {
                var c = s[at++];
                if (c == '"') return output.ToString();
                if (c < 0x20) throw new FormatException("A control character in a string at " + (at - 1) + ".");
                if (c != '\\') { output.Append(c); continue; }
                if (at >= s.Length) break;
                var e = s[at++];
                switch (e)
                {
                    case '"': output.Append('"'); break;
                    case '\\': output.Append('\\'); break;
                    case '/': output.Append('/'); break;
                    case 'b': output.Append('\b'); break;
                    case 'f': output.Append('\f'); break;
                    case 'n': output.Append('\n'); break;
                    case 'r': output.Append('\r'); break;
                    case 't': output.Append('\t'); break;
                    case 'u':
                        if (at + 4 > s.Length || !int.TryParse(s.Substring(at, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var unit))
                            throw new FormatException("A \\u escape of four hex digits expected at " + at + ".");
                        output.Append((char)unit);
                        at += 4;
                        break;
                    default: throw new FormatException("An unknown escape at " + (at - 1) + ".");
                }
            }
            throw new FormatException("A string not closed.");
        }

        private static object ReadNumber(string s, ref int at)
        {
            var start = at;
            if (at < s.Length && s[at] == '-') at++;
            var digits = at;
            while (at < s.Length && char.IsDigit(s[at])) at++;
            if (at == digits || (s[digits] == '0' && at - digits > 1)) throw new FormatException("A number expected at " + start + ".");
            var integer = true;
            if (at < s.Length && s[at] == '.')
            {
                integer = false;
                at++;
                var fraction = at;
                while (at < s.Length && char.IsDigit(s[at])) at++;
                if (at == fraction) throw new FormatException("A fraction's digits expected at " + at + ".");
            }
            if (at < s.Length && (s[at] == 'e' || s[at] == 'E'))
            {
                integer = false;
                at++;
                if (at < s.Length && (s[at] == '+' || s[at] == '-')) at++;
                var exponent = at;
                while (at < s.Length && char.IsDigit(s[at])) at++;
                if (at == exponent) throw new FormatException("An exponent's digits expected at " + at + ".");
            }
            var text = s.Substring(start, at - start);
            if (integer && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole)) return whole;
            return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static void Expect(string s, ref int at, string word)
        {
            if (string.CompareOrdinal(s, at, word, 0, word.Length) != 0) throw new FormatException("'" + word + "' expected at " + at + ".");
            at += word.Length;
        }

        private static void SkipSpace(string s, ref int at)
        {
            while (at < s.Length && (s[at] == ' ' || s[at] == '\t' || s[at] == '\n' || s[at] == '\r')) at++;
        }

        private static void WriteValue(StringBuilder output, object value, int depth)
        {
            if (depth > MaxDepth) throw new ArgumentException("JSON nested too deep.");
            switch (value)
            {
                case null: output.Append("null"); return;
                case string text: WriteString(output, text); return;
                case bool flag: output.Append(flag ? "true" : "false"); return;
                case long n: output.Append(n.ToString(CultureInfo.InvariantCulture)); return;
                case int n: output.Append(n.ToString(CultureInfo.InvariantCulture)); return;
                case short n: output.Append(n.ToString(CultureInfo.InvariantCulture)); return;
                case uint n: output.Append(n.ToString(CultureInfo.InvariantCulture)); return;
                case ulong n: output.Append(n.ToString(CultureInfo.InvariantCulture)); return;
                case double d: WriteDouble(output, d); return;
                case float f: WriteDouble(output, f); return;
                case decimal m: output.Append(m.ToString(CultureInfo.InvariantCulture)); return;
                case IEnumerable<KeyValuePair<string, object>> pairs:
                    output.Append('{');
                    var first = true;
                    foreach (var pair in pairs)
                    {
                        if (!first) output.Append(',');
                        first = false;
                        WriteString(output, pair.Key);
                        output.Append(':');
                        WriteValue(output, pair.Value, depth + 1);
                    }
                    output.Append('}');
                    return;
                case IDictionary dictionary:
                    output.Append('{');
                    var once = true;
                    foreach (DictionaryEntry entry in dictionary)
                    {
                        if (!once) output.Append(',');
                        once = false;
                        WriteString(output, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                        output.Append(':');
                        WriteValue(output, entry.Value, depth + 1);
                    }
                    output.Append('}');
                    return;
                case IEnumerable list:
                    output.Append('[');
                    var start = true;
                    foreach (var item in list)
                    {
                        if (!start) output.Append(',');
                        start = false;
                        WriteValue(output, item, depth + 1);
                    }
                    output.Append(']');
                    return;
                default:
                    throw new ArgumentException("No JSON for a " + value.GetType().Name + ".");
            }
        }

        private static void WriteDouble(StringBuilder output, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) throw new ArgumentException("No JSON for " + d + ".");
            output.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder output, string text)
        {
            output.Append('"');
            foreach (var c in text)
            {
                switch (c)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '/': output.Append("\\/"); break;
                    case '\b': output.Append("\\b"); break;
                    case '\f': output.Append("\\f"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default:
                        if (c < 0x20 || c > 0x7f) output.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else output.Append(c);
                        break;
                }
            }
            output.Append('"');
        }
    }
}
