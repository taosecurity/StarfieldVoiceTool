using System.Text;

namespace StarfieldVT.Core.Export;

/// <summary>Writes list.csv: UTF-8 with a BOM (so Excel reads it as UTF-8), RFC 4180 quoting, CRLF line ends.</summary>
public static class ExportCsv
{
    public const string FileName = "list.csv";

    /// <summary>Quote a cell when it holds a comma, a double quote, a carriage return or a line feed.</summary>
    public static string Escape(string? cell)
    {
        cell ??= "";
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
}
