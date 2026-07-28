using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace KrakhmalovSheets;

public partial class AdvancedFormatSettingsWindow : Window
{
    private readonly ExportProfile _profile;

    public AdvancedFormatSettingsWindow(ExportProfile profile)
    {
        InitializeComponent();
        _profile = ExportService.CloneProfile(profile);
        DwgMergedViewsCheck.IsChecked = profile.DwgMergedViews;
        DwgBindImagesCheck.IsChecked = false;
        DwgCleanPcpCheck.IsChecked = profile.DwgCleanPcp;
        DgnMergedViewsCheck.IsChecked = profile.DgnMergedViews;
        Select(DwfTypeCombo, profile.DwfFileType);
        DwfCombinedNameBox.Text = profile.CombinedDwfName;
        DwfCropBoxCheck.IsChecked = profile.DwfCropBoxVisible;
        DwfObjectDataCheck.IsChecked = profile.DwfExportObjectData;
        DwfTexturesCheck.IsChecked = profile.DwfExportTextures;
        DwfAreasCheck.IsChecked = profile.DwfExportAreas;
        Select(DwfQualityCombo, profile.DwfImageQuality);
        Select(DwfImageFormatCombo, profile.DwfImageFormat);

        NwcWholeModelCheck.IsChecked = profile.NwcWholeModel;
        Select(NwcCoordinatesCombo, profile.NwcCoordinates);
        NwcFacetingBox.Text = profile.NwcFacetingFactor.ToString("0.##", CultureInfo.CurrentCulture);
        NwcLevelsCheck.IsChecked = profile.NwcDivideIntoLevels;
        NwcPartsCheck.IsChecked = profile.NwcExportParts;
        NwcRoomGeometryCheck.IsChecked = profile.NwcRoomGeometry;
        NwcLinkedCadCheck.IsChecked = profile.NwcConvertLinkedCad;
        NwcLightsCheck.IsChecked = profile.NwcConvertLights;
        NwcPropertiesCheck.IsChecked = profile.NwcElementProperties;
        NwcMaterialsCheck.IsChecked = profile.NwcFindMissingMaterials;
        NwcUrlsCheck.IsChecked = profile.NwcExportUrls;
        NwcRoomAttributeCheck.IsChecked = profile.NwcRoomAsAttribute;
        NwcLinksCheck.IsChecked = profile.NwcExportLinks;
        NwcIdsCheck.IsChecked = profile.NwcExportElementIds;
        Select(NwcParametersCombo, profile.NwcParameters);

        Select(IfcVersionCombo, profile.IfcVersion);
        Select(IfcBoundaryCombo, profile.IfcSpaceBoundaryLevel.ToString(CultureInfo.InvariantCulture));
        IfcMappingBox.Text = profile.IfcFamilyMappingFile;
        IfcBaseQuantitiesCheck.IsChecked = profile.IfcBaseQuantities;
        IfcSplitCheck.IsChecked = profile.IfcWallAndColumnSplitting;
        IfcVisibleCheck.IsChecked = profile.IfcVisibleElementsOnly;
        Ifc2dCheck.IsChecked = profile.IfcExport2DElements;
        IfcLinksCheck.IsChecked = profile.IfcExportLinkedFiles;
        IfcPartsCheck.IsChecked = profile.IfcExportPartsAsBuildingElements;
        IfcRevitPsetsCheck.IsChecked = profile.IfcExportInternalRevitPropertySets;
        IfcCommonPsetsCheck.IsChecked = profile.IfcExportIfcCommonPropertySets;
        IfcActiveGeometryCheck.IsChecked = profile.IfcUseActiveViewGeometry;
        Select(IfcFileTypeCombo, profile.IfcFileType);
        Select(IfcSitePlacementCombo, profile.IfcSitePlacement);
        IfcPhaseBox.Text = profile.IfcPhaseId;
        IfcCategoryMappingBox.Text = profile.IfcCategoryMapping;
        IfcSteelCheck.IsChecked = profile.IfcIncludeSteelElements;
        IfcRoomsCheck.IsChecked = profile.IfcExportRoomsInView;
        IfcSchedulesCheck.IsChecked = profile.IfcExportSchedulesAsPsets;
        IfcSpecificSchedulesCheck.IsChecked = profile.IfcExportSpecificSchedules;
        IfcUserPsetsCheck.IsChecked = profile.IfcExportUserDefinedPsets;
        IfcUserPsetsFileBox.Text = profile.IfcUserDefinedPsetsFile;
        IfcParameterMapCheck.IsChecked = profile.IfcExportParameterMapping;
        IfcParameterMapFileBox.Text = profile.IfcParameterMappingFile;
        Select(IfcTessellationCombo, profile.IfcTessellationLevel);
        IfcSolidCheck.IsChecked = profile.IfcExportSolidModelRep;
        IfcFamilyNameCheck.IsChecked = profile.IfcUseFamilyAndTypeName;
        Ifc2dRoomCheck.IsChecked = profile.IfcUse2dRoomBoundaries;
        IfcSiteElevationCheck.IsChecked = profile.IfcIncludeSiteElevation;
        IfcGuidCheck.IsChecked = profile.IfcStoreGuid;
        IfcBoundingBoxCheck.IsChecked = profile.IfcExportBoundingBox;
        IfcTriangulationCheck.IsChecked = profile.IfcUseOnlyTriangulation;
        IfcTypeNameOnlyCheck.IsChecked = profile.IfcUseTypeNameOnly;
        IfcVisibleNameCheck.IsChecked = profile.IfcUseVisibleName;

        Select(ImageFitCombo, profile.ImageFitDirection);
        Select(ImageZoomTypeCombo, profile.ImageZoomType);
        ImagePixelBox.Text = profile.ImagePixelSize.ToString(CultureInfo.CurrentCulture);
        ImageZoomBox.Text = profile.ImageZoom.ToString(CultureInfo.CurrentCulture);
        ImageWebsiteCheck.IsChecked = profile.ImageCreateWebsite;
        Select(ImageShadowFormatCombo, profile.ImageShadowFormat);
        ImageWebsiteNameBox.Text = profile.ImageWebsiteName;
        XmlParametersCheck.IsChecked = profile.XmlIncludeParameters;
        XmlProjectParametersCheck.IsChecked = profile.XmlIncludeProjectParameters;
        XmlOneFileCheck.IsChecked = profile.XmlOneFilePerItem;
    }

