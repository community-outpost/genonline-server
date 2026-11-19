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

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GenOnlineService.Identity
{
	// Keyed hash (device HMAC key, DeviceHasher) of what a ban holds: for an erased account under an active ban, its exact email and each provider id.
	// A live account keeps no hash: its address stays in users.email and is held by address uniqueness.
	public class IdentifierHash
	{
		public const int KindMaxLength = 48;

		public Int64 ID { get; set; }
		public Int64 UserID { get; set; }

		public string Kind { get; set; } = String.Empty;

		public byte[] ValueHmac { get; set; } = Array.Empty<byte>();

		public Int64? SanctionID { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UnixEpoch;
	}

	public class IdentifierHashConfiguration : IEntityTypeConfiguration<IdentifierHash>
	{
		public void Configure(EntityTypeBuilder<IdentifierHash> builder)
		{
			builder.ToTable("identifier_hashes");
			builder.HasKey(e => e.ID);
			builder.HasIndex(e => new { e.Kind, e.ValueHmac }).HasDatabaseName("ix_identifier_hashes_kind_value");
			builder.HasIndex(e => e.UserID).HasDatabaseName("ix_identifier_hashes_user");

			builder.Property(e => e.ID).HasColumnName("id").ValueGeneratedOnAdd();
			builder.Property(e => e.UserID).HasColumnName("user_id");
			builder.Property(e => e.Kind).HasColumnName("kind").HasMaxLength(IdentifierHash.KindMaxLength);
			builder.Property(e => e.ValueHmac).HasColumnName("value_hmac").HasMaxLength(32).IsFixedLength();
			builder.Property(e => e.SanctionID).HasColumnName("sanction_id");
			builder.Property(e => e.CreatedAt).HasColumnName("created_at");
		}
	}

	public static class HashedIdentifierKinds
	{
		public const string Email = "email";
		public const string ProviderPrefix = "provider:";

		public static string Provider(string name) => ProviderPrefix + name.ToLowerInvariant();
	}

	public static class IdentifierHashes
	{
		public static byte[] Hash(string kind, string value) => DeviceHasher.HashIdentifier(kind + "\n" + value);

		// The exact address as login uses it (EmailNormalized); no mailbox grouping, so a +tag or a dot is another address. Null for no address.
		public static byte[]? EmailHash(string? email) =>
			String.IsNullOrWhiteSpace(email) ? null : Hash(HashedIdentifierKinds.Email, NameNormalizer.Normalize(email));

		// `exceptUserID` lets an account keep its own address while it changes another one.
		public static async Task<bool> EmailHeldByBan(AppDbContext db, string? email, Int64 exceptUserID, DateTime utcNow, CancellationToken cancellationToken = default)
		{
			byte[]? hash = EmailHash(email);
			if (hash == null)
			{
				return false;
			}

			IQueryable<Int64> banned = Database.Moderation.ActiveSanctions(db, SanctionTypes.Ban, utcNow).Select(s => s.UserID);
			return await db.IdentifierHashes.AnyAsync(h => h.Kind == HashedIdentifierKinds.Email && h.ValueHmac == hash && h.SanctionID != null
				&& h.UserID != exceptUserID && banned.Contains(h.UserID), cancellationToken);
		}

		public static Task<bool> ProviderHeld(AppDbContext db, string provider, string subject, CancellationToken cancellationToken = default)
		{
			string kind = HashedIdentifierKinds.Provider(provider);
			byte[] hash = Hash(kind, subject);
			return db.IdentifierHashes.AnyAsync(h => h.Kind == kind && h.ValueHmac == hash && h.SanctionID != null, cancellationToken);
		}

		public static Task<int> PurgeEndedHolds(AppDbContext db, DateTime utcNow, CancellationToken cancellationToken = default)
		{
			IQueryable<Int64> banned = Database.Moderation.ActiveSanctions(db, SanctionTypes.Ban, utcNow).Select(s => s.UserID);
			return db.IdentifierHashes.Where(h => h.SanctionID != null && !banned.Contains(h.UserID)).ExecuteDeleteAsync(cancellationToken);
		}
	}
}
