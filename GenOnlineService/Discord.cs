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

#define USE_DISCORD_IN_DEBUG

using Amazon.S3.Model;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using GenOnlineService;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Collections.Concurrent;

public enum EDiscordChannelIDs
{
	Any = -1,
	NetworkRoomChat = 1,
	AdminCommands = 2,
	DirectMessage = 0
}

public static class Helpers
{
	public static string ComputeMD5Hash(string input)
	{
		using (MD5 md5 = MD5.Create())
		{
			byte[] inputBytes = Encoding.UTF8.GetBytes(input);
			byte[] hashBytes = md5.ComputeHash(inputBytes);

			// Convert byte array to hexadecimal string
			StringBuilder sb = new StringBuilder();
			foreach (byte b in hashBytes)
			{
				sb.Append(b.ToString("x2"));
			}
			return sb.ToString();
		}
	}
	public static Int64 GetUnixTimestamp(bool toUTC = false)
	{
		DateTime now = DateTime.Now;

		if (toUTC)
		{
			now = now.ToUniversalTime();
		}

		return (Int64)now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
	}
}

public class DiscordBot
{
	private static readonly Microsoft.Extensions.Logging.ILogger s_log = AppLog.For(typeof(DiscordBot));

	DiscordSocketClient? discord = null;

	private Dictionary<EDiscordChannelIDs, ulong> g_dictChannelIDs = new Dictionary<EDiscordChannelIDs, ulong>();
	private Dictionary<EDiscordChannelIDs, ISocketMessageChannel> g_dictChannels = new Dictionary<EDiscordChannelIDs, ISocketMessageChannel>();

	public DiscordBot()
	{
#if !DEBUG
		_ = InitAsync().ContinueWith(t =>
		{
			if (t.IsFaulted)
				s_log.LogError(t.Exception, "Discord initialization failed");
		}, TaskContinuationOptions.OnlyOnFaulted);
#endif
	}

	~DiscordBot()
	{
		if (discord != null)
		{
			discord.LogoutAsync();
		}
	}

	public async Task SendNetworkRoomChat(int roomID, Int64 userID, string strDisplayName, string strMessage)
	{
		try
		{
            if (Program.g_Config == null)
            {
                return;
            }

            IConfiguration? discordSettings = Program.g_Config.GetSection("Discord");

            if (discordSettings == null)
            {
                return;
            }

            bool discord_send_room_chat_to_discord = discordSettings.GetValue<bool>("send_room_chat_to_discord");


			if (discord_send_room_chat_to_discord)
			{
                string strFormattedChatMsg = String.Format("[{0} - UID {1}] {2}", strDisplayName, userID, strMessage);

                ISocketMessageChannel? channel = GetChannel(EDiscordChannelIDs.NetworkRoomChat);
                if (channel != null)
                {
                    string strDiscordMsg = String.Format("[NETWORK ROOM CHAT ID #{0}] {1}", roomID, strFormattedChatMsg);
                    await channel.SendMessageAsync(strDiscordMsg, allowedMentions: AllowedMentions.None).ConfigureAwait(true);
                }
            }
		}
		catch
		{

		}
	}

	public string GetDiscordUsernameFromID(UInt64 discordID)
	{
		if (discord != null)
		{
			var user = discord.GetUser(discordID);
			if (user != null)
			{
				return user.Username;
			}
		}

		return String.Empty;
	}

	public void UpdateBotStatus(string strStatus)
	{
		if (discord != null)
		{
			Game game = new Game(strStatus, ActivityType.Playing);
			discord.SetStatusAsync(UserStatus.Online);
			discord.SetActivityAsync(game);
		}
	}

