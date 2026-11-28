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

namespace GenOnlineService.Realtime
{
	// Lobbies, matchmaking and peer networking (sections 5.2, 5.3, 5.6, 6.3, 6.4, 6.6)
	public sealed partial class RealtimeHub
	{
		public const int LobbyNameMaxLength = 140;
		public const int MapNameMaxLength = 255;
		public const int MapPathMaxLength = 512;
		public const int LobbyPasswordMaxLength = 128;
		public const int MinCameraHeight = 210;
		public const int MaxCameraHeight = 1000;

		// a module scan answer adds at most this many new modules, later scans pick up the rest
		private const int MaxNewModulesPerScan = 256;

		// Reconnects rejoin the lobby group and get the current state and lobby chat; signalling restarts for the unconnected pairs once the new connection's transports arrived
		private async partial Task RestoreLobbyAsync(UserSession session)
		{
			if (session.GetSessionType() != EUserSessionType.GameClient || session.currentLobbyID == -1)
			{
				return;
			}

			Lobby? lobby = m_lobbyManager.GetLobby(session.currentLobbyID);
			if (lobby == null || lobby.GetMemberFromUserID(session.m_UserID) == null)
			{
				return;
			}

			await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Lobby(lobby.LobbyID));
			await SendLobbySnapshotAsync(lobby, session.m_UserID);
			session.MarkSignallingResendPending();
			await ResendSignallingIfDueAsync(session);
		}

		private async Task ResendSignallingIfDueAsync(UserSession session)
		{
			if (!session.TakeSignallingResend())
			{
				return;
			}

			Lobby? lobby = m_lobbyManager.GetLobby(session.currentLobbyID);
			if (lobby != null)
			{
				await lobby.ResendSignallingAfterReconnectAsync(session.m_UserID);
			}
		}

		private async Task SendLobbySnapshotAsync(Lobby lobby, Int64 userID)
		{
			await Clients.Caller.LobbyState(new LobbyStateEvent { Lobby = RealtimeLobbies.StateOf(lobby) with { Start = lobby.StartFor(userID) } });
			await SendLobbyHistoryAsync(lobby);
		}

