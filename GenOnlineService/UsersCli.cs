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

using GenOnlineService.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace GenOnlineService
{
	public static class UsersCli
	{
		private const string Usage =
			"Usage:\n"
			+ "  GenOnlineService users password-reset <user_id> [--password-stdin] [--reason <text>]\n"
			+ "  GenOnlineService users two-factor-reset <user_id> [--reason <text>]\n"
			+ "  GenOnlineService users name-reset <user_id> [--reason <text>]\n"
			+ "  GenOnlineService users delete <user_id> --reason <text> --yes\n"
			+ "  GenOnlineService users discord-bot-link <user_id> --discord <discord_user_id> | --clear\n"
			+ "password-reset prints a new random password once, or sets the one read from standard input with --password-stdin.\n"
			+ "Both end the account's lockout, revoke every sign-in of the game with its refresh tokens, and sign it out of the website within a minute.\n"
			+ "name-reset replaces an inappropriate player name with a placeholder and ends every sign-in; the player chooses a new name when signing in again.\n"
			+ "delete erases the account on its owner's request and cannot be undone: the personal data goes, the account id stays with a\n"
			+ "neutral name so matches, ratings and bans keep it, and the name leaves the player's matches. While a ban is active its device\n"
			+ "ids and sign-in provider ids stay (as hashes) until the ban ends. --yes confirms; the reason (the request) is required.\n"
			+ "discord-bot-link sets (or with --clear removes) the Discord account the bot treats as this user for its staff commands; it is never a sign-in.";

		private sealed class CliException : Exception
		{
			public CliException(string message) : base(message) { }
		}

		private sealed class ConfiguredDbContextFactory : IDbContextFactory<AppDbContext>
		{
			public AppDbContext CreateDbContext() => Program.OpenConfiguredDbContext();
		}

		private sealed record Arguments(string Command, Int64 UserID, bool PasswordFromStdin, string? Reason, bool Confirmed, string? Discord = null, bool Clear = false);

		private static Arguments Parse(string[] args)
		{
			if (args.Length < 3 || !Int64.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out Int64 userID))
			{
				throw new CliException(Usage);
			}

			string command = args[1].ToLowerInvariant();
			if (command != "password-reset" && command != "two-factor-reset" && command != "name-reset" && command != "delete" && command != "discord-bot-link")
			{
				throw new CliException(Usage);
			}

			bool fromStdin = false;
			bool confirmed = false;
			bool clear = false;
			string? reason = null;
			string? discord = null;
			for (int i = 3; i < args.Length; i++)
			{
				if (args[i].Equals("--password-stdin", StringComparison.OrdinalIgnoreCase) && command == "password-reset")
				{
					fromStdin = true;
				}
				else if (args[i].Equals("--yes", StringComparison.OrdinalIgnoreCase) && command == "delete")
				{
					confirmed = true;
				}
				else if (args[i].Equals("--discord", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && command == "discord-bot-link")
				{
					discord = args[++i];
				}
				else if (args[i].Equals("--clear", StringComparison.OrdinalIgnoreCase) && command == "discord-bot-link")
				{
					clear = true;
				}
				else if (args[i].Equals("--reason", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && command != "discord-bot-link")
				{
					reason = args[++i];
				}
				else
				{
					// no --password: a password on the command line ends up in shell history and process lists
					throw new CliException($"Unknown option or missing value: {args[i]}{Environment.NewLine}{Usage}");
				}
			}

			if (command == "delete" && (String.IsNullOrWhiteSpace(reason) || !confirmed))
			{
				throw new CliException($"delete cannot be undone: give the request as --reason <text> and confirm with --yes.{Environment.NewLine}{Usage}");
			}

			if (command == "discord-bot-link" && (discord == null) == !clear)
			{
				throw new CliException($"discord-bot-link takes either --discord <discord_user_id> or --clear.{Environment.NewLine}{Usage}");
			}

			return new Arguments(command, userID, fromStdin, reason, confirmed, discord, clear);
		}

		private static string CliActor()
		{
			string actor = "cli:" + Environment.UserName;
			return actor.Length > 64 ? actor.Substring(0, 64) : actor;
		}

		// The CLI has no rank: it is how an owner, whom nobody outranks, links their own Discord account
		private static async Task<int> LinkDiscordBot(AppDbContext db, Arguments parsed)
		{
			if (!await Database.Users.Exists(db, parsed.UserID))
			{
				throw new CliException($"No user with id {parsed.UserID}.");
			}

			var actor = new Database.ModerationActor { Kind = AuditActorKinds.Cli, Name = CliActor(), Source = ModerationSources.Cli };
			if (parsed.Clear)
			{
				Console.WriteLine(await Database.DiscordBotLinks.Clear(db, parsed.UserID, actor)
					? $"User {parsed.UserID} has no Discord account linked for the bot any more."
					: $"User {parsed.UserID} had no Discord account linked for the bot.");
				return 0;
			}

			if (!Database.DiscordBotLinks.TryParse(parsed.Discord, out Int64 discordUserID))
			{
				throw new CliException("A Discord user id is 15 to 20 digits.");
			}

			(Database.DiscordBotLinkOutcome outcome, Int64? heldBy) = await Database.DiscordBotLinks.Set(db, parsed.UserID, discordUserID, actor);
			if (outcome == Database.DiscordBotLinkOutcome.InUse)
			{
				throw new CliException($"Discord user {discordUserID} is already linked to user {heldBy}.");
			}
			Console.WriteLine($"The bot treats Discord user {discordUserID} as user {parsed.UserID}.");
			return 0;
		}

		public static async Task<int> RunUsers(string[] args)
		{
			try
			{
				Arguments parsed = Parse(args);

				await using AppDbContext db = Program.OpenConfiguredDbContext();
				if ((await db.Database.GetPendingMigrationsAsync()).Any())
				{
					throw new CliException("The database has pending migrations. Run \"GenOnlineService migrate\" first.");
				}

				if (parsed.Command == "discord-bot-link")
				{
					return await LinkDiscordBot(db, parsed);
				}

				if (parsed.Command == "name-reset")
				{
					var actor = new Database.ModerationActor { Kind = AuditActorKinds.Cli, Name = CliActor(), Source = ModerationSources.Cli };
					Realtime.Result<Realtime.Empty> reset = await new Names.PlayerNames(new ConfiguredDbContextFactory()).ResetNameByStaffAsync(actor, parsed.UserID, parsed.Reason, live: false);
					if (!reset.Ok)
					{
						throw new InvalidOperationException(reset.Code == Realtime.ErrorCodes.NotFound ? $"No user with id {parsed.UserID}." : $"The name of user {parsed.UserID} was not reset: {reset.Code}.");
					}
					Console.WriteLine($"The name of user {parsed.UserID} is reset and their sign-ins ended; they choose a new name when they sign in again.");
					return 0;
				}

				if (parsed.Command == "delete")
				{
					// a banned account keeps its mailbox and provider ids as hashes made with the service's device key; a key this
					// command made itself would never match, so a missing key stops it
					string keyPath = DeviceHasher.GetKeyPath(WebApplication.CreateBuilder(Array.Empty<string>()).Configuration);
					if (!File.Exists(keyPath))
					{
						throw new CliException($"The device HMAC key is not at {keyPath}. Run this on the service's host with its configuration (Devices:hmac_key_path).");
					}
					DeviceHasher.Initialize(keyPath);
					var actor = new Database.ModerationActor { Kind = AuditActorKinds.Cli, Name = CliActor(), Source = ModerationSources.Cli };
					ErasureResult erased = await AccountErasure.Erase(db, parsed.UserID, actor, parsed.Reason, live: false);
					if (!erased.Found)
					{
						throw new InvalidOperationException($"No user with id {parsed.UserID}.");
					}
					Console.WriteLine(erased.AlreadyErased
						? $"User {parsed.UserID} was already erased, nothing changed."
						: $"User {parsed.UserID} is erased; their name left {erased.MatchesRenamed} match row(s)."
							+ (erased.BanHeld ? " An active ban holds their device ids and sign-in provider links until it ends." : String.Empty));
					return 0;
				}

				if (parsed.Command == "two-factor-reset")
				{
					await UserResets.ResetTwoFactor(db, parsed.UserID, CliActor(), parsed.Reason);
					Console.WriteLine($"Two-factor sign-in of user {parsed.UserID} is off; they can set it up again after signing in.");
					return 0;
				}

				string? password = parsed.PasswordFromStdin ? Console.In.ReadLine() : null;
				int minLength = Program.g_Config == null ? PasswordSignInSettings.MinimumLength : AccountsSettings.Read(Program.g_Config).Password.MinLength;
				string set = await UserResets.ResetPassword(db, parsed.UserID, password, minLength, CliActor(), parsed.Reason);
				Console.WriteLine(parsed.PasswordFromStdin
					? $"The password of user {parsed.UserID} is set."
					: $"The new password of user {parsed.UserID} (shown once): {set}");
				return 0;
			}
			catch (CliException ex)
			{
				Console.Error.WriteLine(ex.Message);
				return 2;
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex.Message);
				return 1;
			}
		}
	}
}
