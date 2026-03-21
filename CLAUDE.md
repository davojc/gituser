# GitUserHandler

A .NET 10 console app (`gituser`) for managing multiple git user profiles.

## Build & Run

```bash
dotnet build
dotnet run --project src/GitUserHandler.Cli -- <command>
```

## Publish

```bash
dotnet publish src/GitUserHandler.Cli -c Release -o out -p:PublishAot=false -p:PublishSingleFile=true -p:DebugType=none --no-self-contained
```

## Architecture

Single project: `src/GitUserHandler.Cli` — all code lives here.

```
src/GitUserHandler.Cli/
├── Program.cs              # Entry point, ServiceFactory
├── AppTheme.cs             # Central theme (all color/style tokens)
├── SetupStatus.cs          # Setup state DTO
├── SpectreHelpRenderer.cs  # Re-renders ConsoleAppFramework help via Spectre
├── ThemeHelper.cs           # Converts theme strings to Spectre Color/Style
├── Commands/
│   ├── SetupCommands.cs    # setup, reset
│   └── UserCommands.cs     # list, add, insert
└── Services/
    ├── IEnvironmentProvider.cs  # OS-level abstraction
    ├── EnvironmentProvider.cs   # Cross-platform file/env implementation
    └── SetupService.cs          # All business logic
```

## Constraints

- **Native AOT**: Must remain AOT-compatible. `PublishAot=true` in csproj.
- **Cross-platform**: Must work on Windows, Mac, and Linux.
- **CLI framework**: ConsoleAppFramework (source-generator based, AOT safe). Do NOT use Spectre.Console.CLI.
- **Console output**: All formatting via Spectre.Console. No raw `Console.WriteLine`. Help output rendered via `SpectreHelpRenderer`.
- **Theming**: All colors/styles defined in `AppTheme`. Never hard-code Spectre style strings in commands or renderers — always reference `AppTheme` properties. `ThemeHelper` bridges to Spectre `Color`/`Style` where needed.
- **No git execution**: The app does not shell out to `git`. It manages config files directly.

## Storage

User profiles stored as `~/.git/.gitconfig-{label}` files. Global config at `~/.git/.gitconfig` (pointed to by `GIT_CONFIG_GLOBAL` env var).

## Packages

| Package | Purpose |
|---|---|
| ConsoleAppFramework 5.7.13 | CLI command routing (source-gen) |
| Spectre.Console 0.54.0 | Rich console formatting |
