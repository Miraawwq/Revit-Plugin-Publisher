using System.IO.Compression;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace KrakhmalovSheets;

public static class ReportService
{
    public static IReadOnlyList<string> Write(ExportProfile profile, ExportRunResult result)
    {
        var paths = new List<string>();
        string mode = string.IsNullOrWhiteSpace(profile.ReportFormat) ? (profile.CreateReport ? "CSV" : "None") : profile.ReportFormat;
        if (mode.Equals("None", StringComparison.OrdinalIgnoreCase)) return paths;

        string folder = ExportService.ExpandOutputFolder(profile.OutputFolder);
        Directory.CreateDirectory(folder);
        string stem = $"BIMLEADERS_Sheets_{DateTime.Now:yyyyMMdd_HHmmss}";
        if (mode.Equals("CSV", StringComparison.OrdinalIgnoreCase) || mode.Equals("Both", StringComparison.OrdinalIgnoreCase))
        {
            string path = Path.Combine(folder, stem + ".csv");
            WriteCsv(path, result.Entries);
            paths.Add(path);
        }

        if (mode.Equals("XLSX", StringComparison.OrdinalIgnoreCase) || mode.Equals("Both", StringComparison.OrdinalIgnoreCase))
        {
            string path = Path.Combine(folder, stem + ".xlsx");
            WriteXlsx(path, result.Entries);
            paths.Add(path);
        }

        return paths;
    }

    private static void WriteCsv(string path, IReadOnlyList<ExportLogEntry> entries)
    {
        var csv = new StringBuilder("Time,Format,Item,Success,Path,Message\r\n");
        foreach (ExportLogEntry entry in entries)
        {
            csv.AppendLine(string.Join(",", new[]
            {
                Csv(entry.Time.ToString("O")), Csv(entry.Format), Csv(entry.Item), Csv(entry.Success.ToString()), Csv(entry.Path), Csv(entry.Message)
            }));
        }

        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
    }

    private static void WriteXlsx(string path, IReadOnlyList<ExportLogEntry> entries)
    {
        if (File.Exists(path)) File.Delete(path);
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
            </Types>
            """);
        WriteEntry(archive, "_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);
        WriteEntry(archive, "xl/workbook.xml", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets><sheet name="Export report" sheetId="1" r:id="rId1"/></sheets>
            </workbook>
            """);
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
            </Relationships>
            """);

        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = new List<string[]> { new[] { "Time", "Format", "Item", "Success", "Path", "Message" } };
        rows.AddRange(entries.Select(entry => new[] { entry.Time.ToString("O"), entry.Format, entry.Item, entry.Success.ToString(), entry.Path, entry.Message }));
        var sheetData = new XElement(ns + "sheetData",
            rows.Select((values, rowIndex) => new XElement(ns + "row", new XAttribute("r", rowIndex + 1),
                values.Select((value, columnIndex) => new XElement(ns + "c",
                    new XAttribute("r", ColumnName(columnIndex + 1) + (rowIndex + 1)),
                    new XAttribute("t", "inlineStr"),
                    new XElement(ns + "is", new XElement(ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value ?? string.Empty)))))));
        var worksheet = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(ns + "worksheet", sheetData));
        WriteEntry(archive, "xl/worksheets/sheet1.xml", worksheet.ToString(SaveOptions.DisableFormatting));
    }

    private static void WriteEntry(ZipArchive archive, string name, string contents)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(contents.Trim());
    }

    private static string ColumnName(int index)
    {
        var value = new StringBuilder();
        while (index > 0)
        {
            index--;
            value.Insert(0, (char)('A' + index % 26));
            index /= 26;
        }

        return value.ToString();
    }

    private static string Csv(string value) => '"' + value.Replace("\"", "\"\"") + '"';
}