	private Task OnReady()
	{
		if (Program.g_Config == null)
		{
			return Task.CompletedTask;
		}

		IConfiguration? discordSettings = Program.g_Config.GetSection("Discord");

		if (discordSettings == null)
		{
			return Task.CompletedTask;
		}

		UInt64? discord_network_room_chat_channel = discordSettings.GetValue<UInt64>("discord_network_room_chat_channel");
		UInt64? discord_admin_commands_channel = discordSettings.GetValue<UInt64>("discord_admin_commands_channel");

		if (discord_network_room_chat_channel == null || discord_admin_commands_channel == null)
		{
			return Task.CompletedTask;
		}

		// cache our channels
		g_dictChannelIDs[EDiscordChannelIDs.NetworkRoomChat] = (ulong)discord_network_room_chat_channel;
		g_dictChannelIDs[EDiscordChannelIDs.AdminCommands] = (ulong)discord_admin_commands_channel;

		ISocketMessageChannel? channel = GetChannel(EDiscordChannelIDs.NetworkRoomChat);
		if (channel != null)
		{
			//await channel.SendMessageAsync("Bot Started").ConfigureAwait(true);
		}

		return Task.CompletedTask;
	}

	private bool IsChannelAnAdminChannel(ulong channelID)
	{
		EDiscordChannelIDs discordChannelID = EDiscordChannelIDs.NetworkRoomChat;
		foreach (var channel in g_dictChannelIDs)
		{
			if (channel.Value == channelID)
			{
				discordChannelID = channel.Key;
				break;
			}
		}

		if (discordChannelID == EDiscordChannelIDs.NetworkRoomChat || discordChannelID == EDiscordChannelIDs.AdminCommands)
		{
			return true;
		}

		return false;
	}

	public bool IsReady()
	{
		return discord != null && discord.ConnectionState == ConnectionState.Connected;
	}

	public bool GetChannelID(out ulong channelID, EDiscordChannelIDs discordChannelID)
	{
		if (g_dictChannelIDs.ContainsKey(discordChannelID))
		{
			channelID = g_dictChannelIDs[discordChannelID];
		}

		channelID = 999999;
		return false;
	}

	public bool IsChannelIDDefined(ulong channelID, EDiscordChannelIDs discordChannelID)
	{
		if (g_dictChannelIDs.ContainsKey(discordChannelID))
		{
			return g_dictChannelIDs[discordChannelID] == channelID;
		}

		return false;
	}

	private uint g_cooldownLengthSeconds = 20;
	private Dictionary<ulong, double> m_dictCooldowns = new Dictionary<ulong, double>();

	private bool DoesDiscordClientHaveCooldown(ulong channelID, SocketUser user)
	{
		// Never have a cooldown for admin channels
		if (IsChannelAnAdminChannel(channelID))
		{
			return false;
		}

		ExpireCooldowns();
		return m_dictCooldowns.ContainsKey(user.Id);
	}

	private void ExpireCooldowns()
	{
		Int64 unixTimestamp = Helpers.GetUnixTimestamp();

		List<ulong> m_lstToRemove = new List<ulong>();
		foreach (var kvPair in m_dictCooldowns)
		{
			if ((kvPair.Value + g_cooldownLengthSeconds) <= unixTimestamp)
			{
				m_lstToRemove.Add(kvPair.Key);
			}
		}

		foreach (ulong key in m_lstToRemove)
		{
			m_dictCooldowns.Remove(key);
		}
	}

	private void CreateCooldown(ulong channelID, SocketUser user)
	{
		if (!IsChannelAnAdminChannel(channelID))
		{
			Int64 unixTimestamp = Helpers.GetUnixTimestamp();
			m_dictCooldowns[user.Id] = unixTimestamp;
		}
	}

	// Slash commands need no privileged intent, so the bot never reads message content
	// Staff commands stay visible to everyone, and the service decides who may run them
	private string m_root = DiscordCommands.DefaultRoot;

	private bool m_bCommandsRegistered = false;

