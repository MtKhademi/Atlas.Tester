using Atlas.Tester.Engine;

namespace Atlas.Tester.Engine.Tests;

public sealed class SandboxRuntimePlanTests
{
    [Fact]
    public void Creates_internal_network_and_non_published_database()
    {
        var project = new ProjectDefinition("atlas", "Atlas", "", "http://127.0.0.1:5100", "/openapi/v1.json", DateTimeOffset.UtcNow, Path.GetTempPath());
        var plan = SandboxRuntimePlan.Create(SandboxPlanner.Create(project), 5100, "atlas-api:test", "/app", "IAM__ConnectionString");

        var commands = plan.DockerCommands();

        Assert.Contains("--internal", commands[0]);
        Assert.Contains("postgres:16-alpine", commands[2]);
        Assert.DoesNotContain("-p", commands[2]);
        Assert.Contains("127.0.0.1:5100:8080", commands[3]);
        Assert.Contains(commands[3], value => value.StartsWith("IAM__ConnectionString=Host=postgres;", StringComparison.Ordinal));
        Assert.All(commands, command => Assert.Contains("atlas-tester.managed=true", command));
    }
}
