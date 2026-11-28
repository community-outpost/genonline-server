/*
**    GeneralsOnline Game Services - Backend Services for Command & Conquer Generals Online: Zero Hour
**    Copyright (C) 2025  GeneralsOnline Development Team
**
**    This program is free software: you can redistribute it and/or modify
**    it under the terms of the GNU Affero General Public License as
**    published by the Free Software Foundation, either version 3 of the
**    License, or (at your option) any later version.
**
**    This program is distributed in the hope that it will be useful,
**    but WITHOUT ANY WARRANTY; without even the implied warranty of
**    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
**    GNU Affero General Public License for more details.
**
**    You should have received a copy of the GNU Affero General Public License
**    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using GenOnlineService.Playlists;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GenOnlineService.Playlists
{
	public class PlaylistRow
	{
		public int Id { get; set; }
		public string Name { get; set; } = String.Empty;
		public bool Enabled { get; set; }
		public int MinPlayers { get; set; }
		public int DesiredPlayers { get; set; }
		public int MinSelectedMaps { get; set; }
		public bool AllowTeams { get; set; }

		// -1 means no teams
		public int TeamSize { get; set; } = -1;

		public bool AllowArmySelection { get; set; }
		public int GracePeriodMs { get; set; }

		// Moves on every change to the playlist or its maps, and If-Match compares it
		public int Version { get; set; } = 1;

		public string UpdatedBy { get; set; } = String.Empty;
		public DateTime UpdatedAt { get; set; } = DateTime.UnixEpoch;
	}

	public class PlaylistMapRow
	{
		// Stays with the map when it moves or is renamed
		public long Id { get; set; }
		public int PlaylistId { get; set; }
		public int Position { get; set; }
		public string Name { get; set; } = String.Empty;

		// The file stem, the client builds maps\<stem>\<stem>.map from it
		public string Path { get; set; } = String.Empty;

		public bool Custom { get; set; }
		public int MaxPlayers { get; set; }
		public bool Enabled { get; set; } = true;
	}

	public class PlaylistRowConfiguration : IEntityTypeConfiguration<PlaylistRow>
	{
		public void Configure(EntityTypeBuilder<PlaylistRow> builder)
		{
			builder.ToTable("playlists");
			builder.HasKey(e => e.Id);
			builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
			builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(PlaylistRules.MaximumNameLength);
			builder.Property(e => e.Enabled).HasColumnName("enabled");
			builder.Property(e => e.MinPlayers).HasColumnName("min_players");
			builder.Property(e => e.DesiredPlayers).HasColumnName("desired_players");
			builder.Property(e => e.MinSelectedMaps).HasColumnName("min_selected_maps");
			builder.Property(e => e.AllowTeams).HasColumnName("allow_teams");
			builder.Property(e => e.TeamSize).HasColumnName("team_size");
			builder.Property(e => e.AllowArmySelection).HasColumnName("allow_army_selection");
			builder.Property(e => e.GracePeriodMs).HasColumnName("grace_period_ms");
			builder.Property(e => e.Version).HasColumnName("version").IsConcurrencyToken();
			builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
			builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
		}
	}

	public class PlaylistMapRowConfiguration : IEntityTypeConfiguration<PlaylistMapRow>
	{
		public void Configure(EntityTypeBuilder<PlaylistMapRow> builder)
		{
			builder.ToTable("playlist_maps");
			builder.HasKey(e => e.Id);
			builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
			builder.Property(e => e.PlaylistId).HasColumnName("playlist_id");
			builder.Property(e => e.Position).HasColumnName("position");
			builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(PlaylistRules.MaximumMapNameLength);
			builder.Property(e => e.Path).HasColumnName("path").HasMaxLength(PlaylistRules.MaximumPathLength);
			builder.Property(e => e.Custom).HasColumnName("custom");
			builder.Property(e => e.MaxPlayers).HasColumnName("max_players");
			builder.Property(e => e.Enabled).HasColumnName("enabled");
			builder.HasIndex(e => new { e.PlaylistId, e.Position }).HasDatabaseName("ix_playlist_maps_playlist_position");
			builder.HasOne<PlaylistRow>().WithMany().HasForeignKey(e => e.PlaylistId).OnDelete(DeleteBehavior.Cascade);
		}
	}
}

public abstract partial class AppDbContext
{
	public DbSet<PlaylistRow> Playlists => Set<PlaylistRow>();
	public DbSet<PlaylistMapRow> PlaylistMaps => Set<PlaylistMapRow>();

	partial void ConfigurePlaylists(ModelBuilder modelBuilder)
	{
		modelBuilder.ApplyConfiguration(new PlaylistRowConfiguration());
		modelBuilder.ApplyConfiguration(new PlaylistMapRowConfiguration());
	}
}

namespace GenOnlineService.Catalog
{
	public static partial class CatalogStartup
	{
		static partial void RegisterPlaylists() => CatalogSignals.Register(CatalogResources.Playlists, PlaylistCatalog.Reload);

		static partial void AddPlaylistSeed(List<Func<AppDbContext, Task>> seeds) => seeds.Add(PlaylistStore.EnsureSeedAsync);
	}
}
