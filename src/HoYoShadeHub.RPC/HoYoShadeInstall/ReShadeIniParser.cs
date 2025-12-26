using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HoYoShadeHub.RPC.HoYoShadeInstall;

/// <summary>
/// INI file parser based on ReShade official installer
/// Reference: https://github.com/crosire/reshade (IniFile.cs)
/// </summary>
public class IniFile
{
    private readonly SortedDictionary<string, SortedDictionary<string, string[]>> sections = new();

    public IniFile(Stream stream)
    {
        if (stream == null)
        {
            return;
        }

        string section = string.Empty;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (!reader.EndOfStream)
        {
            string line = reader.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(line) ||
                line.StartsWith(";", StringComparison.Ordinal) ||
                line.StartsWith("#", StringComparison.Ordinal) ||
                line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("[", StringComparison.Ordinal))
            {
                int sectionEnd = line.IndexOf(']');
                if (sectionEnd >= 0)
                {
                    section = line.Substring(1, sectionEnd - 1);
                    continue;
                }
            }

            var pair = line.Split(new[] { '=' }, 2, StringSplitOptions.None);
            if (pair.Length == 2)
            {
                string key = pair[0].Trim();
                string value = pair[1].Trim();
                SetValue(section, key, value.Split(new[] { ',' }, StringSplitOptions.None));
            }
            else
            {
                SetValue(section, line);
            }
        }
    }

    public bool HasValue(string section)
    {
        return sections.ContainsKey(section);
    }

    public bool HasValue(string section, string key)
    {
        return sections.TryGetValue(section, out var sectionData) && sectionData.ContainsKey(key);
    }

    public bool GetValue(string section, string key, out string[] value)
    {
        if (!sections.TryGetValue(section, out var sectionData))
        {
            value = default;
            return false;
        }

        return sectionData.TryGetValue(key, out value);
    }

    public void SetValue(string section, string key, params string[] value)
    {
        if (!sections.TryGetValue(section, out var sectionData))
        {
            if (value == null)
            {
                return;
            }

            sections[section] = sectionData = new SortedDictionary<string, string[]>();
        }

        sectionData[key] = value ?? new string[] { };
    }

    public string GetString(string section, string key, string defaultValue = null)
    {
        return GetValue(section, key, out var value) ? string.Join(",", value) : defaultValue;
    }

    public string[] GetSections()
    {
        return sections.Select(x => x.Key).ToArray();
    }
}
