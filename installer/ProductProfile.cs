namespace JKXRDioramaMRInstaller
{
    // This is the only product-specific file that needs to be adapted when the
    // Galatea installer shell is reused for another Diorama MR mod.
    internal static class ProductProfile
    {
        internal const string BrandName = "Galatea Diorama Mods";
        internal const string ProductName = "JKXR Diorama MR";
        internal const string InstallerTitle = ProductName + " Installer";
        internal const string PlatformBadge = "META QUEST  |  DIORAMA MR";

        internal const string GameName = "Jedi Academy";
        internal const string GameLongName = "Star Wars Jedi Knight: Jedi Academy";
        internal const string GameRegistrySearchTerm = "Jedi Academy";
        internal const string SteamAppId = "6020";
        internal const string CommandLineValue = "ja";

        internal const string AndroidPackage = "com.galateainteractive.jkxrdioramamr";
        internal const string DeviceRoot = "/sdcard/JKXR-Diorama-MR";
        internal const string GameDataDirectory = DeviceRoot + "/JK3/base";

        internal const string EmbeddedApkResource = "Installer.Product.apk";
        internal const string ExtractedApkName = "JKXR-Diorama-MR.apk";
        internal const string TempFolderName = "GalateaDioramaMods\\JKXRDioramaMR\\v1";
        internal const string LocalBundleFolder = "JediAcademy";

        internal static readonly string[] RequiredFiles =
            { "assets0.pk3", "assets1.pk3", "assets2.pk3", "assets3.pk3" };

        internal static readonly string[] CommonInstallNames =
        {
            "Jedi Academy",
            "Star Wars Jedi Knight - Jedi Academy",
            "STAR WARS Jedi Knight - Jedi Academy"
        };
    }
}
