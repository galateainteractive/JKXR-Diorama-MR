using System;
using System.IO;

namespace GalateaDioramaInstaller
{
    // This is the only source file that is specific to JKXR Diorama MR.
    // Duplicate it to create another Galatea installer profile.
    internal static class ProductProfile
    {
        internal const InstallerWorkflow Workflow = InstallerWorkflow.EmbeddedApkWithPayload;
        internal const string ProfileId = "jkxr-diorama-mr";
        internal const string BrandName = "Galatea Diorama Mods";
        internal const string ProductName = "JKXR Diorama MR";
        internal const string InstallerTitle = ProductName + " Installer";
        internal const string GameName = "Jedi Academy";
        internal const string GameLongName = "Star Wars Jedi Knight: Jedi Academy";
        internal const string PostInstallInstructions = "";
        internal const string CommandLineValue = "ja";
        internal const string CommandLineApkAssetPath = "assets/commandline.txt";
        internal const string CommandLineDeviceRelativePath = "commandline.txt";
        internal const bool GrantAllFilesAccess = true;

        internal const string AndroidPackage = "com.galateainteractive.jkxrdioramamr";
        internal const string DeviceRoot = "/sdcard/JKXR-Diorama-MR";
        internal const string DevicePayloadRoot = DeviceRoot + "/JK3/base";

        internal const string EmbeddedApkResource = "Installer.Product.apk";
        internal const string ExtractedApkName = "JKXR-Diorama-MR.apk";
        internal const string TempFolderName = "GalateaDioramaMods\\JKXRDioramaMR\\v1";
        internal const string LocalBundleFolder = "JediAcademy";

        internal const string SourceKitResource = "";
        internal const string SourceKitArchiveName = "";
        internal const string SourceKitVersion = "";
        internal const string SourceKitBuildScript = "";
        internal const string SourceKitExportScript = "";
        internal const string SourceKitWorkFolder = "";
        internal const string GamePackageFilter = "All files (*.*)|*.*";
        internal const string AudioSourceLabel = "Required audio files";
        internal const string AudioSourceFilter = "All files (*.*)|*.*";
        internal const string AudioDownloadUrl = "";

        internal static readonly string[] SteamAppIds = { "6020" };
        internal static readonly string[] RegistrySearchTerms = { "Jedi Academy" };

        internal static readonly string[] InstallRootCandidates =
            { "", "base", Path.Combine("GameData", "base") };

        internal static readonly string[] CommonSearchRoots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            @"C:\GOG Games", @"D:\GOG Games", @"C:\Games", @"D:\Games"
        };

        internal static readonly string[] CommonInstallParentPaths =
            { "", Path.Combine("GOG Galaxy", "Games"), "LucasArts" };

        internal static readonly PayloadRule[] Payload =
        {
            new PayloadRule("assets0.pk3", "assets0.pk3", PayloadEntryType.File,
                PayloadValidation.ZipArchive, 1024 * 1024),
            new PayloadRule("assets1.pk3", "assets1.pk3", PayloadEntryType.File,
                PayloadValidation.ZipArchive, 1024 * 1024),
            new PayloadRule("assets2.pk3", "assets2.pk3", PayloadEntryType.File,
                PayloadValidation.ZipArchive, 1024 * 1024),
            new PayloadRule("assets3.pk3", "assets3.pk3", PayloadEntryType.File,
                PayloadValidation.ZipArchive, 1024 * 1024)
        };

        internal static readonly string[] CommonInstallNames =
        {
            "Jedi Academy",
            "Star Wars Jedi Knight - Jedi Academy",
            "STAR WARS Jedi Knight - Jedi Academy"
        };
    }
}
