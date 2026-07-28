using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace MiraSHA.Sheets;

public static class NamingService
{
    private static readonly Regex AngleToken = new("<(?<token>[^<>]+)>", RegexOptions.Compiled);
    private static readonly Regex PercentToken = new("%(?<token>[^%]+)%", RegexOptions.Compiled);

    public static string Build(Document document, ExportItem item, ExportProfile profile)
    {
        string pattern = string.IsNullOrWhiteSpace(item.CustomFileName)
            ? string.IsNullOrWhiteSpace(profile.NamingPattern) ? "<Number> - <Name>" : profile.NamingPattern
            : item.CustomFileName;

        Element? element = document.GetElement(item.Id);
        string expanded = AngleToken.Replace(pattern, match => Resolve(document, element, item, match.Groups["token"].Value));
        expanded = PercentToken.Replace(expanded, match => Resolve(document, element, item, match.Groups["token"].Value));
        return Sanitize(expanded);
    }

    public static IReadOnlyList<string> CollectParameterTokens(Document document, IEnumerable<ExportItem> items)
    {
        var names = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (ExportItem item in items.Take(40))
        {
            if (document.GetElement(item.Id) is not Element element) continue;
            foreach (Parameter parameter in element.Parameters)
            {
                string? name = parameter.Definition?.Name;
                if (!string.IsNullOrWhiteSpace(name)) names.Add($"<Parameter:{name}>");
            }
        }

        foreach (Parameter parameter in document.ProjectInformation.Parameters)
        {
            string? name = parameter.Definition?.Name;
            if (!string.IsNullOrWhiteSpace(name)) names.Add($"<Project:{name}>");
        }

        return names.OrderBy(name => name).ToList();
    }

    public static string Sanitize(string value)
    {
        string result = value.Trim();
        foreach (char character in Path.GetInvalidFileNameChars()) result = result.Replace(character, '_');
        return string.IsNullOrWhiteSpace(result) ? "Export" : result.TrimEnd('.', ' ');
    }

    private static string Resolve(Document document, Element? element, ExportItem item, string rawToken)
    {
        string token = rawToken.Trim();
        DateTime now = DateTime.Now;
        string? dateToken = token switch
        {
            "Y" => now.ToString("yyyy", CultureInfo.InvariantCulture),
            "yy" => now.ToString("yy", CultureInfo.InvariantCulture),
            "m" or "mm" => now.ToString("MM", CultureInfo.InvariantCulture),
            "d" or "dd" => now.ToString("dd", CultureInfo.InvariantCulture),
            "H" or "HH" => now.ToString("HH", CultureInfo.InvariantCulture),
            "M" or "MM" => now.ToString("mm", CultureInfo.InvariantCulture),
            "S" or "SS" => now.ToString("ss", CultureInfo.InvariantCulture),
            _ => null
        };
        if (dateToken != null) return dateToken;
        string? builtIn = token.ToLowerInvariant() switch
        {
            "number" or "sheetnumber" => item.Number,
            "name" or "sheetname" => item.Name,
            "revision" => item.Revision,
            "type" => item.Kind,
            "sheetsize" or "size" => item.DisplayPaperSize,
            "username" => document.Application.Username,
            "projectname" => document.ProjectInformation.Name,
            "projectnumber" => document.ProjectInformation.Number,
            _ => null
        };
        if (builtIn != null) return builtIn;

        if (token.StartsWith("Parameter:", StringComparison.OrdinalIgnoreCase))
            return ParameterValue(element, token[10..]);
        if (token.StartsWith("Project:", StringComparison.OrdinalIgnoreCase))
            return ParameterValue(document.ProjectInformation, token[8..]);

        string elementValue = ParameterValue(element, token);
        return string.IsNullOrEmpty(elementValue) ? ParameterValue(document.ProjectInformation, token) : elementValue;
    }

    private static string ParameterValue(Element? element, string name)
    {
        if (element == null || string.IsNullOrWhiteSpace(name)) return string.Empty;
        Parameter? parameter = element.LookupParameter(name.Trim());
        if (parameter == null) return string.Empty;
        try
        {
            return parameter.AsValueString()
                   ?? parameter.StorageType switch
                   {
                       StorageType.String => parameter.AsString() ?? string.Empty,
                       StorageType.Integer => parameter.AsInteger().ToString(CultureInfo.InvariantCulture),
                       StorageType.Double => parameter.AsDouble().ToString(CultureInfo.InvariantCulture),
                       StorageType.ElementId => parameter.AsElementId().Value.ToString(CultureInfo.InvariantCulture),
                       _ => string.Empty
                   };
        }
        catch
        {
            return string.Empty;
        }
    }
}
