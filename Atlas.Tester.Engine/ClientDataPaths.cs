namespace Atlas.Tester.Engine;

public static class ClientDataPaths
{
    public static string RootDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AtlasTester");
    public static string ProjectsDirectory => Path.Combine(RootDirectory, "projects");
    public static string CatalogsDirectory => Path.Combine(RootDirectory, "catalogs");
    public static string ScenariosDirectory => Path.Combine(RootDirectory, "scenarios");
    public static string ReportsDirectory => Path.Combine(RootDirectory, "reports");

    private static string LegacyRoamingRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtlasTester");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(ProjectsDirectory);
        Directory.CreateDirectory(CatalogsDirectory);
        Directory.CreateDirectory(ScenariosDirectory);
        Directory.CreateDirectory(ReportsDirectory);
    }

    public static void MigrateLegacyApplicationData()
    {
        MigrateDirectory(Path.Combine(LegacyRoamingRoot, "projects"), ProjectsDirectory);
        MigrateDirectory(Path.Combine(LegacyRoamingRoot, "catalogs"), CatalogsDirectory);
    }

    public static void MigrateDirectory(string legacyDirectory, string destinationDirectory)
    {
        if (!Directory.Exists(legacyDirectory) || Path.GetFullPath(legacyDirectory).Equals(Path.GetFullPath(destinationDirectory), StringComparison.OrdinalIgnoreCase)) return;
        foreach (var sourcePath in Directory.EnumerateFiles(legacyDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(legacyDirectory, sourcePath);
            var destinationPath = Path.Combine(destinationDirectory, relativePath);
            if (File.Exists(destinationPath)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath);
        }
    }

    public static void DeleteLegacyFile(string category, string fileName)
    {
        var legacyPath = Path.Combine(LegacyRoamingRoot, category, fileName);
        if (File.Exists(legacyPath)) File.Delete(legacyPath);
    }
}