    public ExportProfile Profile => _profile;

    private void SaveSettings(object sender, RoutedEventArgs e)
    {
        _profile.DwgMergedViews = DwgMergedViewsCheck.IsChecked == true;
        _profile.DwgBindImages = false;
        _profile.DwgCleanPcp = DwgCleanPcpCheck.IsChecked == true;
        _profile.DgnMergedViews = DgnMergedViewsCheck.IsChecked == true;
        _profile.DwfFileType = Text(DwfTypeCombo, "DWF");
        _profile.CombinedDwfName = DwfCombinedNameBox.Text.Trim();
        _profile.DwfCropBoxVisible = DwfCropBoxCheck.IsChecked == true;
        _profile.DwfExportObjectData = DwfObjectDataCheck.IsChecked == true;
        _profile.DwfExportTextures = DwfTexturesCheck.IsChecked == true;
        _profile.DwfExportAreas = DwfAreasCheck.IsChecked == true;
        _profile.DwfImageQuality = Text(DwfQualityCombo, "Medium");
        _profile.DwfImageFormat = Text(DwfImageFormatCombo, "Lossless");

        _profile.NwcWholeModel = NwcWholeModelCheck.IsChecked == true;
        _profile.NwcCoordinates = Text(NwcCoordinatesCombo, "Internal");
        _profile.NwcFacetingFactor = Math.Clamp(Number(NwcFacetingBox.Text, 1), 0.01, 10);
        _profile.NwcDivideIntoLevels = NwcLevelsCheck.IsChecked == true;
        _profile.NwcExportParts = NwcPartsCheck.IsChecked == true;
        _profile.NwcRoomGeometry = NwcRoomGeometryCheck.IsChecked == true;
        _profile.NwcConvertLinkedCad = NwcLinkedCadCheck.IsChecked == true;
        _profile.NwcConvertLights = NwcLightsCheck.IsChecked == true;
        _profile.NwcElementProperties = NwcPropertiesCheck.IsChecked == true;
        _profile.NwcFindMissingMaterials = NwcMaterialsCheck.IsChecked == true;
        _profile.NwcExportUrls = NwcUrlsCheck.IsChecked == true;
        _profile.NwcRoomAsAttribute = NwcRoomAttributeCheck.IsChecked == true;
        _profile.NwcExportLinks = NwcLinksCheck.IsChecked == true;
        _profile.NwcExportElementIds = NwcIdsCheck.IsChecked == true;
        _profile.NwcParameters = Text(NwcParametersCombo, "All");

        _profile.IfcVersion = Text(IfcVersionCombo, "IFC2x3");
        _profile.IfcSpaceBoundaryLevel = (int)Number(Text(IfcBoundaryCombo, "1"), 1);
        _profile.IfcFamilyMappingFile = IfcMappingBox.Text.Trim();
        _profile.IfcBaseQuantities = IfcBaseQuantitiesCheck.IsChecked == true;
        _profile.IfcWallAndColumnSplitting = IfcSplitCheck.IsChecked == true;
        _profile.IfcVisibleElementsOnly = IfcVisibleCheck.IsChecked == true;
        _profile.IfcExport2DElements = Ifc2dCheck.IsChecked == true;
        _profile.IfcExportLinkedFiles = IfcLinksCheck.IsChecked == true;
        _profile.IfcExportPartsAsBuildingElements = IfcPartsCheck.IsChecked == true;
        _profile.IfcExportInternalRevitPropertySets = IfcRevitPsetsCheck.IsChecked == true;
        _profile.IfcExportIfcCommonPropertySets = IfcCommonPsetsCheck.IsChecked == true;
        _profile.IfcUseActiveViewGeometry = IfcActiveGeometryCheck.IsChecked == true;
        _profile.IfcFileType = Text(IfcFileTypeCombo, "Ifc");
        _profile.IfcSitePlacement = Text(IfcSitePlacementCombo, "Current Shared Coordinates");
        _profile.IfcPhaseId = IfcPhaseBox.Text.Trim();
        _profile.IfcCategoryMapping = IfcCategoryMappingBox.Text.Trim();
        _profile.IfcIncludeSteelElements = IfcSteelCheck.IsChecked == true;
        _profile.IfcExportRoomsInView = IfcRoomsCheck.IsChecked == true;
        _profile.IfcExportSchedulesAsPsets = IfcSchedulesCheck.IsChecked == true;
        _profile.IfcExportSpecificSchedules = IfcSpecificSchedulesCheck.IsChecked == true;
        _profile.IfcExportUserDefinedPsets = IfcUserPsetsCheck.IsChecked == true;
        _profile.IfcUserDefinedPsetsFile = IfcUserPsetsFileBox.Text.Trim();
        _profile.IfcExportParameterMapping = IfcParameterMapCheck.IsChecked == true;
        _profile.IfcParameterMappingFile = IfcParameterMapFileBox.Text.Trim();
        _profile.IfcTessellationLevel = Text(IfcTessellationCombo, "Extra Low");
        _profile.IfcExportSolidModelRep = IfcSolidCheck.IsChecked == true;
        _profile.IfcUseFamilyAndTypeName = IfcFamilyNameCheck.IsChecked == true;
        _profile.IfcUse2dRoomBoundaries = Ifc2dRoomCheck.IsChecked == true;
        _profile.IfcIncludeSiteElevation = IfcSiteElevationCheck.IsChecked == true;
        _profile.IfcStoreGuid = IfcGuidCheck.IsChecked == true;
        _profile.IfcExportBoundingBox = IfcBoundingBoxCheck.IsChecked == true;
        _profile.IfcUseOnlyTriangulation = IfcTriangulationCheck.IsChecked == true;
        _profile.IfcUseTypeNameOnly = IfcTypeNameOnlyCheck.IsChecked == true;
        _profile.IfcUseVisibleName = IfcVisibleNameCheck.IsChecked == true;

        _profile.ImageFitDirection = Text(ImageFitCombo, "Horizontal");
        _profile.ImageZoomType = Text(ImageZoomTypeCombo, "FitToPage");
        _profile.ImagePixelSize = Math.Clamp((int)Number(ImagePixelBox.Text, 3000), 100, 15000);
        _profile.ImageZoom = Math.Clamp((int)Number(ImageZoomBox.Text, 100), 1, 1000);
        _profile.ImageCreateWebsite = ImageWebsiteCheck.IsChecked == true;
        _profile.ImageShadowFormat = Text(ImageShadowFormatCombo, "PNG");
        _profile.ImageWebsiteName = ImageWebsiteNameBox.Text.Trim();
        _profile.XmlIncludeParameters = XmlParametersCheck.IsChecked == true;
        _profile.XmlIncludeProjectParameters = XmlProjectParametersCheck.IsChecked == true;
        _profile.XmlOneFilePerItem = XmlOneFileCheck.IsChecked == true;
        DialogResult = true;
    }

    private static void Select(ComboBox combo, string value)
    {
        combo.SelectedItem = combo.Items.Cast<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase)) ?? combo.Items[0];
    }

    private static string Text(ComboBox combo, string fallback) => (combo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? fallback;

    private static double Number(string value, double fallback) => double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out double result) || double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ? result : fallback;
}
