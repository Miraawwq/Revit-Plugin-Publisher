using System.IO;
using System.Text.Json;

namespace KrakhmalovSheets;

public sealed class LocalSettings
{
    public string ActiveProfile { get; set; } = "Default";

    public List<ExportProfile> Profiles { get; set; } = new() { new ExportProfile() };

    public List<SelectionSetRecord> SelectionSets { get; set; } = new();

    public List<ScheduledExportJob> ScheduledJobs { get; set; } = new();

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KrakhmalovSheets",
        "settings.json");

    public static LocalSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                LocalSettings? settings = JsonSerializer.Deserialize<LocalSettings>(File.ReadAllText(SettingsPath));
                if (settings != null)
                {
                    settings.Profiles ??= new List<ExportProfile>();
                    settings.SelectionSets ??= new List<SelectionSetRecord>();
                    settings.ScheduledJobs ??= new List<ScheduledExportJob>();
                    foreach (ScheduledExportJob job in settings.ScheduledJobs)
                    {
                        job.Weekdays ??= new List<DayOfWeek>();
                        job.Profile ??= new ExportProfile();
                    }
                    if (settings.Profiles.Count == 0)
                    {
                        settings.Profiles.Add(new ExportProfile());
                    }

                    return settings;
                }
            }
        }
        catch
        {
        }

        return new LocalSettings();
    }

    public void Save()
    {
        string? directory = Path.GetDirectoryName(SettingsPath);
        if (directory == null)
        {
            return;
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class ExportProfile
{
    public string Name { get; set; } = "Default";
    public string OutputFolder { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public string NamingPattern { get; set; } = "<Number> - <Name>";
    public bool SplitByFormat { get; set; }
    public bool CreateReport { get; set; } = true;
    public string ReportFormat { get; set; } = "CSV";
    public bool Pdf { get; set; } = true;
    public bool Dwg { get; set; }
    public bool Dgn { get; set; }
    public bool Dwf { get; set; }
    public bool Nwc { get; set; }
    public bool Ifc { get; set; }
    public bool Image { get; set; }
    public bool Xml { get; set; }
    public bool CombinePdf { get; set; }
    public string CombinedPdfName { get; set; } = "Sheets.pdf";
    public string PdfEngine { get; set; } = "Revit native PDF";
    public string PrinterName { get; set; } = string.Empty;
    public string PrinterPaperSize { get; set; } = string.Empty;
    public string PrinterPaperSource { get; set; } = string.Empty;
    public int PrinterCopies { get; set; } = 1;
    public bool PrinterCollate { get; set; }
    public bool PrinterReverseOrder { get; set; }
    public bool PrinterPrintToFile { get; set; } = true;
    public string DwgSetup { get; set; } = "Default";
    public bool DwgMergedViews { get; set; }
    public bool DwgBindImages { get; set; }
    public bool DwgCleanPcp { get; set; }
    public string DgnSetup { get; set; } = "Default";
    public bool DgnMergedViews { get; set; }
    public string RasterQuality { get; set; } = "High";
    public string ColorMode { get; set; } = "Color";
    public bool AlwaysRaster { get; set; }
    public bool ViewLinksInBlue { get; set; } = true;
    public bool HideReferencePlanes { get; set; } = true;
    public bool HideUnreferencedTags { get; set; } = true;
    public bool HideScopeBoxes { get; set; } = true;
    public bool HideCropBoundaries { get; set; } = true;
    public bool ReplaceHalftone { get; set; }
    public bool MaskCoincidentLines { get; set; }
    public bool CombineDwf { get; set; }
    public string CombinedDwfName { get; set; } = "Sheets.dwf";
    public string DwfFileType { get; set; } = "DWF";
    public bool DwfCropBoxVisible { get; set; }
    public string DwfImageQuality { get; set; } = "Medium";
    public string DwfImageFormat { get; set; } = "Lossless";
    public bool DwfExportObjectData { get; set; } = true;
    public bool DwfExportTextures { get; set; } = true;
    public bool DwfExportAreas { get; set; } = true;
    public bool NwcWholeModel { get; set; }
    public bool NwcConvertLinkedCad { get; set; } = true;
    public bool NwcConvertLights { get; set; } = true;
    public double NwcFacetingFactor { get; set; } = 1;
    public bool NwcDivideIntoLevels { get; set; } = true;
    public bool NwcElementProperties { get; set; } = true;
    public bool NwcFindMissingMaterials { get; set; } = true;
    public bool NwcRoomGeometry { get; set; }
    public string NwcCoordinates { get; set; } = "Internal";
    public bool NwcExportUrls { get; set; } = true;
    public bool NwcRoomAsAttribute { get; set; } = true;
    public bool NwcExportLinks { get; set; } = true;
    public bool NwcExportElementIds { get; set; } = true;
    public bool NwcExportParts { get; set; }
    public string NwcParameters { get; set; } = "All";
    public string IfcVersion { get; set; } = "IFC2x3";
    public int IfcSpaceBoundaryLevel { get; set; } = 1;
    public string IfcFamilyMappingFile { get; set; } = string.Empty;
    public bool IfcBaseQuantities { get; set; } = true;
    public bool IfcWallAndColumnSplitting { get; set; }
    public bool IfcVisibleElementsOnly { get; set; }
    public bool IfcExport2DElements { get; set; }
    public bool IfcExportLinkedFiles { get; set; }
    public bool IfcExportPartsAsBuildingElements { get; set; }
    public bool IfcExportInternalRevitPropertySets { get; set; } = true;
    public bool IfcExportIfcCommonPropertySets { get; set; } = true;
    public bool IfcUseActiveViewGeometry { get; set; }
    public string IfcFileType { get; set; } = "Ifc";
    public string IfcPhaseId { get; set; } = string.Empty;
    public string IfcSitePlacement { get; set; } = "Current Shared Coordinates";
    public bool IfcIncludeSteelElements { get; set; }
    public bool IfcExportRoomsInView { get; set; }
    public bool IfcExportSchedulesAsPsets { get; set; }
    public bool IfcExportSpecificSchedules { get; set; }
    public bool IfcExportUserDefinedPsets { get; set; }
    public string IfcUserDefinedPsetsFile { get; set; } = string.Empty;
    public bool IfcExportParameterMapping { get; set; }
    public string IfcParameterMappingFile { get; set; } = string.Empty;
    public string IfcTessellationLevel { get; set; } = "Extra Low";
    public bool IfcExportSolidModelRep { get; set; }
    public bool IfcUseFamilyAndTypeName { get; set; }
    public bool IfcUse2dRoomBoundaries { get; set; }
    public bool IfcIncludeSiteElevation { get; set; }
    public bool IfcStoreGuid { get; set; }
    public bool IfcExportBoundingBox { get; set; }
    public bool IfcUseOnlyTriangulation { get; set; }
    public bool IfcUseTypeNameOnly { get; set; }
    public bool IfcUseVisibleName { get; set; }
    public string IfcCategoryMapping { get; set; } = string.Empty;
    public string ImageFormat { get; set; } = "PNG";
    public string ImageResolution { get; set; } = "DPI_300";
    public string ImageFitDirection { get; set; } = "Horizontal";
    public string ImageZoomType { get; set; } = "FitToPage";
    public int ImagePixelSize { get; set; } = 3000;
    public int ImageZoom { get; set; } = 100;
    public bool ImageCreateWebsite { get; set; }
    public string ImageShadowFormat { get; set; } = "PNG";
    public string ImageWebsiteName { get; set; } = "Images";
    public bool XmlIncludeParameters { get; set; } = true;
    public bool XmlIncludeProjectParameters { get; set; } = true;
    public bool XmlOneFilePerItem { get; set; }
    public string PaperSize { get; set; } = "Default";
    public string PaperOrientation { get; set; } = "Auto";
    public string PaperPlacement { get; set; } = "Center";
    public double OriginOffsetXmm { get; set; }
    public double OriginOffsetYmm { get; set; }
    public string ZoomMode { get; set; } = "FitToPage";
    public int ZoomPercentage { get; set; } = 100;
    public bool DisableTemporaryHideIsolate { get; set; } = true;
    public bool DisableWorksharingDisplay { get; set; } = true;
    public bool DisableRevealHiddenElements { get; set; } = true;
    public bool DisableRevealConstraints { get; set; } = true;
    public bool KeepPaperSizeAndOrientation { get; set; }
}

public sealed class SelectionSetRecord
{
    public string Name { get; set; } = string.Empty;
    public List<string> ItemUniqueIds { get; set; } = new();
}

public sealed class ScheduledExportJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DocumentKey { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public DateTime NextRun { get; set; }
    public string Repeat { get; set; } = "Once";
    public List<DayOfWeek> Weekdays { get; set; } = new();
    public bool Enabled { get; set; } = true;
    public DateTime? LastRun { get; set; }
    public string LastError { get; set; } = string.Empty;
    public int RunCount { get; set; }
    public List<string> ItemUniqueIds { get; set; } = new();
    public ExportProfile Profile { get; set; } = new();
}
