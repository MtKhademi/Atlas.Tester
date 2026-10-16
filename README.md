# Atlas.Tester

Atlas.Tester is a standalone Windows load-testing toolkit for HTTP APIs. It includes a WPF control center, a reusable .NET load-test engine, scenario builder, saved scenarios, and report generation.

## Projects

- `Atlas.LoadTest.Wpf` — WPF desktop UI for building and running scenarios.
- `Atlas.LoadTest.Engine` — reusable .NET engine and report writers.
- `Atlas.LoadTest.Engine.Tests` — engine tests.

## Requirements

- Windows
- .NET 10 SDK
- A local or dedicated test API

## Run the WPF app

```powershell
dotnet run --project .\Atlas.LoadTest.Wpf\Atlas.LoadTest.Wpf.csproj
```

Scenarios are stored under the application output directory and are ignored by Git.

## Test the engine

```powershell
dotnet test .\Atlas.LoadTest.Engine.Tests\Atlas.LoadTest.Engine.Tests.csproj
```

## Safety

Use Atlas.Tester only against development or dedicated test environments. The UI requires explicit acknowledgement before starting a load test and blocks unsafe remote targets by default.

The local admin helper uses the public HTTP API only and never accesses PostgreSQL directly.

Run the optional local WinForms helper from Windows PowerShell:

```powershell
powershell.exe -Sta -ExecutionPolicy Bypass -File .\Atlas.AdminLoadTest.ps1
```

The tool uses the normal HTTP API. It can log in with a seeded staff account, create test users with varied roles, run concurrent requests against the dashboard summary, and disable the users it created. It does not access PostgreSQL directly and does not delete accounts because the production API intentionally has no hard-delete operation.

Use only against a Development or dedicated test environment. The UI blocks non-local targets unless the operator explicitly enables the remote-target checkbox.