		public async Task<Result<WatchLobbyListData>> WatchLobbyList(Empty request)
		{
			if (RequireAccess(ESessionAccessType.ServerListReadOnly) is Failure denied)
			{
				return denied;
			}

			await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.LobbyList);
			IReadOnlyList<LobbySummary> lobbies = LobbyListPublisher.Watch(m_lobbyManager, Context.ConnectionId, Session);
			return Result.Success(new WatchLobbyListData { Lobbies = lobbies });
		}

		public Task<Result<Empty>> ReportRegionLatency(ReportRegionLatencyRequest request)
		{
			if (RequireAccess(ESessionAccessType.ServerListReadOnly) is Failure denied)
			{
				return Task.FromResult<Result<Empty>>(denied);
			}
			if (RegionLatency.Report(Session, request, GenOnlineService.PingBeacons.Current, Environment.TickCount64) is Failure refused)
			{
				return Task.FromResult<Result<Empty>>(refused);
			}

			LobbyListPublisher.Refresh(m_lobbyManager, Session);
			if (Session.currentLobbyID != -1)
			{
				LobbyListPublisher.MarkDirty(Session.currentLobbyID);
			}
			return Task.FromResult(Result.Success());
		}

		public async Task<Result<Empty>> UnwatchLobbyList(Empty request)
		{
			if (RequireAccess(ESessionAccessType.ServerListReadOnly) is Failure denied)
			{
				return denied;
			}

			LobbyListPublisher.Unwatch(Context.ConnectionId);
			await Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.LobbyList);
			return Result.Success();
		}

		// A lobby handed to another copy is frozen from its final capture on: a change to it, or a join, is refused as a standby refuses it,
		// its members switch with the lobby and send the change again on the new copy
		internal static Failure? MovingRefusal(Lobby? lobby) => lobby?.IsMovingAway == true
			? Result.Fail(GenOnlineService.Rest.RestCodes.ServiceUnavailable, new { What = GenOnlineService.Failover.RealtimeFailover.StandbyWhat })
			: null;

		private Failure? RefuseOnMovingLobby(Int64? lobbyID = null) => MovingRefusal(m_lobbyManager.GetLobby(lobbyID ?? Session.currentLobbyID));

		// While this copy hands over, new lobbies and queue entries belong on the new copy: the caller is refused as a standby refuses and switched there
		private Failure? RefuseWhileHandingOver()
		{
			if (!GenOnlineService.Failover.RealtimeLiveState.Enabled || GenOnlineService.Failover.RealtimeFailover.Current?.Role != GenOnlineService.Failover.FailoverRole.Draining)
			{
				return null;
			}
			GenOnlineService.Failover.RealtimeFailover.SwitchSoon(Session);
			return Result.Fail(GenOnlineService.Rest.RestCodes.ServiceUnavailable, new { What = GenOnlineService.Failover.RealtimeFailover.StandbyWhat });
		}

		public async Task<Result<CreateLobbyData>> CreateLobby(CreateLobbyRequest request)
		{
			if (RequireAccess(ESessionAccessType.Gameplay) is Failure denied)
			{
				return denied;
			}
			if (RefuseWhileHandingOver() is Failure handingOver)
			{
				return handingOver;
			}

			if (LobbyNameFailure(request.Name) is Failure badName)
			{
				return badName;
			}
			string lobbyName = request.Name.Trim();
			if (request.Map == null) return Result.Invalid("map");
			if (!IsValidText(request.Map.Name, MapNameMaxLength)) return Result.Invalid("map.name");
			if (!IsValidText(request.Map.Path, MapPathMaxLength)) return Result.Invalid("map.path");
			if (request.MaxPlayers < 1 || request.MaxPlayers > Lobby.MaxLobbySize) return Result.Invalid("max_players");
			if (!Enum.IsDefined(request.Visibility)) return Result.Invalid("visibility");
			if (request.Password != null && !IsValidText(request.Password, LobbyPasswordMaxLength)) return Result.Invalid("password");
			if (request.PreferredPort < 0 || request.PreferredPort > UInt16.MaxValue) return Result.Invalid("preferred_port");
			if (request.Settings == null) return Result.Invalid("settings");
			if (request.Settings.StartingCash < 0) return Result.Invalid("settings.starting_cash");
			if (request.Settings.MaxCameraHeight < MinCameraHeight || request.Settings.MaxCameraHeight > MaxCameraHeight) return Result.Invalid("settings.max_camera_height");

			GameSessionRegistration? build = GameSessions.Get(UserID, EUserSessionType.GameClient);
			if (build == null)
			{
				return Result.Fail(ErrorCodes.SessionNotRegistered);
			}
			if (LobbyAnticheat.CreateRefusal((EKnownAnticheatID)build.AnticheatID, request.RequireAnticheat) is string anticheatRefusal)
			{
				return Result.Fail(anticheatRefusal);
			}

			UserSession session = Session;
			Int64 lobbyID;
			await using (AppDbContext db = await m_dbFactory.CreateDbContextAsync())
			{
				(string Name, int Tag) owner = await global::Database.DisplayNames.Get(db, UserID) ?? (String.Empty, 0);
				LobbySettings settings = request.Settings;
				lobbyID = await m_lobbyManager.CreateLobby(db, session, owner.Name, lobbyName, request.Map.Name, request.Map.Path, request.Map.Official, request.MaxPlayers,
					(UInt16)request.PreferredPort, settings.VanillaTeams, settings.TrackStats, (UInt32)settings.StartingCash, settings.LimitSuperweapons,
					request.Password != null, request.Password ?? String.Empty, session.networkRoomID, settings.AllowObservers, (UInt16)settings.MaxCameraHeight,
					GameBuild.Of(build), ELobbyType.CustomGame,
					request.Visibility == LobbyVisibility.Friends ? ELobbyJoinability.FriendsOnly : ELobbyJoinability.Public, owner.Tag, request.RequireAnticheat);
			}

			if (lobbyID < 0)
			{
				return Result.NotFound("session");
			}

			// the host joins as the lobby is created, a lobby the host could not join is removed again
			Lobby? created = m_lobbyManager.GetLobby(lobbyID);
			if (created == null || created.GetMemberFromUserID(UserID) == null)
			{
				if (created != null && await created.TryMarkCompleteIfEmptyAsync())
				{
					await m_lobbyManager.DeleteLobby(created);
				}
				return Result.NotFound("user");
			}

			await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Lobby(lobbyID));
			// The host joined with their favorite side and color, which only the lobby state tells the client
			await SendLobbySnapshotAsync(created, UserID);
			(string username, string token) = await m_lobbyManager.CreateLobbyRelayCredentialsAsync(session, lobbyID);
			return Result.Success(new CreateLobbyData { LobbyId = lobbyID, Turn = new TurnCredentials { Username = username, Token = token } });
		}

		public async Task<Result<JoinLobbyData>> JoinLobby(JoinLobbyRequest request)
		{
			if ((RefuseOnMovingLobby(request.LobbyId) ?? RefuseOnMovingLobby()) is Failure moving)
			{
				return moving;
			}
			if (RequireAccess(ESessionAccessType.Gameplay) is Failure denied)
			{
				return denied;
			}

			if (request.Password != null && !IsValidText(request.Password, LobbyPasswordMaxLength)) return Result.Invalid("password");
			if (request.PreferredPort < 0 || request.PreferredPort > UInt16.MaxValue) return Result.Invalid("preferred_port");

			GameSessionRegistration? build = GameSessions.Get(UserID, EUserSessionType.GameClient);
			if (build == null)
			{
				return Result.Fail(ErrorCodes.SessionNotRegistered);
			}

			JoinLobbyResult joined = await m_lobbyManager.JoinAsync(new JoinLobbyCommand(request.LobbyId, UserID, SessionType, (UInt16)request.PreferredPort,
				request.HasMap, request.Password, GameBuild.Of(build)));

			if (joined.Status != LobbyMembershipStatus.Success)
			{
				return JoinFailure(joined.Status);
			}

			await Groups.AddToGroupAsync(Context.ConnectionId, RealtimeGroups.Lobby(request.LobbyId));
			GenOnlineService.Rules.RuleRunner.Raise(GenOnlineService.Rules.RuleEvent.Of(GenOnlineService.Rules.RuleTriggers.LobbyJoin, UserID, null, (GenOnlineService.Rules.RuleEventFields.LobbyId, request.LobbyId)));
			Lobby? joinedLobby = m_lobbyManager.GetLobby(request.LobbyId);
			if (joinedLobby != null && joinedLobby.GetMemberFromUserID(UserID) != null)
			{
				await SendLobbySnapshotAsync(joinedLobby, UserID);
			}
			return Result.Success(new JoinLobbyData { Turn = new TurnCredentials { Username = joined.TurnUsername, Token = joined.TurnToken } });
		}

		private static Failure JoinFailure(LobbyMembershipStatus status) => status switch
		{
			LobbyMembershipStatus.NotMatched => Result.Fail(ErrorCodes.LobbyNotMatched),
			LobbyMembershipStatus.AnticheatMismatch => Result.Fail(ErrorCodes.LobbyAnticheatMismatch),
			LobbyMembershipStatus.BuildMismatch => Result.Fail(ErrorCodes.LobbyBuildMismatch),
			LobbyMembershipStatus.PasswordMismatch => Result.Fail(ErrorCodes.LobbyWrongPassword),
			LobbyMembershipStatus.NotInSetup => Result.Fail(ErrorCodes.LobbyNotInSetup),
			LobbyMembershipStatus.AlreadyMember => Result.Fail(ErrorCodes.LobbyAlreadyMember),
			LobbyMembershipStatus.NoOpenSlot => Result.Fail(ErrorCodes.LobbyFull),
			LobbyMembershipStatus.BlockedByOwner => Result.Fail(ErrorCodes.LobbyBlocked),
			LobbyMembershipStatus.Banned => Result.Fail(ErrorCodes.LobbyBanned),
			LobbyMembershipStatus.FriendshipRequired => Result.Fail(ErrorCodes.LobbyFriendsOnly),
			LobbyMembershipStatus.SessionUnavailable => Result.NotFound("session"),
			LobbyMembershipStatus.PreferencesUnavailable => Result.NotFound("user"),
			_ => Result.NotFound("lobby"),
		};

		public async Task<Result<Empty>> LeaveLobby(Empty request)
		{
			if (RefuseOnMovingLobby() is Failure moving)
			{
				return moving;
			}
			if (RequireAccess(ESessionAccessType.Gameplay) is Failure denied)
			{
				return denied;
			}

			Int64 lobbyID = Session.currentLobbyID;
			if (lobbyID == -1 || !await m_lobbyManager.LeaveAsync(new LeaveLobbyCommand(lobbyID, UserID, SessionType)))
			{
				return Result.Fail(ErrorCodes.LobbyNotMember);
			}

			await Groups.RemoveFromGroupAsync(Context.ConnectionId, RealtimeGroups.Lobby(lobbyID));
			return Result.Success();
		}

		public Task<Result<Empty>> UpdateLobby(UpdateLobbyRequest request)
		{
			if (RefuseOnMovingLobby() is Failure moving)
			{
				return Task.FromResult<Result<Empty>>(moving);
			}
			if (request.Map != null)
			{
				if (!IsValidText(request.Map.Name, MapNameMaxLength)) return Invalid("map.name");
				if (!IsValidText(request.Map.Path, MapPathMaxLength)) return Invalid("map.path");
				if (request.Map.MaxPlayers < 1 || request.Map.MaxPlayers > Lobby.MaxLobbySize) return Invalid("map.max_players");
			}
			if (request.Settings?.StartingCash < 0) return Invalid("settings.starting_cash");
			if (request.Settings?.MaxCameraHeight is int height && (height < MinCameraHeight || height > MaxCameraHeight)) return Invalid("settings.max_camera_height");
			if (request.Visibility is LobbyVisibility visibility && !Enum.IsDefined(visibility)) return Invalid("visibility");
			if (request.Name != null && LobbyNameFailure(request.Name) is Failure badName) return Task.FromResult<Result<Empty>>(badName);

			return ChangeLobbyAsync(hostOnly: true, resetsReady: ChangesGameTerms(request), async (lobby, member) =>
			{
				if (RealtimeLobbies.RefuseQuickMatchSettings(lobby, request) is Failure locked)
				{
					await ReportQuickMatchRuleAsync(lobby, nameof(UpdateLobby), []);
					return locked;
				}
				if (request.Map != null || request.Settings?.StartingCash != null || request.Settings?.LimitSuperweapons != null)
				{
					await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
					if (request.Map != null)
					{
						await lobby.UpdateMap(db, request.Map.Name, request.Map.Path, request.Map.Official, request.Map.MaxPlayers);
					}
					if (request.Settings?.StartingCash is int cash)
					{
						await lobby.UpdateStartingCash(db, (UInt32)cash);
					}
					if (request.Settings?.LimitSuperweapons is bool limit)
					{
						await lobby.UpdateLimitSuperweapons(db, limit);
					}
				}
				if (request.Settings?.MaxCameraHeight is int cameraHeight)
				{
					lobby.UpdateMaxCameraHeight((UInt16)cameraHeight);
				}
				// track_stats and the anticheat stay as the lobby was created
				if (request.Settings?.VanillaTeams is bool vanillaTeams)
				{
					lobby.UpdateVanillaTeams(vanillaTeams);
				}
				if (request.Settings?.AllowObservers is bool allowObservers)
				{
					lobby.UpdateAllowObservers(allowObservers);
				}
				if (request.Visibility is LobbyVisibility newVisibility)
				{
					lobby.UpdateJoinability(newVisibility == LobbyVisibility.Friends ? ELobbyJoinability.FriendsOnly : ELobbyJoinability.Public);
				}
				if (request.Name != null)
				{
					lobby.UpdateName(request.Name.Trim());
				}
				return null;
			});
		}

		// a rename and the access settings are no game terms, so they keep the ready flags and a pending start
		internal static bool ChangesGameTerms(UpdateLobbyRequest request) => request.Map != null
			|| request.Settings?.StartingCash != null || request.Settings?.LimitSuperweapons != null || request.Settings?.MaxCameraHeight != null
			|| request.Settings?.VanillaTeams != null || request.Settings?.AllowObservers != null;

		public Task<Result<Empty>> SetLobbyPassword(SetLobbyPasswordRequest request)
		{
			if (RefuseOnMovingLobby() is Failure moving)
			{
				return Task.FromResult<Result<Empty>>(moving);
			}
			if (request.Password != null && !IsValidText(request.Password, LobbyPasswordMaxLength)) return Invalid("password");

			// a password is no game term, so it keeps the ready flags and a pending start
			return ChangeLobbyAsync(hostOnly: true, resetsReady: false, (lobby, member) =>
			{
				if (RealtimeLobbies.RefuseQuickMatchPassword(lobby) is Failure locked)
				{
					return Task.FromResult<Failure?>(locked);
				}
				if (request.Password == null)
				{
					lobby.RemovePassword();
				}
				else
				{
					lobby.AddPassword(request.Password);
				}
				lobby.DirtyRetransmit();
				return Task.FromResult<Failure?>(null);
			});
		}

		public Task<Result<Empty>> UpdateMySlot(UpdateMySlotRequest request)
		{
			if (RefuseOnMovingLobby() is Failure moving)
			{
				return Task.FromResult<Result<Empty>>(moving);
			}
			if (SlotFieldFailure(request.Side, request.Color, request.Team, request.StartPosition, "") is Failure badSlot) return Task.FromResult<Result<Empty>>(badSlot);

			bool changes = request.Side != null || request.Color != null || request.StartPosition != null || request.Team.HasValue;
			return ChangeLobbyAsync(hostOnly: false, resetsReady: changes, async (lobby, member) =>
			{
				if (RealtimeLobbies.RefuseArmySelection(lobby, [request.Side]) is Failure locked)
				{
					await ReportQuickMatchRuleAsync(lobby, nameof(UpdateMySlot), [request.Side]);
					return locked;
				}
				// the observer seat follows the host's setting, as in the original game
				if (request.Side == ClientFeatures.ObserverSide && !lobby.AllowObservers)
				{
					return Result.Invalid("side");
				}
				// a colour another player or AI holds is refused, so it never becomes the favourite either
				if (request.Color is int wanted && Lobby.IsColorTaken(wanted, lobby.Members.Where(m => m != member && (m.IsHuman() || m.IsAI())).Select(m => m.Color)))
				{
					return Result.Invalid("color");
				}
				if (request.Side != null || request.Color != null)
				{
					await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
					if (request.Side is int side)
					{
						await member.UpdateSide(db, side);
					}
					if (request.Color is int color)
					{
						await member.UpdateColor(db, color);
					}
				}
				if (request.StartPosition is int startPosition)
				{
					member.UpdateStartPos(startPosition);
				}
				if (request.Team.HasValue)
				{
					member.UpdateTeam(request.Team.Value ?? -1);
				}
				return null;
			});
		}

		public Task<Result<Empty>> SetHasMap(SetHasMapRequest request)
		{
			if (RefuseOnMovingLobby() is Failure moving)
			{
				return Task.FromResult<Result<Empty>>(moving);
			}
			return ChangeLobbyAsync(hostOnly: false, resetsReady: false, (lobby, member) =>
			{
				member.UpdateHasMap(request.HasMap);
				return Task.FromResult<Failure?>(null);
			});
		}

		public Task<Result<Empty>> SetReady(SetReadyRequest request)
		{
			if (RefuseOnMovingLobby() is Failure moving)
			{
				return Task.FromResult<Result<Empty>>(moving);
			}
			return ChangeLobbyAsync(hostOnly: false, resetsReady: false, (lobby, member) =>
			{
				member.SetReadyState(request.Ready);
				return Task.FromResult<Failure?>(null);
			});
		}

		public Task<Result<Empty>> SetSlotState(SetSlotStateRequest request)
		{
			if (RefuseOnMovingLobby() is Failure moving)
			{
				return Task.FromResult<Result<Empty>>(moving);
			}
			if (request.Index < 0 || request.Index >= Lobby.MaxLobbySize) return Invalid("index");
			if (!Enum.IsDefined(request.State) || request.State == SlotState.Player) return Invalid("state");

			return ChangeLobbyAsync(hostOnly: true, resetsReady: true, (lobby, member) =>
			{
				// human slots are left alone, kicking is how a host removes a player
				LobbyMember? target = lobby.GetMemberFromSlot(request.Index);
				if (target == null || target.IsHuman())
				{
					return Task.FromResult<Failure?>(Result.Fail(ErrorCodes.LobbySlotOccupied, new { Index = request.Index }));
				}
				target.SetPlayerSlotState(RealtimeLobbies.PlayerTypeOf(request.State));
				return Task.FromResult<Failure?>(null);
			});
		}

		public Task<Result<Empty>> UpdateSlots(UpdateSlotsRequest request)
		{
			if (RefuseOnMovingLobby() is Failure moving)
			{
				return Task.FromResult<Result<Empty>>(moving);
			}
			if (request.Slots == null) return Invalid("slots");
			foreach (SlotPatch patch in request.Slots)
			{
				if (patch == null || patch.Index < 0 || patch.Index >= Lobby.MaxLobbySize) return Invalid("slots.index");
				if (SlotFieldFailure(patch.Side, patch.Color, patch.Team, patch.StartPosition, "slots.") is Failure badSlot) return Task.FromResult<Result<Empty>>(badSlot);
			}

			return ChangeLobbyAsync(hostOnly: true, resetsReady: request.Slots.Count > 0, async (lobby, member) =>
			{
				// sides and observer seats are checked for every slot first, so a refused patch changes nothing
				if (RealtimeLobbies.RefuseArmySelection(lobby, request.Slots.Select(patch => patch.Side)) is Failure locked)
				{
					await ReportQuickMatchRuleAsync(lobby, nameof(UpdateSlots), request.Slots.Select(patch => patch.Side));
					return locked;
				}
				foreach (SlotPatch patch in request.Slots.Where(p => p.Side == ClientFeatures.ObserverSide))
				{
					LobbyMember? target = lobby.GetMemberFromSlot(patch.Index);
					if (!lobby.AllowObservers || (target != null && target.IsAI()))
					{
						return Result.Invalid("slots.side");
					}
				}
				foreach (SlotPatch patch in request.Slots.Where(p => p.Color >= 0))
				{
					LobbyMember? target = lobby.GetMemberFromSlot(patch.Index);
					if (Lobby.IsColorTaken(patch.Color!.Value, lobby.Members.Where(m => m != target && (m.IsHuman() || m.IsAI())).Select(m => m.Color)))
					{
						return Result.Invalid("slots.color");
					}
				}
				foreach (SlotPatch patch in request.Slots)
				{
					LobbyMember? target = lobby.GetMemberFromSlot(patch.Index);
					if (target == null || (!target.IsHuman() && !target.IsAI()))
					{
						continue;
					}
					if (patch.Side is int side) target.SetSide(side);
					if (patch.Color is int color) target.SetColor(color);
					if (patch.StartPosition is int startPosition) target.UpdateStartPos(startPosition);
					if (patch.Team.HasValue) target.UpdateTeam(patch.Team.Value ?? -1);
				}
				return null;
			});
		}

		public Task<Result<Empty>> KickFromLobby(UserRequest request) => RemoveFromLobby(request, ban: false);

		public Task<Result<Empty>> BanFromLobby(UserRequest request) => RemoveFromLobby(request, ban: true);

		private async Task<Result<Empty>> RemoveFromLobby(UserRequest request, bool ban)
		{
			if (RefuseOnMovingLobby() is Failure moving)
			{
				return moving;
			}
			if (RequireAccess(ESessionAccessType.Gameplay) is Failure denied)
			{
				return denied;
			}

			if (request.UserId == UserID)
			{
				return Result.Invalid("user_id");
			}

			Int64 lobbyID = Session.currentLobbyID;
			if (lobbyID == -1 || m_lobbyManager.GetLobby(lobbyID) == null)
			{
				return Result.Fail(ErrorCodes.LobbyNotMember);
			}

			LobbyMembershipStatus status = await m_lobbyManager.KickAsync(new KickLobbyMemberCommand(lobbyID, UserID, request.UserId, ban));
			return status switch
			{
				LobbyMembershipStatus.Success => Result.Success(),
				LobbyMembershipStatus.ActorNotMember => Result.Fail(ErrorCodes.LobbyNotMember),
				LobbyMembershipStatus.ActorNotHost => Result.Fail(ErrorCodes.LobbyNotHost),
				_ => Result.NotFound("user"),
			};
		}

		public async Task<Result<Empty>> InviteToLobby(UserRequest request)
		{
			if (RefuseOnMovingLobby() is Failure moving)
			{
				return moving;
			}
			if (RequireAccess(ESessionAccessType.Gameplay) is Failure denied)
			{
				return denied;
			}
			if (request.UserId == UserID)
			{
				return Result.Invalid("user_id");
			}

			Lobby? lobby = CurrentLobby();
			if (lobby == null)
			{
				return Result.Fail(ErrorCodes.LobbyNotMember);
			}

			Dictionary<long, (string Name, int Tag)> names;
			await using (AppDbContext db = await m_dbFactory.CreateDbContextAsync())
			{
				if (await GroupActions.CheckInviteesAsync(db, UserID, [request.UserId]) is Failure refused)
				{
					return refused;
				}
				names = await NamesAsync(db, [UserID]);
			}
			// an invisible friend gets no invite and looks offline, as to every friend
			if (GameSessions.Get(request.UserId, EUserSessionType.GameClient) == null || RealtimePresence.StatusOf(request.UserId) == PresenceStatus.Invisible)
			{
				return Result.Fail(ErrorCodes.LobbyInviteOffline);
			}

			string? error = await lobby.InviteAsync(UserID, request.UserId);
			if (error != null)
			{
				return Result.Fail(error);
			}

			LobbyInviteEntry invite = lobby.Invites[request.UserId];
			await RealtimeOut.UserGame(request.UserId).LobbyInvite(new LobbyInviteEvent
			{
				InviteId = invite.Id,
				FromUserId = UserID,
				FromName = names.TryGetValue(UserID, out var from) ? from.Name : "",
				LobbyId = lobby.LobbyID,
				LobbyName = lobby.Name,
				MapName = lobby.MapName,
				Players = lobby.NumCurrentPlayers,
				MaxPlayers = lobby.MaxPlayers,
				Anticheat = RealtimeLobbies.AnticheatOf(lobby.AnticheatID),
				ExpiresAt = invite.Expires
			});
			return Result.Success();
		}

		public Task<Result<Empty>> ForceReady(Empty request)
		{
			if (RefuseOnMovingLobby() is Failure moving)
			{
				return Task.FromResult<Result<Empty>>(moving);
			}
			return ChangeLobbyAsync(hostOnly: true, resetsReady: false, (lobby, member) =>
			{
				lobby.ForceReady();
				return Task.FromResult<Failure?>(null);
			});
		}

		public async Task<Result<Empty>> StartGame(Empty request)
		{
			if (RefuseOnMovingLobby() is Failure moving)
			{
				return moving;
			}
			if (RequireAccess(ESessionAccessType.Gameplay) is Failure denied)
			{
				return denied;
			}

			Lobby? lobby = CurrentLobby();
			if (lobby == null)
			{
				return Result.Fail(ErrorCodes.LobbyNotMember);
			}

			// the matchmaker starts quick match lobbies
			if (lobby.LobbyType == ELobbyType.QuickMatch)
			{
				return Result.Fail(ErrorCodes.LobbyAlreadyStarted);
			}

			// the mesh check and countdown run after this answer, a failed mesh check arrives as CountdownCancelled
			(Lobby.EStartResult started, IReadOnlyList<Int64> missing) = await lobby.BeginStartAsync(UserID);
			return started switch
			{
				Lobby.EStartResult.Begun => Result.Success(),
				Lobby.EStartResult.NotReady => Result.Fail(ErrorCodes.LobbyNotReady, new { Missing = missing }),
				Lobby.EStartResult.NotHost => Result.Fail(ErrorCodes.LobbyNotHost),
				Lobby.EStartResult.NotMember => Result.Fail(ErrorCodes.LobbyNotMember),
				_ => Result.Fail(ErrorCodes.LobbyAlreadyStarted),
			};
		}

		public async Task<Result<Empty>> JoinQueue(JoinQueueRequest request)
		{
			if (RequireAccess(ESessionAccessType.Gameplay) is Failure denied)
			{
				return denied;
			}
			if (RefuseWhileHandingOver() is Failure handingOver)
			{
				return handingOver;
			}

			if (request.MapIndexes == null) return Result.Invalid("map_indexes");
			if (request.PlaylistId < 0 || request.PlaylistId > UInt16.MaxValue || !MatchmakingManager.GetPlaylists().ContainsKey((UInt16)request.PlaylistId))
			{
				return Result.Fail(ErrorCodes.MatchmakingInvalidPlaylist, new { PlaylistId = request.PlaylistId });
			}

			GameSessionRegistration? build = GameSessions.Get(UserID, EUserSessionType.GameClient);
			if (build == null)
			{
				return Result.Fail(ErrorCodes.SessionNotRegistered);
			}
			// The match lobby requires GenOnline anti-cheat, so a build without one is refused before it is matched.
			if (LobbyAnticheat.CreateRefusal((EKnownAnticheatID)build.AnticheatID, false) is string anticheatRefusal)
			{
				return Result.Fail(anticheatRefusal);
			}

			UInt16 playlistID = (UInt16)request.PlaylistId;
			MatchmakingManager.ERegisterResult registered = await MatchmakingManager.RegisterPlayer(Session, playlistID, request.MapIndexes,
				GameBuild.Of(build));

			return registered switch
			{
				MatchmakingManager.ERegisterResult.Accepted => Result.Success(),
				MatchmakingManager.ERegisterResult.TooFewMaps => Result.Fail(ErrorCodes.MatchmakingTooFewMaps,
					new { Min = MatchmakingManager.GetPlaylists().TryGetValue(playlistID, out global::Playlist? playlist) ? MatchmakingManager.MinSelectedMapsOf(playlist) : 1 }),
				MatchmakingManager.ERegisterResult.AlreadyQueued => Result.Fail(ErrorCodes.MatchmakingAlreadyQueued),
				MatchmakingManager.ERegisterResult.StatsUnavailable => Result.Fail(ErrorCodes.MatchmakingStatsUnavailable),
				MatchmakingManager.ERegisterResult.SessionUnavailable => Result.NotFound("session"),
				_ => Result.Fail(ErrorCodes.MatchmakingInvalidPlaylist, new { PlaylistId = request.PlaylistId }),
			};
		}

		public async Task<Result<Empty>> LeaveQueue(Empty request)
		{
			if (RequireAccess(ESessionAccessType.Gameplay) is Failure denied)
			{
				return denied;
			}

			UserSession session = Session;
			if (!session.IsRegisteredForMatchmaking)
			{
				return Result.Fail(ErrorCodes.MatchmakingNotQueued);
			}

			await MatchmakingManager.DeregisterPlayerAsync(session);
			return Result.Success();
		}

		public Task<Result<Empty>> WidenSearch(Empty request)
		{
			if (RequireAccess(ESessionAccessType.Gameplay) is Failure denied)
			{
				return Task.FromResult<Result<Empty>>(denied);
			}

			return Task.FromResult<Result<Empty>>(MatchmakingManager.PlayerWidenSearch(Session) ? Result.Success() : Result.Fail(ErrorCodes.MatchmakingNotQueued));
		}

		public async Task SetPeerTransports(SetPeerTransportsRequest request)
		{
			if (RequireAccess(ESessionAccessType.Gameplay) != null)
			{
				return;
			}

			// a call of a connection that was superseded after it passed the check above stores nothing
			RealtimeConnection connection = Connection;
			if (connection.OwnerSession.SetPeerTransports(connection, request.Transports))
			{
				await ResendSignallingIfDueAsync(connection.OwnerSession);
			}
		}

		public Task RequestSignalling(RequestSignallingRequest request)
		{
			if (RequireAccess(ESessionAccessType.Gameplay) != null || !TryGetPeers(request.TargetUserId, out Lobby? lobby, out LobbyMember? source, out LobbyMember? target))
			{
				return Task.CompletedTask;
			}

			return lobby.RequestSignallingAsync(source.UserID, target.UserID, request.FailedAttempt, Clients.Caller);
		}

		public Task Signal(RelayRequest request)
		{
			if (RequireAccess(ESessionAccessType.Gameplay) == null && request.Payload != null && TryGetPeers(request.To, out _, out _, out LobbyMember? target))
			{
				RealtimeOut.Fire(RealtimeOut.UserGame(target.UserID).Signal(new RelayEvent { From = UserID, Payload = request.Payload }));
			}
			return Task.CompletedTask;
		}

		public async Task<Result<Empty>> BeginMeshCheck(Empty request)
		{
			if (RefuseOnMovingLobby() is Failure moving)
			{
				return moving;
			}
			if (RequireAccess(ESessionAccessType.Gameplay) is Failure denied)
			{
				return denied;
			}

			Lobby? lobby = CurrentLobby();
			if (lobby == null)
			{
				return Result.Fail(ErrorCodes.LobbyNotMember);
			}

			// the matchmaker runs the mesh check of quick match lobbies
			if (lobby.LobbyType == ELobbyType.QuickMatch || lobby.Owner != UserID)
			{
				return Result.Fail(ErrorCodes.LobbyNotHost);
			}

			if (lobby.State != ELobbyState.GAME_SETUP)
			{
				return Result.Fail(ErrorCodes.LobbyNotInSetup);
			}

			Int64 meshCheckID = await m_lobbyManager.StartMeshCheckAsync(Session, Connection);
			return meshCheckID != 0 ? Result.Success() : Result.Fail(ErrorCodes.LobbyNotInSetup);
		}

		public async Task MeshCheckResponse(MeshCheckResponseRequest request)
		{
			if (RequireAccess(ESessionAccessType.Gameplay) != null)
			{
				return;
			}

			await m_lobbyManager.StoreMeshResponseAsync(Session, Connection, request);
		}

		public Task ProbeResponse(ProbeResponseRequest request)
		{
			if (RequireAccess(ESessionAccessType.Gameplay) == null && request.ProbeId != null)
			{
				CurrentLobby()?.CompleteProbe(UserID, request.ProbeId);
			}
			return Task.CompletedTask;
		}

		public async Task ModuleScanResponse(ModuleScanResponseRequest request)
		{
			if (RequireAccess(ESessionAccessType.Gameplay) != null || request.ScanId == null)
			{
				return;
			}

			UserSession session = Session;
			UInt64 mostRecentMatchID = session.GetLatestMatchID();
			List<AcModuleReport> modulesToFlag = new();
			HashSet<string> seenModules = new(StringComparer.Ordinal);
			int droppedByCap = 0;

			foreach (ModuleInfo module in request.Modules ?? Array.Empty<ModuleInfo>())
			{
				if (module?.Path == null)
				{
					continue;
				}

				string fullModulePath = module.Path.Replace('\\', '/').ToLowerInvariant();

				// cleaned to the column limits first, so the dedupe key and the stored value agree
				string moduleFilename = global::Database.AntiCheat.CleanModuleText(System.IO.Path.GetFileName(fullModulePath), global::Database.AntiCheat.ModuleNameMaxLength);
				string modulePath = global::Database.AntiCheat.CleanModuleText(System.IO.Path.GetDirectoryName(fullModulePath), global::Database.AntiCheat.ModulePathMaxLength);
				if (moduleFilename.Length == 0 || AntiCheatModuleAllowList.IsAllowed(moduleFilename, modulePath))
				{
					continue;
				}

				if (session.IsModuleFlagged(mostRecentMatchID, moduleFilename) || !seenModules.Add(moduleFilename))
				{
					continue;
				}

				if (modulesToFlag.Count >= MaxNewModulesPerScan)
				{
					++droppedByCap;
					continue;
				}

				modulesToFlag.Add(new AcModuleReport(moduleFilename, modulePath, (int)Math.Clamp(module.Size, 0, Int32.MaxValue)));
			}

			if (droppedByCap > 0)
			{
				m_log.LogWarning("Module scan from user {UserId} exceeded the module cap, dropped {Dropped} new modules", UserID, droppedByCap);
			}

			if (modulesToFlag.Count > 0)
			{
				await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
				if (await global::Database.AntiCheat.FlagAccountForReview_Modules(db, UserID, mostRecentMatchID, modulesToFlag))
				{
					session.MarkModulesFlagged(mostRecentMatchID, modulesToFlag.Select(m => m.ModuleName));
				}
			}

			CurrentLobby()?.CompleteModuleScan(UserID, request.ScanId);
		}

		private Lobby? CurrentLobby()
		{
			Int64 lobbyID = Session.currentLobbyID;
			return lobbyID == -1 ? null : m_lobbyManager.GetLobby(lobbyID);
		}

		private bool TryGetPeers(Int64 targetUserID, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Lobby? lobby,
			[System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LobbyMember? source, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LobbyMember? target)
		{
			lobby = CurrentLobby();
			source = lobby?.GetMemberFromUserID(UserID);
			target = targetUserID == UserID ? null : lobby?.GetMemberFromUserID(targetUserID);
			return lobby != null && source != null && target != null;
		}

		private async Task<Result<Empty>> ChangeLobbyAsync(bool hostOnly, bool resetsReady, Func<Lobby, LobbyMember, Task<Failure?>> change)
		{
			if (RequireAccess(ESessionAccessType.Gameplay) is Failure denied)
			{
				return denied;
			}

			Lobby? lobby = CurrentLobby();
			if (lobby == null)
			{
				return Result.Fail(ErrorCodes.LobbyNotMember);
			}

			Int64 userID = UserID;
			Failure? failure = await lobby.RunExclusiveAsync<Failure?>(async () =>
			{
				LobbyMember? member = lobby.GetMemberFromUserID(userID);
				if (member == null)
				{
					return Result.Fail(ErrorCodes.LobbyNotMember);
				}
				if (hostOnly && lobby.Owner != userID)
				{
					return Result.Fail(ErrorCodes.LobbyNotHost);
				}
				if (lobby.State != ELobbyState.GAME_SETUP && lobby.State != ELobbyState.COUNTDOWN)
				{
					return Result.Fail(ErrorCodes.LobbyAlreadyStarted);
				}

				Failure? changeFailure = await change(lobby, member);
				if (changeFailure == null && resetsReady)
				{
					// a game term change clears ready flags and a pending start
					lobby.ResetReadyStates();
					lobby.CancelStartForSettingsChangeLocked();
				}
				return changeFailure;
			});

			return failure is Failure failed ? failed : Result.Success();
		}

		// Keeps a refused quick match request for staff review, once per player and lobby; the caller is not told
		private async Task ReportQuickMatchRuleAsync(Lobby lobby, string method, IEnumerable<int?> sides)
		{
			Int64 userID = UserID;
			if (!lobby.TryMarkQuickMatchRuleReported(userID))
			{
				return;
			}
			try
			{
				await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
				await Database.AntiCheat.FlagAccountForReview_QuickMatchRules(db, RealtimeLobbies.QuickMatchRuleReportOf(lobby, userID, method, sides, DateTime.UtcNow));
			}
			catch (Exception ex)
			{
				m_log.LogError(ex, "Keeping the refused {Method} of {UserID} in lobby {LobbyID} for review failed", method, userID, lobby.LobbyID);
			}
		}

		private static Task<Result<Empty>> Invalid(string field) => Task.FromResult<Result<Empty>>(Result.Invalid(field));

		// the one check of a lobby name, on create and rename: length and text rules, then a blocked word is refused as in a display name
		internal static Failure? LobbyNameFailure(string? name) =>
			!IsValidLobbyName(name) ? Result.Invalid("name") : TextRules.TextChecks.LobbyNameRefusal(name!.Trim());

		private static bool IsValidText(string? text, int maxLength) => text != null && text.Length <= maxLength && !text.Contains('\0');

		// A lobby name is shown to others, so it has text after trimming and no control or bidirectional override characters
		private static bool IsValidLobbyName(string? name)
		{
			string? trimmed = name?.Trim();
			return IsValidText(trimmed, LobbyNameMaxLength) && trimmed!.Length > 0
				&& !trimmed.Any(c => Char.IsControl(c) || (c >= '\u202A' && c <= '\u202E') || (c >= '\u2066' && c <= '\u2069'));
		}

		// side, color and start position are game ids, -1 means random
		// Slot fields outside the game's own ranges are refused, the client indexes arrays with them; a null team clears it
		internal static Failure? SlotFieldFailure(int? side, int? color, Optional<int?> team, int? startPosition, string fieldPrefix)
		{
			if (side is int s && !Lobby.IsValidSide(s)) return Result.Invalid(fieldPrefix + "side");
			if (color is int c && !Lobby.IsValidColor(c)) return Result.Invalid(fieldPrefix + "color");
			if (team.HasValue && team.Value is int t && (t < 0 || !Lobby.IsValidTeam(t))) return Result.Invalid(fieldPrefix + "team");
			if (startPosition is int p && !Lobby.IsValidStartPosition(p)) return Result.Invalid(fieldPrefix + "start_position");
			return null;
		}
	}
}
