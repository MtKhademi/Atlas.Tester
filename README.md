# Atlas.Tester

Atlas.Tester is a standalone Windows testering toolkit for HTTP APIs. It includes a WPF control center, a reusable .NET tester engine, scenario builder, saved scenarios, and report generation.

## Projects

- `Atlas.Tester.Wpf` — WPF desktop UI for building and running scenarios.
- `Atlas.Tester.Engine` — reusable .NET engine and report writers.
- `Atlas.Tester.Engine.Tests` — engine tests.

## Requirements

- Windows
- .NET 10 SDK
- A local or dedicated test API

## Run the WPF app

```powershell
dotnet run --project .\Atlas.Tester.Wpf\Atlas.Tester.Wpf.csproj
```

Scenarios are stored under the application output directory and are ignored by Git.

## Test the engine

```powershell
dotnet test .\Atlas.Tester.Engine.Tests\Atlas.Tester.Engine.Tests.csproj
```

## Safety

Use Atlas.Tester only against development or dedicated test environments. The UI requires explicit acknowledgement before starting a performance test and blocks unsafe remote targets by default.

The local admin helper uses the public HTTP API only and never accesses PostgreSQL directly.

Run the optional local WinForms helper from Windows PowerShell:

```powershell
powershell.exe -Sta -ExecutionPolicy Bypass -File .\Atlas.AdminTester.ps1
```

The tool uses the normal HTTP API. It can log in with a seeded staff account, create test users with varied roles, run concurrent requests against the dashboard summary, and disable the users it created. It does not access PostgreSQL directly and does not delete accounts because the production API intentionally has no hard-delete operation.

Use only against a Development or dedicated test environment. The UI blocks non-local targets unless the operator explicitly enables the remote-target checkbox.
