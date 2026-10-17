namespace Atlas.Tester.Engine;

public static class ClientDataPaths
{
    public static string RootDirectory { get; } = AppContext.BaseDirectory;
    public static string ProjectsDirectory => Path.Combine(RootDirectory, "projects");
    public static string CatalogsDirectory => Path.Combine(RootDirectory, "catalogs");
    public static string ScenariosDirectory => Path.Combine(RootDirectory, "scenarios");
    public static string ReportsDirectory => Path.Combine(RootDirectory, "reports");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(ProjectsDirectory);
        Directory.CreateDirectory(CatalogsDirectory);
        Directory.CreateDirectory(ScenariosDirectory);
        Directory.CreateDirectory(ReportsDirectory);
    }

}
