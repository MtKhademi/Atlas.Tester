namespace Atlas.Tester.Engine;

public sealed record ScenarioStep(string Name, string Method, string Path, bool RequiresAuthentication, ScenarioRequestDefinition? Request = null, IReadOnlyList<ScenarioCaptureDefinition>? Captures = null);
public sealed record TesterScenario(string Key, string DisplayName, IReadOnlyList<ScenarioStep> Steps);

public static class ScenarioCatalog
{
    public static IReadOnlyList<TesterScenario> All { get; } =
    [
        new("user-portfolio", "User portfolio journey", [
            new("Login", "POST", "/iam/api/auth/login", false),
            new("Market assets", "GET", "/oracle/api/market/assets", true),
            new("Market prices", "GET", "/oracle/api/market/prices?symbols=gold18,usd", true),
            new("Create user asset", "POST", "/oracle/api/me/assets", true),
            new("Record transaction", "POST", "/oracle/api/me/assets/{id}/transactions", true),
            new("Read user assets", "GET", "/oracle/api/me/assets", true),
            new("Read transaction history", "GET", "/oracle/api/me/assets/{id}/transactions", true)
        ]),
        new("bank-account", "Bank account journey", [
            new("Login", "POST", "/iam/api/auth/login", false),
            new("Create bank account", "POST", "/oracle/api/me/bank-accounts", true),
            new("Read bank accounts", "GET", "/oracle/api/me/bank-accounts", true),
            new("Update bank account", "PUT", "/oracle/api/me/bank-accounts/{id}", true),
            new("Delete bank account", "DELETE", "/oracle/api/me/bank-accounts/{id}", true)
        ]),
        new("admin-operations", "Admin operations journey", [
            new("Admin login", "POST", "/iam/api/auth/login", false),
            new("Read users", "GET", "/admin/api/users", true),
            new("Read roles", "GET", "/admin/api/roles", true),
            new("Dashboard", "GET", "/admin/api/dashboard/summary", true),
            new("System status", "GET", "/admin/api/system/status", true),
            new("Audit logs", "GET", "/admin/api/audit-logs", true)
        ]),
        new("market-prices", "Market price journey", [
            new("Admin login", "POST", "/iam/api/auth/login", false),
            new("Batch price write", "POST", "/admin/api/oracle/operations/prices", true),
            new("Read prices", "GET", "/oracle/api/market/prices?symbols=gold18,usd", true),
            new("Manual refresh", "POST", "/admin/api/oracle/operations/refresh", true),
            new("Read prices again", "GET", "/oracle/api/market/prices?symbols=gold18,usd", true)
        ])
    ];
}
