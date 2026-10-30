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

## Docker

```
docker run --rm -v ./appsettings.json:/app/appsettings.json:ro ghcr.io/community-outpost/genonline-server:<version> migrate --yes
docker run -p 8080:8080 -v ./appsettings.json:/app/appsettings.json:ro -v ./data:/app/data ghcr.io/community-outpost/genonline-server:<version>
```

The image listens on port 8080 over plain HTTP; put TLS in front of it. Start `./data` from a copy of `GenOnlineService/data`, and make it writable for the container's non-root user.

## Contributing

Changes land on `main` through pull requests. A model change needs an EF Core migration for both `PostgresAppDbContext` and `MySqlAppDbContext`.

Releases are tags named after the month, `vYYYY.MM.P`. A release candidate (`vYYYY.MM.P-rc.N`) goes to the test environment first, and the release tag on the same commit goes live. Each tag publishes an image and a linux-x64 archive.

## License

AGPL-3.0, see [LICENSE](LICENSE).
