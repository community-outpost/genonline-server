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

using Discord;
using Discord.WebSocket;
using GenOnlineService;
using GenOnlineService.Api;
using GenOnlineService.Identity;
using GenOnlineService.Portal.Client;
using GenOnlineService.Realtime;
using GenOnlineService.Rest;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

// One subcommand of the bot's command, whose permission is the one the same action needs on the staff pages so a role means
// the same in Discord as anywhere else, and null for a public command
public sealed record DiscordCommand(string Name, string Description, string? Permission, Func<SlashCommandOptionBuilder, SlashCommandOptionBuilder>? Options = null)
{
	public SlashCommandOptionBuilder Build()
	{
		SlashCommandOptionBuilder builder = new SlashCommandOptionBuilder().WithName(Name).WithDescription(Description).WithType(ApplicationCommandOptionType.SubCommand);
		return Options?.Invoke(builder) ?? builder;
	}
}

public static class DiscordCommands
{
	public const string Help = "help";

	// Every command is a subcommand of this one, so none sits next to Discord's own /kick, /ban or /timeout in the picker
	public const string DefaultRoot = "go";

	// Discord's rule for a command name, kept to lower-case ASCII so the name reads the same everywhere
	public static bool IsValidRoot(string? name) => name != null && name.Length is >= 1 and <= 32 && name.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_');

	public static ApplicationCommandProperties BuildRoot(string root)
	{
		SlashCommandBuilder builder = new SlashCommandBuilder().WithName(root).WithDescription("Generals Online");
		foreach (DiscordCommand command in All)
		{
			builder.AddOption(command.Build());
		}
		return builder.Build();
	}

	private static SlashCommandOptionBuilder Player(SlashCommandOptionBuilder b) => b.AddOption("user_id", ApplicationCommandOptionType.Integer, "User id of the player (find it with whois)", isRequired: true, minValue: 1);

	private static SlashCommandOptionBuilder Reason(SlashCommandOptionBuilder b, bool required, string description) =>
		b.AddOption("reason", ApplicationCommandOptionType.String, description, isRequired: required, maxLength: ModerationManager.MaximumReasonLength);

	private static SlashCommandOptionBuilder Duration(SlashCommandOptionBuilder b, bool required, string description) =>
		b.AddOption("duration", ApplicationCommandOptionType.String, description, isRequired: required, maxLength: 8);

	// The order is the order /help lists them in
	public static readonly IReadOnlyList<DiscordCommand> All =
	[
		new("players", "Shows how many players are online", null),
		new("lobbies", "Shows how many lobbies exist", null),
		new(Help, "Lists the staff commands you can use", null),
		new("stats", "Shows the uptime, the player peak and what runs right now", Permissions.ServerStats),
		new("whois", "Finds the user id of an online player", Permissions.ModerationKick,
			b => b.AddOption("name", ApplicationCommandOptionType.String, "Display name, or Name#tag", isRequired: true, maxLength: 37)),
		new("player", "Shows a player's account, roles and active sanctions", Permissions.ModerationKick, Player),
		new("kick", "Kicks an online player from the server", Permissions.ModerationKick,
			b => Reason(Player(b), required: false, "Reason shown to the player")),
		new("mute", "Mutes a player's chat for a while", Permissions.ModerationMute,
			b => Reason(Duration(Player(b), required: true, "How long: 30m, 12h, 7d or 2w"), required: false, "Reason shown to the player")),
		new("unmute", "Lifts a player's active mute", Permissions.ModerationMute,
			b => Reason(Player(b), required: false, "Reason kept in the audit log")),
		new("ban", "Bans a player from playing; sign-in keeps working so they can appeal", Permissions.ModerationBan,
			b => Duration(Reason(Player(b), required: true, "Reason shown to the player"), required: false, "How long: 12h, 7d, 2w; leave out for a permanent ban")),
		new("unban", "Lifts a player's active ban", Permissions.ModerationBan,
			b => Reason(Player(b), required: false, "Reason kept in the audit log")),
		new("rename", "Replaces an offensive display name, the tag follows the new name", Permissions.ModerationRename,
			b => Reason(Player(b).AddOption("name", ApplicationCommandOptionType.String, "The new display name", isRequired: true, maxLength: RealtimeHub.NameMaxLength), required: false, "Reason kept in the audit log")),
		new("resetname", "Takes away a display name; the player chooses a new one at the next sign-in", Permissions.ModerationRename,
			b => Reason(Player(b), required: false, "Reason kept in the audit log")),
		new("announce", "Sends an announcement to every online player", Permissions.AnnouncementsSend,
			b => b.AddOption("message", ApplicationCommandOptionType.String, "Announcement text", isRequired: true, maxLength: Announcements.MaximumLength)),
	];

	public static DiscordCommand? Find(string name) => All.FirstOrDefault(c => c.Name == name);

