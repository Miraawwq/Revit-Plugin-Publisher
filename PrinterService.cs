using Microsoft.Win32;
using Autodesk.Revit.DB;

namespace MiraSHA.Sheets;

internal static class PrinterService
{
    public static IReadOnlyList<string> GetInstalledPrinters()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ReadRegistryPrinterNames(Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Print\Printers", names, false);
        ReadRegistryPrinterNames(Registry.CurrentUser, @"Software\Microsoft\Windows NT\CurrentVersion\Devices", names, true);
        return names.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static PrinterCapabilities GetCapabilities(Document document, string printerName)
    {
        var result = new PrinterCapabilities();
        if (string.IsNullOrWhiteSpace(printerName)) return result;

        try
        {
            PrintManager manager = document.PrintManager;
            manager.SelectNewPrintDriver(printerName);
            result.IsVirtual = manager.IsVirtual != VirtualPrinterType.None;
            result.PaperSizes.AddRange(manager.PaperSizes.Cast<PaperSize>().Select(item => item.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase));
            result.PaperSources.AddRange(manager.PaperSources.Cast<PaperSource>().Select(item => item.Name).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase));
        }
        catch
        {
        }

        result.PaperSizes.Sort(StringComparer.CurrentCultureIgnoreCase);
        result.PaperSources.Sort(StringComparer.CurrentCultureIgnoreCase);
        return result;
    }

    private static void ReadRegistryPrinterNames(RegistryKey root, string path, HashSet<string> names, bool values)
    {
        try
        {
            using RegistryKey? key = root.OpenSubKey(path);
            if (key == null) return;
            foreach (string name in values ? key.GetValueNames() : key.GetSubKeyNames())
            {
                if (!string.IsNullOrWhiteSpace(name) && !name.StartsWith("S-1-5-", StringComparison.OrdinalIgnoreCase)) names.Add(name);
            }
        }
        catch
        {
        }
    }
}

internal sealed class PrinterCapabilities
{
    public bool IsVirtual { get; set; }
    public List<string> PaperSizes { get; } = new();
    public List<string> PaperSources { get; } = new();
}
