# GenOnline Server

The game services behind Command & Conquer: Generals Online (Zero Hour): sign-in, lobbies, quick match, chat, friends, stats and ladders. They replace what GameSpy did for the original game.

[![Windows](https://github.com/community-outpost/genonline-server/actions/workflows/Windows.yml/badge.svg)](https://github.com/community-outpost/genonline-server/actions/workflows/Windows.yml)
[![Linux](https://github.com/community-outpost/genonline-server/actions/workflows/Linux.yml/badge.svg)](https://github.com/community-outpost/genonline-server/actions/workflows/Linux.yml)
[![macOS](https://github.com/community-outpost/genonline-server/actions/workflows/MacOS.yml/badge.svg)](https://github.com/community-outpost/genonline-server/actions/workflows/MacOS.yml)

## Requirements

- .NET 10 SDK
- PostgreSQL 15 or newer (MariaDB 12.1.2 or newer works for a single copy)

## Getting started

1. Build with `dotnet build GenServices.sln`.
2. Fill in `appsettings.json`, including the `Database` section (`db_provider` is `postgres` or `mysql`).
3. Create the schema with `GenOnlineService migrate`. The service never migrates on its own.
4. Run `GenOnlineService`.

To upgrade, stop the service, run `GenOnlineService migrate --yes` and start the new version.

## Contributing

Changes land on `main` through pull requests. A model change needs an EF Core migration for both `PostgresAppDbContext` and `MySqlAppDbContext`.

## License

AGPL-3.0, see [LICENSE](LICENSE).