	// Whole minutes, hours, days or weeks, from one minute up to the longest sanction
	public static long? ParseDuration(string? text)
	{
		string value = text?.Trim().ToLowerInvariant() ?? String.Empty;
		if (value.Length < 2 || !Int64.TryParse(value.AsSpan(0, value.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out long amount) || amount <= 0)
		{
			return null;
		}
		long unit = value[^1] switch
		{
			'm' => 60,
			'h' => 3600,
			'd' => 86400,
			'w' => 604800,
			_ => 0
		};
		if (unit == 0 || amount > SanctionActions.MaximumDurationS / unit)
		{
			return null;
		}
		long seconds = amount * unit;
		return seconds is >= SanctionActions.MinimumDurationS and <= SanctionActions.MaximumDurationS ? seconds : null;
	}

	public static string HelpText(string root, Func<string, bool> holds)
	{
		List<DiscordCommand> mine = All.Where(c => c.Permission != null && holds(c.Permission)).ToList();
		if (mine.Count == 0)
		{
			return $"You can use /{root} players and /{root} lobbies. Staff commands need a GenOnline staff account linked to your Discord account.";
		}

		var text = new StringBuilder("Your staff commands (in the admin commands channel):");
		foreach (DiscordCommand command in mine)
		{
			text.Append('\n').Append('/').Append(root).Append(' ').Append(command.Name).Append(" — ").Append(command.Description);
		}
		return text.ToString();
	}

	// A failure in words that names a code without its own text, so the audit log and the reply can be matched up
	public static string Explain(string? code, object? parameters, string what)
	{
		string? field = parameters?.GetType().GetProperty("Field")?.GetValue(parameters) as string;
		return code switch
		{
			ErrorCodes.ModerationTargetOutranks => "You can only act on players below your own staff rank, and never on yourself.",
			ErrorCodes.ModerationTargetOffline => "That player is not online.",
			ErrorCodes.ModerationReasonTooLong => $"The reason can be {ModerationManager.MaximumReasonLength} characters at most.",
			ErrorCodes.NotFound => "No such player, or nothing of that kind to undo.",
			ErrorCodes.Forbidden => "You don't have access to this command.",
			GenOnlineService.Rest.RestCodes.Conflict => "The player already has one of those running.",
			ErrorCodes.InvalidArgument when field == "reason" => "This needs a reason.",
			ErrorCodes.InvalidArgument when field == "id" => "That is not a player you can act on.",
			ErrorCodes.InvalidArgument when field == "duration_s" => "The duration must be 1 minute to 10 years, such as 30m, 12h, 7d or 2w.",
			_ => $"{what} was not done: {code}."
		};
	}

	private static T? Option<T>(IEnumerable<SocketSlashCommandDataOption> options, string name)
	{
		object? value = options.FirstOrDefault(o => o.Name == name)?.Value;
		return value is T typed ? typed : default;
	}

	private static string When(DateTime? utc) => utc == null ? "never" : utc.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";

	private static string? PortalLink(long userID)
	{
		string? issuer = ServiceLocator.Services.GetService<AccountsSettings>()?.Issuer;
		return String.IsNullOrEmpty(issuer) ? null : issuer.TrimEnd('/') + PortalRoutes.PlayerPage(userID);
	}

	// The staff routes' own code runs, so every rule, rank check and audit row is the same as on the staff pages
	public static async Task<string> RunAsync(string name, IReadOnlyCollection<SocketSlashCommandDataOption> command, Database.ModerationActor actor)
	{
		IDbContextFactory<AppDbContext> dbFactory = ServiceLocator.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
		Func<string, Task<Failure?>> requirePermission = permission => Task.FromResult<Failure?>(StaffRoles.HasPermission(actor.UserID!.Value, permission) ? null : Result.Forbidden(permission));
		long target = Option<long>(command, "user_id");
		string? reason = Option<string>(command, "reason");

		switch (name)
		{
			case "stats":
			{
				TimeSpan uptime = DateTime.UtcNow.Subtract(Program.g_LastStartTime);
				return $"Up since {When(Program.g_LastStartTime)} ({uptime.Days}d {uptime.Hours}h {uptime.Minutes}m). "
					+ $"{GenOnlineService.WebSocketManager.GetNumberOfUsersOnline()} players online, peak {GenOnlineService.WebSocketManager.g_PeakConnectionCount} since the start, "
					+ $"{ServiceLocator.Services.GetRequiredService<LobbyManager>().GetNumLobbies()} lobbies.";
			}
			case "whois":
			{
				string lookup = Option<string>(command, "name") ?? String.Empty;
				SharedUserData? found = GenOnlineService.WebSocketManager.GetSharedDataForUser(lookup);
				return found != null
					? $"{Database.DisplayNames.Format(found.m_strDisplayName, found.m_NameTag)} is user {found.m_UserID}."
					: $"{lookup} is not online, or more than one online player has that name: use Name#tag.";
			}
			case "player":
			{
				Result<PortalPlayer> read = await GenOnlineService.Rest.StaffPlayerActions.GetAsync(dbFactory, target);
				if (!read.Ok)
				{
					return Explain(read.Code, read.Params, "The lookup");
				}
				PortalPlayer player = read.Data!;
				var text = new StringBuilder($"{player.FullName} (user {player.Id}), {player.State}");
				text.Append(player.Online ? (player.LobbyId is long lobby ? $", online in lobby {lobby}" : ", online") : ", offline");
				text.Append($"\nCreated {When(player.CreatedAt)}, last sign-in {When(player.LastSignInAt)}");
				text.Append(player.Roles.Count > 0 ? $"\nRoles: {String.Join(", ", player.Roles)}" : "\nNo staff role");
				foreach (PortalSanction sanction in player.Sanctions.Where(s => s.Active))
				{
					text.Append($"\nActive {sanction.Type} until {(sanction.ExpiresAt == null ? "lifted" : When(sanction.ExpiresAt))}");
					if (!String.IsNullOrEmpty(sanction.Reason))
					{
						text.Append($": {sanction.Reason}");
					}
				}
				if (PortalLink(player.Id) is string link)
				{
					text.Append($"\n{link}");
				}
				return text.ToString();
			}
			case "kick":
			case "mute":
			case "ban":
			{
				SanctionType type = name switch { "kick" => SanctionType.Kick, "mute" => SanctionType.Mute, _ => SanctionType.Ban };
				string? durationText = Option<string>(command, "duration");
				long? duration = ParseDuration(durationText);
				if (durationText != null && duration == null)
				{
					return Explain(ErrorCodes.InvalidArgument, new { Field = "duration_s" }, "The sanction");
				}

				var request = new CreateSanctionRequest { Type = type, DurationS = duration, Reason = String.IsNullOrWhiteSpace(reason) ? null : reason.Trim() };
				Result<CreateSanctionResponse> applied = await SanctionActions.CreateAsync(dbFactory, actor, target, request, requirePermission);
				if (!applied.Ok)
				{
					return Explain(applied.Code, applied.Params, $"The {name}");
				}
				return type switch
				{
					SanctionType.Kick => $"User {target} is kicked.",
					SanctionType.Mute => $"User {target} is muted for {durationText}.",
					_ => duration == null ? $"User {target} is banned permanently." : $"User {target} is banned for {durationText}."
				};
			}
			case "unmute":
			case "unban":
			{
				string type = name == "unmute" ? SanctionTypes.Mute : SanctionTypes.Ban;
				long? sanctionID;
				await using (AppDbContext db = await dbFactory.CreateDbContextAsync())
				{
					sanctionID = await Database.Moderation.ActiveSanctions(db, type, DateTime.UtcNow)
						.Where(s => s.UserID == target && s.Source == AuthoritySources.Local)
						.OrderByDescending(s => s.ID)
						.Select(s => (long?)s.ID)
						.FirstOrDefaultAsync();
				}
				if (sanctionID == null)
				{
					return $"User {target} has no active {type} to lift.";
				}

				Result<Empty> lifted = await SanctionActions.LiftAsync(dbFactory, actor, sanctionID.Value, reason, requirePermission);
				return lifted.Ok ? $"The {type} of user {target} is lifted." : Explain(lifted.Code, lifted.Params, $"The {name}");
			}
			case "rename":
			{
				string newName = Option<string>(command, "name") ?? String.Empty;
				Result<Empty> renamed = await ServiceLocator.Services.GetRequiredService<GenOnlineService.Names.PlayerNames>().RenameByStaffAsync(actor, target, newName, reason);
				return renamed.Ok ? $"User {target} is now named {newName}." : Explain(renamed.Code, renamed.Params, "The rename");
			}
			case "resetname":
			{
				Result<Empty> reset = await ServiceLocator.Services.GetRequiredService<GenOnlineService.Names.PlayerNames>().ResetNameByStaffAsync(actor, target, reason);
				return reset.Ok ? $"The name of user {target} is reset; they choose a new one at their next sign-in." : Explain(reset.Code, reset.Params, "The name reset");
			}
			case "announce":
			{
				string? message = Option<string>(command, "message");
				await using AppDbContext db = await dbFactory.CreateDbContextAsync();
				(int deliveredTo, Failure? failure) = await Announcements.SendAsync(db, actor, message);
				return failure != null
					? $"An announcement is 1 to {Announcements.MaximumLength} characters."
					: $"The announcement reached {deliveredTo} players.";
			}
			default:
				return "Unknown command.";
		}
	}
}