	// Guild commands appear immediately, global commands can take up to an hour
	private async Task OnReadyAsync()
	{
		if (m_bCommandsRegistered || discord == null || Program.g_Config == null)
		{
			return;
		}

		ulong guildID = Program.g_Config.GetSection("Discord").GetValue<ulong>("discord_guild_id");
		if (guildID == 0)
		{
			s_log.LogWarning("Discord:discord_guild_id is not set, slash commands are not registered");
			return;
		}

		SocketGuild? guild = discord.GetGuild(guildID);
		if (guild == null)
		{
			s_log.LogWarning("The Discord bot is not a member of guild {GuildId}, slash commands are not registered", guildID);
			return;
		}

		string root = Program.g_Config.GetSection("Discord").GetValue<string>("command_name") ?? DiscordCommands.DefaultRoot;
		if (!DiscordCommands.IsValidRoot(root))
		{
			s_log.LogError("Discord:command_name {CommandName} is not a Discord command name (1 to 32 of a-z, 0-9, - and _), slash commands are not registered", root);
			return;
		}
		m_root = root;

		// replaces every command the bot registered before, so a renamed root leaves nothing behind
		await guild.BulkOverwriteApplicationCommandAsync([DiscordCommands.BuildRoot(root)]).ConfigureAwait(false);
		m_bCommandsRegistered = true;
	}

	// The GenOnline accounts behind a Discord user, one signing in with that Discord account and one staff linked to it for the
	// bot only, so a Discord user without either gets no staff command
	private static async Task<List<Int64>> AccountsOf(ulong discordUserID)
	{
		using var scope = ServiceLocator.Services.CreateScope();
		var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
		await using var db = await factory.CreateDbContextAsync();
		string externUid = discordUserID.ToString();
		List<Int64> accounts = await db.Identities.AsNoTracking()
			.Where(i => i.Provider == IdentityProviders.Discord && i.ExternUid == externUid)
			.Select(i => i.UserID)
			.ToListAsync();
		if (discordUserID <= Int64.MaxValue && await Database.DiscordBotLinks.UserOf(db, (Int64)discordUserID) is Int64 botLinked && !accounts.Contains(botLinked))
		{
			accounts.Add(botLinked);
		}
		return accounts;
	}

	private static Database.ModerationActor? ActorFor(ulong discordUserID, IEnumerable<Int64> accounts, string permission)
	{
		foreach (Int64 userID in accounts)
		{
			if (StaffRoles.HasPermission(userID, permission))
			{
				return new Database.ModerationActor { Kind = AuditActorKinds.Discord, UserID = userID, Name = $"discord:{discordUserID}", Source = ModerationSources.Discord };
			}
		}
		return null;
	}

	private async Task OnSlashCommandAsync(SocketSlashCommand command)
	{
		try
		{
			SocketSlashCommandDataOption? sub = command.Data.Options.FirstOrDefault(o => o.Type == ApplicationCommandOptionType.SubCommand);
			if (command.CommandName != m_root || sub == null)
			{
				await command.RespondAsync("Unknown command.", ephemeral: true);
				return;
			}

			if (sub.Name == "players" || sub.Name == "lobbies")
			{
				if (DoesDiscordClientHaveCooldown(command.ChannelId ?? 0, command.User))
				{
					await command.RespondAsync("Too many commands. Please wait.", ephemeral: true);
					return;
				}

				CreateCooldown(command.ChannelId ?? 0, command.User);

				string publicReply = sub.Name == "players"
					? $"There are currently {GenOnlineService.WebSocketManager.GetNumberOfUsersOnline()} players online."
					: $"There are currently {ServiceLocator.Services.GetRequiredService<LobbyManager>().GetNumLobbies()} lobbies.";
				await command.RespondAsync(publicReply, allowedMentions: AllowedMentions.None);
				return;
			}

			DiscordCommand? known = DiscordCommands.Find(sub.Name);
			if (known == null)
			{
				await command.RespondAsync("Unknown command.", ephemeral: true);
				return;
			}

			List<Int64> accounts = await AccountsOf(command.User.Id);
			if (known.Name == DiscordCommands.Help)
			{
				await command.RespondAsync(DiscordCommands.HelpText(m_root, permission => accounts.Any(a => StaffRoles.HasPermission(a, permission))), ephemeral: true);
				return;
			}

			if (!IsChannelIDDefined(command.ChannelId ?? 0, EDiscordChannelIDs.AdminCommands))
			{
				await command.RespondAsync("Staff commands only work in the admin commands channel.", ephemeral: true);
				return;
			}

			Database.ModerationActor? actor = known.Permission == null ? null : ActorFor(command.User.Id, accounts, known.Permission);
			if (actor == null)
			{
				await command.RespondAsync($"You don't have access to this command. /{m_root} help lists the ones you can use.", ephemeral: true);
				return;
			}

			// a ban or an announcement can take longer than the 3 second reply window
			await command.DeferAsync();
			string reply = await DiscordCommands.RunAsync(sub.Name, sub.Options, actor);
			await command.FollowupAsync(reply, allowedMentions: AllowedMentions.None);
		}
		catch (Exception ex)
		{
			s_log.LogError(ex, "Discord slash command {Command} failed", command.CommandName);

			if (command.HasResponded)
			{
				await command.FollowupAsync("The command failed, check the service logs.", ephemeral: true).ConfigureAwait(false);
			}
		}
	}

