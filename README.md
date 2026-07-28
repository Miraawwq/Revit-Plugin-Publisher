# BIMLEADERS Sheets

Independent offline Revit 2025/2026 add-in for exporting any number of project sheets.

Build a specific Revit version with `dotnet build -c Release -p:RevitVersion=2025` or `-p:RevitVersion=2026`.

Features:

- sheet search and multi-selection;
- no account, server, telemetry, or network access;
- sheets, printable views, real Revit View/Sheet Sets, and legacy local selection sets;
- local export profiles with JSON import and export;
- PDF, DWG, DGN, DWF, NWC, IFC, image, and XML export;
- configurable file naming with built-in, date, user, sheet/view parameter, and project parameter tokens;
- individual paper size, orientation, ordering, progress, and retry of failed exports;
- CSV and XLSX reports and per-format output folders;
- local one-time, daily, weekly, and monthly scheduled publishing while Revit is running;
- advanced DWF/DWFX, NWC, IFC, image, XML, and temporary-view-mode settings;
- local settings remain in `%LOCALAPPDATA%\KrakhmalovSheets` for compatibility with existing profiles and schedules.
