using System.Globalization;
using System.Text;

namespace Mash.GUI.MNXArchiver.Settings;

/// <summary>Portable UTF-8 INI settings. Section and key names are case-insensitive.</summary>
public sealed class INI_Config {
    private readonly Dictionary<string, Section> _sections = new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase);
    public IEnumerable<string> SectionNames => _sections.Keys;
    public bool RemoveSection(string name) {
        ValidateName(name, true);
        return _sections.Remove(name);
        }
    public Section this[string name] {
        get {
            ValidateName(name, true);
            if (!_sections.TryGetValue(name, out Section? section)) {
                section = new Section();
                _sections.Add(name, section);
                }
            return section;
            }
        }

    public static INI_Config Load(string path, int maximumBytes = 1024 * 1024) {
        INI_Config config = new INI_Config();
        if (!System.IO.File.Exists(path)) {
            return config;
            }
        if (new FileInfo(path).Length > maximumBytes) {
            throw new IOException("INI file exceeds the permitted size: " + path);
            }
        string? section = "General";
        foreach (string raw in System.IO.File.ReadLines(path, Encoding.UTF8)) {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#') {
                continue;
                }
            if (line[0] == '[' && line.EndsWith(']')) {
                section = line.Substring(1, line.Length - 2).Trim();
                // KConfig may contain nested [group][subgroup] sections. Ignore
                // unsupported headers and their keys, rather than merging them.
                if (section.Length == 0 || section.IndexOfAny(new char[] { '[', ']' }) >= 0) {
                    section = null;
                    }
                continue;
                }
            int equals = line.IndexOf('=');
            if (equals > 0 && section is not null) {
                config[section].Set(line.Substring(0, equals).Trim(), line.Substring(equals + 1).Trim());
                }
            }
        return config;
        }

    public INI_Config Copy() {
        INI_Config copy = new INI_Config();
        foreach (KeyValuePair<string, Section> section in _sections) {
            foreach (KeyValuePair<string, string> value in section.Value.Values) {
                copy[section.Key].Set(value.Key, value.Value);
                }
            }
        return copy;
        }

    public string ToText(string? comment = null) {
        StringBuilder text = new StringBuilder();
        if (comment is not null) {
            foreach (string line in comment.Replace("\r", "").Split('\n')) {
                text.Append("; ").AppendLine(line);
                }
            }
        foreach (KeyValuePair<string, Section> section in _sections) {
            text.AppendLine().Append('[').Append(section.Key).AppendLine("]");
            foreach (KeyValuePair<string, string> value in section.Value.Values) {
                text.Append(value.Key).Append('=').AppendLine(value.Value);
                }
            }
        return text.ToString();
        }

    /// <summary>Write beside the destination, then rename; a failed save keeps the old file.</summary>
    public void Save(string path, string? comment = null) {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string pending = path + "." + Guid.NewGuid().ToString("N") + ".pending";
        try {
            FileStreamOptions options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows()) {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                }
            using (FileStream stream = new FileStream(pending, options)) {
                byte[] bytes = Encoding.UTF8.GetBytes(ToText(comment));
                stream.Write(bytes);
                stream.Flush(true);
                }
            System.IO.File.Move(pending, path, true);
            }
        finally {
            System.IO.File.Delete(pending);
            }
        }

    private static void ValidateName(string name, bool section) {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(new char[] { '\r', '\n', '\0', '[', ']' }) >= 0 || (!section && (name.Contains('=') || name.StartsWith(';') || name.StartsWith('#')))) {
            throw new ArgumentException("Invalid INI " + (section ? "section" : "key") + " name.", nameof(name));
            }
        }

    public sealed class Section {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public IEnumerable<KeyValuePair<string, string>> Values => _values;
        public bool Contains(string key) { return _values.ContainsKey(key); }
        public bool Remove(string key) { return _values.Remove(key); }
        public string GetString(string key, string fallback = "") {
            return _values.TryGetValue(key, out string? value) ? value : fallback;
            }
        public bool GetBool(string key, bool fallback = false) {
            string value = GetString(key).ToLowerInvariant();
            if (value == "true" || value == "yes" || value == "on" || value == "1") {
                return true;
                }
            if (value == "false" || value == "no" || value == "off" || value == "0") {
                return false;
                }
            return fallback;
            }
        public T Get<T>(string key, T fallback) where T : IParsable<T> {
            if (typeof(T) == typeof(bool)) {
                return (T)(object)GetBool(key, (bool)(object)fallback);
                }
            if (T.TryParse(GetString(key), CultureInfo.InvariantCulture, out T? value)) {
                // Non-finite numbers are not useful window sizes, limits, or delays.
                if (value is double number && !double.IsFinite(number) || value is float single && !float.IsFinite(single)) {
                    return fallback;
                    }
                return value;
                }
            return fallback;
            }
        public byte GetByte(string key, byte fallback = 0) { return Get(key, fallback); }
        public short GetShort(string key, short fallback = 0) { return Get(key, fallback); }
        public int GetInt(string key, int fallback = 0) { return Get(key, fallback); }
        public long GetLong(string key, long fallback = 0) { return Get(key, fallback); }
        public Int128 GetLongLong(string key, Int128 fallback = default) { return Get(key, fallback); }
        public float GetFloat(string key, float fallback = 0) { return Get(key, fallback); }
        public double GetDouble(string key, double fallback = 0) { return Get(key, fallback); }
        public decimal GetDecimal(string key, decimal fallback = 0) { return Get(key, fallback); }

        public void Set(string key, object value) {
            ValidateName(key, false);
            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            if (text.IndexOfAny(new char[] { '\n', '\r', '\0' }) >= 0) {
                throw new ArgumentException("INI values must fit on one line.", nameof(value));
                }
            _values[key] = text;
            }

        /// <summary>Quoting keeps semicolons and quotes inside list items intact.</summary>
        public void SetList<T>(string key, IEnumerable<T> values) {
            List<string> fields = new List<string>();
            foreach (T value in values) {
                string field = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                fields.Add("\"" + field.Replace("\"", "\"\"") + "\"");
                }
            Set(key, string.Join(';', fields));
            }
        public IReadOnlyList<T> GetList<T>(string key, IReadOnlyList<T>? fallback = null) where T : IParsable<T> {
            List<T> result = new List<T>();
            if (!Contains(key) || !TryFields(GetString(key), out List<string> fields)) {
                return fallback ?? Array.Empty<T>();
                }
            foreach (string field in fields) {
                Section item = new Section();
                item.Set("value", field);
                if (typeof(T) == typeof(bool)) {
                    string boolean = field.ToLowerInvariant();
                    if (boolean != "true" && boolean != "false" && boolean != "yes" && boolean != "no" && boolean != "on" && boolean != "off" && boolean != "1" && boolean != "0") {
                        return fallback ?? Array.Empty<T>();
                        }
                    result.Add((T)(object)item.GetBool("value"));
                    }
                else if (T.TryParse(field, CultureInfo.InvariantCulture, out T? value)) {
                    result.Add(value);
                    }
                else {
                    return fallback ?? Array.Empty<T>();
                    }
                }
            return result;
            }

        private static bool TryFields(string text, out List<string> fields) {
            fields = new List<string>();
            if (text.Length == 0) {
                return true;
                }
            StringBuilder field = new StringBuilder();
            bool quoted = false;
            bool wasQuoted = false;
            for (int index = 0; index < text.Length; index++) {
                char character = text[index];
                if (character == '"') {
                    if (quoted && index + 1 < text.Length && text[index + 1] == '"') {
                        field.Append('"');
                        index++;
                        }
                    else {
                        if (!quoted && !wasQuoted && string.IsNullOrWhiteSpace(field.ToString())) {
                            field.Clear();
                            }
                        wasQuoted = true;
                        quoted = !quoted;
                        }
                    }
                else if (character == ';' && !quoted) {
                    fields.Add(wasQuoted ? field.ToString() : field.ToString().Trim());
                    field.Clear();
                    wasQuoted = false;
                    }
                else if (wasQuoted && !quoted && char.IsWhiteSpace(character)) {
                    continue;
                    }
                else {
                    field.Append(character);
                    }
                }
            fields.Add(wasQuoted ? field.ToString() : field.ToString().Trim());
            return !quoted;
            }
        }
    }