	private static Task LogAsync(LogMessage log)
	{
		s_log.LogDebug("Discord: {DiscordLogMessage}", log.ToString());
		return Task.CompletedTask;
	}

	private async Task InitAsync()
	{
#if !DEBUG || USE_DISCORD_IN_DEBUG
		DiscordSocketConfig conf = new();
		conf.GatewayIntents = GatewayIntents.Guilds;
		discord = new DiscordSocketClient(conf);

		// event handlers
		discord.Connected += OnReady;
		discord.Log += LogAsync;
		discord.Ready += OnReadyAsync;
		discord.SlashCommandExecuted += OnSlashCommandAsync;

		//1354979004507226294

		IConfigurationSection? discordSettings = Program.g_Config?.GetSection("Discord");

		if (discordSettings == null)
		{
			throw new Exception("Discord section missing in config");
		}

		string? discordToken = discordSettings.GetValue<string>("token");

		if (discordToken == null)
		{
			throw new Exception("Discord Token missing in config");
		}

		await discord.LoginAsync(TokenType.Bot, discordToken).ConfigureAwait(true);
		await discord.StartAsync().ConfigureAwait(true);
#else
		await Task.Delay(1).ConfigureAwait(true);
#endif
	}

	public void PushDM(SocketUser user, string strMessage)
	{
		try
		{
			if (user != null)
			{
				user.SendMessageAsync(strMessage, allowedMentions: AllowedMentions.None).ContinueWith(t => { }, TaskContinuationOptions.OnlyOnFaulted);
			}
		}
		catch
		{
			// User probably has some privacy settings that do not allow us to send DMs
		}
	}

	public SocketUser? GetDiscordUserFromDiscordID(ulong DiscordUserID)
	{
		if (discord != null)
		{
			return discord.GetUser(DiscordUserID);
		}

		return null;
	}

	private ISocketMessageChannel? GetChannel(EDiscordChannelIDs channelID)
	{
		ISocketMessageChannel? channel = null;
		if (discord != null)
		{
			if (g_dictChannels.ContainsKey(channelID) && g_dictChannels[channelID] != null)
			{
				channel = g_dictChannels[channelID];
			}
			else
			{
				if (g_dictChannelIDs.ContainsKey(channelID))
				{
					channel = (ISocketMessageChannel)discord.GetChannel(g_dictChannelIDs[channelID]);
					g_dictChannels[channelID] = channel;
				}
			}
		}

		return channel;
	}

	public void PushChannelMessage(EDiscordChannelIDs channelID, string strMessage)
	{
		try
		{
			ISocketMessageChannel? channel = GetChannel(channelID);
			if (channel != null)
			{
				channel.SendMessageAsync(strMessage, allowedMentions: AllowedMentions.None).ContinueWith(t => { }, TaskContinuationOptions.OnlyOnFaulted);
			}
		}
		catch
		{

		}
	}

	public async Task PushMessage(SocketUser user, ulong channelToUse, string strMessage)
	{
		try
		{
			if (discord != null)
			{
				if (channelToUse == (ulong)EDiscordChannelIDs.DirectMessage)
				{
					PushDM(user, strMessage);
				}
				else
				{
					ISocketMessageChannel channel = (ISocketMessageChannel)discord.GetChannel(channelToUse);
					if (channel != null)
					{
						RestUserMessage msg = await channel.SendMessageAsync(strMessage, allowedMentions: AllowedMentions.None).ConfigureAwait(true);
					}
				}
			}
		}
		catch
		{

		}
	}
}
