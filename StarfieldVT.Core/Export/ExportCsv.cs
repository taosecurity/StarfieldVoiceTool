using System.Text;

namespace StarfieldVT.Core.Export;

/// <summary>Writes list.csv: UTF-8 with a BOM (so Excel reads it as UTF-8), RFC 4180 quoting, CRLF line ends.</summary>
public static class ExportCsv
{
    public const string FileName = "list.csv";

    /// <summary>
    /// A cell that starts with one of these is a formula to Excel and other spreadsheets. Names and
    /// dialogue come from mods, so such a cell gets a leading apostrophe, which spreadsheets read as
    /// "this is text".
    /// </summary>
    private static readonly char[] FormulaStarts = ['=', '+', '-', '@', '\t', '\r'];

    /// <summary>
    /// The cell with an apostrophe in front when it would start a formula (= + - @, tab or CR), or
    /// when it already starts with an apostrophe (so <see cref="Unguard"/> always removes exactly the
    /// one that was added).
    /// </summary>
    public static string Guard(string? cell)
    {
        cell ??= "";
        return cell.Length > 0 && (cell[0] == '\'' || Array.IndexOf(FormulaStarts, cell[0]) >= 0) ? "'" + cell : cell;
    }

    /// <summary>Undo <see cref="Guard"/>: drop the apostrophe that was put in front of a formula start or an apostrophe.</summary>
    public static string Unguard(string cell) =>
        cell.Length > 1 && cell[0] == '\'' && (cell[1] == '\'' || Array.IndexOf(FormulaStarts, cell[1]) >= 0) ? cell[1..] : cell;

    /// <summary>Guard against formulas, then quote the cell when it holds a comma, a double quote, a carriage return or a line feed.</summary>
    public static string Escape(string? cell)
    {
        cell = Guard(cell);
        return cell.IndexOfAny([',', '"', '\r', '\n']) < 0 ? cell : "\"" + cell.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>The CSV text (without the BOM) for a header and rows.</summary>
    public static string Format(IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", header.Select(Escape))).Append("\r\n");
        foreach (var row in rows)
        {
            sb.Append(string.Join(",", row.Select(Escape))).Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>Write the CSV to <paramref name="path"/>, replacing any earlier list.</summary>
    public static void Write(string path, IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows) =>
        File.WriteAllText(path, Format(header, rows), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

    /// <summary>
    /// Read a CSV written by <see cref="Write"/> back into rows of plain cells (quotes and formula
    /// guards removed). The first row is the header.
    /// </summary>
    public static List<List<string>> Parse(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;
        var i = text.Length > 0 && text[0] == '﻿' ? 1 : 0;
        for (; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c != '"') cell.Append(c);
                else if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else quoted = false;
            }
            else if (c == '"') quoted = true;
            else if (c == ',')
            {
                row.Add(Unguard(cell.ToString()));
                cell.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(Unguard(cell.ToString()));
                cell.Clear();
                rows.Add(row);
                row = [];
            }
            else cell.Append(c);
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(Unguard(cell.ToString()));
            rows.Add(row);
        }

        return rows;
    }
}
