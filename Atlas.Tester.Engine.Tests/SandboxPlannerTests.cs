using Atlas.Tester.Engine;

namespace Atlas.Tester.Engine.Tests;

public sealed class SandboxPlannerTests
{
    [Fact]
    public void Creates_unique_private_network_plan_for_existing_backend_path()
    {
        var project = new ProjectDefinition("atlas", "Atlas", "", "http://127.0.0.1:5100", "/openapi/v1.json", DateTimeOffset.UtcNow, Path.GetTempPath());

        var first = SandboxPlanner.Create(project);
        var second = SandboxPlanner.Create(project);

        Assert.NotEqual(first.RunId, second.RunId);
        Assert.NotEqual(first.NetworkName, second.NetworkName);
        Assert.StartsWith("atlas-tester-", first.NetworkName);
        Assert.Contains(first.Dependencies, dependency => dependency.Name == "PostgreSQL");
        Assert.Equal("true", first.EnvironmentOverrides["ATLAS_TESTER_SANDBOX"]);
    }

    [Fact]
    public void Rejects_missing_backend_path()
    {
        var project = new ProjectDefinition("atlas", "Atlas", "", "http://127.0.0.1:5100", "/openapi/v1.json", DateTimeOffset.UtcNow);

        var error = Assert.Throws<InvalidOperationException>(() => SandboxPlanner.Create(project));

        Assert.Contains("backend project", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
