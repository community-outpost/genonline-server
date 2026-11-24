using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

// The 43 migrations up to RoleDefinitions were collapsed into one Baseline migration per provider. A database that
// ran all of them already has the Baseline schema, so their history rows are replaced by the Baseline; later rows stay.
public static class MigrationBaseline
{
	public const string LastCollapsedPostgres = "20261121195429_RoleDefinitions";
	public const string LastCollapsedMySql = "20261121195410_RoleDefinitions";

	public enum Outcome
	{
		NotNeeded,
		Adopted,
		Behind
	}

	// Behind: the database stopped part way through the old chain, which only the last release with that chain can finish
	public static Outcome Decide(IReadOnlyCollection<string> applied, string baselineID, string lastCollapsedID)
	{
		if (applied.Count == 0 || applied.Contains(baselineID))
		{
			return Outcome.NotNeeded;
		}
		return applied.Contains(lastCollapsedID) ? Outcome.Adopted : Outcome.Behind;
	}

	public static async Task<Outcome> AdoptAsync(AppDbContext db)
	{
		var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
		string baselineID = db.Database.GetMigrations().First();
		string lastCollapsedID = db.Database.IsNpgsql() ? LastCollapsedPostgres : LastCollapsedMySql;

		Outcome outcome = Decide(applied, baselineID, lastCollapsedID);
		if (outcome != Outcome.Adopted)
		{
			return outcome;
		}

		IHistoryRepository history = db.GetService<IHistoryRepository>();
		await using var transaction = await db.Database.BeginTransactionAsync();
		// migration ids sort by their timestamp, so the collapsed ones are those up to the last of them
		foreach (string id in applied.Where(id => String.CompareOrdinal(id, lastCollapsedID) <= 0))
		{
			await db.Database.ExecuteSqlRawAsync(history.GetDeleteScript(id));
		}
		await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(baselineID, ProductInfo.GetVersion())));
		await transaction.CommitAsync();
		return outcome;
	}
}
