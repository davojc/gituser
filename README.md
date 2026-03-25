# gituser

A cross-platform CLI tool for managing multiple git user profiles. Switch between different git identities (name, email, signing keys) per repository without manually editing config files.

## Why?

If you use git for both work and personal projects, you've probably committed with the wrong email at least once. `gituser` solves this by letting you define named profiles and apply them to repositories automatically using git's built-in `includeIf` mechanism.

## Install

Download the latest release for your platform from [GitHub Releases](https://github.com/davojc/gituser/releases), or build from source:

```bash
dotnet publish src/GitUserHandler.Cli -c Release -o out -p:PublishAot=false -p:PublishSingleFile=true -p:DebugType=none --no-self-contained
```

Place the resulting `gituser` binary somewhere on your `PATH`.

## Quick start

```bash
# 1. Run initial setup (migrates your git config to ~/.git/)
gituser setup

# 2. Add a second profile
gituser add

# 3. Apply a profile to the current repository
gituser apply

# 4. Verify the active config
gituser current
```

## Commands

### Global Config

| Command | Description |
|---------|-------------|
| `setup` | Initial setup — migrates `~/.git*` files to `~/.git/` and sets the `GIT_CONFIG_GLOBAL` environment variable |
| `reset` | Reverses setup — moves config files back and removes the environment variable |
| `add` | Create a new git user profile (interactive) |
| `edit` | Modify an existing profile |
| `list` | Display all configured profiles |

### Repository

| Command | Description |
|---------|-------------|
| `apply` | Apply a user profile to the current repository |
| `clear` | Remove the applied profile from the current repository |
| `current` | Show the effective git user config for the current directory |
| `localsign` | Enable or disable commit signing for the current repository |

### CLI

| Command | Description |
|---------|-------------|
| `update` | Check for updates and install the latest version |
| `--version` | Show the current version |

## How it works

`gituser` stores named profiles as individual git config files under `~/.git/`:

```
~/.git/
├── .gitconfig              # Global config (pointed to by GIT_CONFIG_GLOBAL)
├── .gitconfig-work         # "work" profile
└── .gitconfig-personal     # "personal" profile
```

Each profile is a standard git config file:

```ini
[user]
    name = Jane Doe
    email = jane@work.com
    signingkey = ~/.ssh/id_ed25519_work.pub

[commit]
    gpgsign = true

[gpg]
    format = ssh
```

When you run `gituser apply` in a repository, it adds an [`includeIf`](https://git-scm.com/docs/git-config#_conditional_includes) directive to your global config:

```ini
[includeIf "gitdir:~/projects/work/myrepo/"]
    path = ~/.git/.gitconfig-work
```

Git natively evaluates this condition, so the correct identity is used automatically — no wrappers or shims required.

## Signing support

Profiles can optionally configure commit signing with GPG or SSH keys. The `localsign` command lets you toggle signing on or off for individual repositories.

## Requirements

- [.NET 10](https://dotnet.microsoft.com/) (to build from source)
- Git (for `current` command only — all other commands manage config files directly)
- Windows, macOS, or Linux

## License

See [LICENSE](LICENSE) for details.
