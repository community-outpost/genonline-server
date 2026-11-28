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

using GenOnlineService;
using GenOnlineService.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenIddict.EntityFrameworkCore.Models;

// one per user and session type; stored so every service copy accepts the realtime upgrade of a registered client
public class GameSessionRow
{
	public Int64 UserID { get; set; }
	public int SessionType { get; set; }
	public string AuthorizationID { get; set; } = String.Empty;
	public string ClientID { get; set; } = String.Empty;
	public string Game { get; set; } = String.Empty;
	public DateTime RegisteredAt { get; set; }
	public Int64? ExeCrc { get; set; }
	public Int64? IniCrc { get; set; }
	public int AnticheatID { get; set; }
	// The machine the sign-in of this registration sent, null when it sent no identifier or the machine is removed
	public Int64? MachineID { get; set; }
}

public class GameSessionRowConfiguration : IEntityTypeConfiguration<GameSessionRow>
{
	public void Configure(EntityTypeBuilder<GameSessionRow> builder)
	{
		builder.ToTable("game_sessions");

		builder.HasKey(e => new { e.UserID, e.SessionType });
		builder.HasIndex(e => e.AuthorizationID).HasDatabaseName("ix_game_sessions_authorization_id");

		builder.Property(e => e.UserID).HasColumnName("user_id").ValueGeneratedNever();
		builder.Property(e => e.SessionType).HasColumnName("session_type").ValueGeneratedNever();
		builder.Property(e => e.AuthorizationID).HasColumnName("authorization_id").IsRequired();
		builder.Property(e => e.ClientID).HasColumnName("client_id").HasMaxLength(Client.ClientIDMaxLength).IsRequired();
		builder.Property(e => e.Game).HasColumnName("game").HasMaxLength(16).IsRequired();
		builder.Property(e => e.RegisteredAt).HasColumnName("registered_at");
		builder.Property(e => e.ExeCrc).HasColumnName("exe_crc");
		builder.Property(e => e.IniCrc).HasColumnName("ini_crc");
		builder.Property(e => e.AnticheatID).HasColumnName("anticheat_id");
		builder.Property(e => e.MachineID).HasColumnName("machine_id");
		builder.HasOne<Machine>().WithMany().HasForeignKey(e => e.MachineID).OnDelete(DeleteBehavior.SetNull);

		// a sign-in that is pruned takes its registration with it
		builder.HasOne<OpenIddictEntityFrameworkCoreAuthorization>().WithMany().HasForeignKey(e => e.AuthorizationID).OnDelete(DeleteBehavior.Cascade);
	}
}

namespace Database
{
	public static class GameSessionStore
	{
		public static async Task<GameSessionRegistration?> Load(AppDbContext db, Int64 userID, EUserSessionType sessionType, CancellationToken cancellationToken = default)
		{
			GameSessionRow? row = await db.GameSessions.AsNoTracking().SingleOrDefaultAsync(r => r.UserID == userID && r.SessionType == (int)sessionType, cancellationToken);
			return row == null ? null : new GameSessionRegistration(row.AuthorizationID, row.ClientID, row.Game, DateTime.SpecifyKind(row.RegisteredAt, DateTimeKind.Utc),
				row.ExeCrc == null ? null : (uint)row.ExeCrc.Value, row.IniCrc == null ? null : (uint)row.IniCrc.Value, row.AnticheatID);
		}

		// Replaces the user's registration of this session type. The row is tracked: the service's contexts do not track queries,
		// and an untracked row would keep the previous sign-in, which the realtime upgrade then refuses with session.not_registered
		public static async Task Save(AppDbContext db, Int64 userID, EUserSessionType sessionType, GameSessionRegistration registration)
		{
			GameSessionRow? row = await db.GameSessions.AsTracking().SingleOrDefaultAsync(r => r.UserID == userID && r.SessionType == (int)sessionType);
			if (row == null)
			{
				row = new GameSessionRow { UserID = userID, SessionType = (int)sessionType };
				db.GameSessions.Add(row);
			}
			row.AuthorizationID = registration.AuthorizationID;
			row.ClientID = registration.ClientID;
			row.Game = registration.Game;
			row.RegisteredAt = registration.RegisteredAt;
			row.ExeCrc = registration.ExeCrc;
			row.IniCrc = registration.IniCrc;
			row.AnticheatID = registration.AnticheatID;

			try
			{
				await db.SaveChangesAsync();
			}
			catch (DbUpdateException ex) when (DbHelpers.IsUniqueViolation(ex))
			{
				// two registrations at once: the later one wins
				db.ChangeTracker.Clear();
				await db.GameSessions.Where(r => r.UserID == userID && r.SessionType == (int)sessionType).ExecuteUpdateAsync(s => s
					.SetProperty(r => r.AuthorizationID, registration.AuthorizationID)
					.SetProperty(r => r.ClientID, registration.ClientID)
					.SetProperty(r => r.Game, registration.Game)
					.SetProperty(r => r.RegisteredAt, registration.RegisteredAt)
					.SetProperty(r => r.ExeCrc, (Int64?)registration.ExeCrc)
					.SetProperty(r => r.IniCrc, (Int64?)registration.IniCrc)
					.SetProperty(r => r.AnticheatID, registration.AnticheatID));
			}
		}

		// Keyed by the authorization, so a registration of a newer sign-in is never given the machine of an older one
		public static Task SetMachine(AppDbContext db, Int64 userID, EUserSessionType sessionType, string authorizationID, Int64? machineID) =>
			db.GameSessions.Where(r => r.UserID == userID && r.SessionType == (int)sessionType && r.AuthorizationID == authorizationID)
				.ExecuteUpdateAsync(s => s.SetProperty(r => r.MachineID, machineID));

		public static Task<Int64?> MachineOf(AppDbContext db, Int64 userID, EUserSessionType sessionType, string authorizationID) =>
			db.GameSessions.AsNoTracking()
				.Where(r => r.UserID == userID && r.SessionType == (int)sessionType && r.AuthorizationID == authorizationID)
				.Select(r => r.MachineID)
				.FirstOrDefaultAsync();

		public static Task Delete(AppDbContext db, Int64 userID, EUserSessionType sessionType) =>
			db.GameSessions.Where(r => r.UserID == userID && r.SessionType == (int)sessionType).ExecuteDeleteAsync();

		public static Task DeleteSignIn(AppDbContext db, string authorizationID) =>
			db.GameSessions.Where(r => r.AuthorizationID == authorizationID).ExecuteDeleteAsync();

		public static Task DeleteUser(AppDbContext db, Int64 userID, IReadOnlyCollection<string>? authorizationIDs)
		{
			IQueryable<GameSessionRow> rows = db.GameSessions.Where(r => r.UserID == userID);
			if (authorizationIDs != null)
			{
				List<string> ids = authorizationIDs.ToList();
				rows = rows.Where(r => ids.Contains(r.AuthorizationID));
			}
			return rows.ExecuteDeleteAsync();
		}
	}
}
