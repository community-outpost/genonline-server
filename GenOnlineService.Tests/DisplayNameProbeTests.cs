using Database;
using GenOnlineService.Names;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace GenOnlineService.Tests;

// Runs only when GENONLINE_PROBE_POSTGRES / GENONLINE_PROBE_MARIADB / GENONLINE_PROBE_MYSQL hold a server connection string (no database name);
// each test migrates its own new database on that server and drops it afterwards. MySQL and MariaDB share the MySqlAppDbContext.
public sealed class ProbeTheoryAttribute : TheoryAttribute
{
	public ProbeTheoryAttribute()
	{
		if (Environment.GetEnvironmentVariable(ProbeDatabase.PostgresVariable) == null && Environment.GetEnvironmentVariable(ProbeDatabase.MariaDbVariable) == null
			&& Environment.GetEnvironmentVariable(ProbeDatabase.MySqlVariable) == null)
		{
			Skip = $"Set {ProbeDatabase.PostgresVariable}, {ProbeDatabase.MariaDbVariable} and/or {ProbeDatabase.MySqlVariable} to run the database probes";
		}
	}
}

public sealed class ProbeDatabase : IAsyncDisposable
{
	public const string PostgresVariable = "GENONLINE_PROBE_POSTGRES";
	public const string MariaDbVariable = "GENONLINE_PROBE_MARIADB";
	public const string MySqlVariable = "GENONLINE_PROBE_MYSQL";

	public static IEnumerable<object[]> Providers()
	{
		yield return new object[] { "postgres" };
		yield return new object[] { "mariadb" };
		yield return new object[] { "mysql" };
	}

	private readonly Func<AppDbContext> m_create;
	public string Provider { get; }

	private ProbeDatabase(string provider, Func<AppDbContext> create)
	{
		Provider = provider;
		m_create = create;
	}

	public static async Task<ProbeDatabase?> Create(string provider, bool migrate = true)
	{
		string? server = Environment.GetEnvironmentVariable(provider switch
		{
			"postgres" => PostgresVariable,
			"mariadb" => MariaDbVariable,
			"mysql" => MySqlVariable,
			_ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "postgres, mariadb or mysql"),
		});
		if (server == null)
		{
			return null;
		}

		string name = "gn_probe_" + Guid.NewGuid().ToString("N").Substring(0, 12);
		// queries do not track, as the service configures every context (Program.ConfigureDbContextOptions), so a probe catches
		// a write that changes a loaded entity without tracking it
		Func<AppDbContext> create;
		if (provider == "postgres")
		{
			string connection = $"{server};Database={name}";
			create = () => new PostgresAppDbContext(new DbContextOptionsBuilder<PostgresAppDbContext>().UseNpgsql(connection).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options);
		}
		else
		{
			string connection = $"{server};Database={name}";
			ServerVersion version = ServerVersion.AutoDetect(server);
			create = () => new MySqlAppDbContext(new DbContextOptionsBuilder<MySqlAppDbContext>().UseMySql(connection, version).UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking).Options);
		}

		var probe = new ProbeDatabase(provider, create);
		if (migrate)
		{
			await using AppDbContext db = probe.Open();
			await db.Database.MigrateAsync();
		}
		return probe;
	}

	public AppDbContext Open() => m_create();

	public async ValueTask DisposeAsync()
	{
		await using AppDbContext db = Open();
		await db.Database.EnsureDeletedAsync();
	}
}

public class DisplayNameProbeTests
{
	private static async Task<User> AddUser(AppDbContext db, string name, int tag)
	{
		var user = new User { Username = name, UsernameNormalized = NameNormalizer.Normalize(name), NameTag = tag, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
		db.Users.Add(user);
		await db.SaveChangesAsync();
		return user;
	}

	private static async Task<long> Scalar(AppDbContext db, string sql)
	{
		await db.Database.OpenConnectionAsync();
		await using System.Data.Common.DbCommand command = db.Database.GetDbConnection().CreateCommand();
		command.CommandText = sql;
		return Convert.ToInt64(await command.ExecuteScalarAsync());
	}

	private static Task<long> UniqueKeyIndexColumns(AppDbContext db, string provider)
	{
		return Scalar(db, provider == "postgres"
			? "SELECT COUNT(*) FROM pg_indexes WHERE tablename = 'users' AND indexname = 'ux_users_name_key_name_tag' AND indexdef LIKE 'CREATE UNIQUE INDEX%(name_key, name_tag)'"
			: "SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema = DATABASE() AND table_name = 'users' AND index_name = 'ux_users_name_key_name_tag' AND non_unique = 0");
	}

}
