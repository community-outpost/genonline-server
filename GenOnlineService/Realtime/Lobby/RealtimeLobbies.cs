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

namespace GenOnlineService
{
	public enum ELobbyJoinability
	{
		Public,
		FriendsOnly
	}
}

namespace GenOnlineService.Realtime
{
	internal static class RealtimeLobbies
	{
		// A quick match lobby whose playlist has allow_army_selection off keeps every side random (-1)
		public static Failure? RefuseArmySelection(Lobby lobby, IEnumerable<int?> sides)
		{
			if (lobby.LobbyType == ELobbyType.QuickMatch && !lobby.AllowArmySelection && sides.Any(side => side is int chosen && chosen != -1))
			{
				return Result.Fail(ErrorCodes.LobbyArmySelectionDisabled);
			}
			return null;
		}

		// The playlist sets the map, the starting cash and the superweapon limit of a quick match lobby, so no member changes them
		public static Failure? RefuseQuickMatchSettings(Lobby lobby, UpdateLobbyRequest request)
		{
			if (lobby.LobbyType == ELobbyType.QuickMatch && (request.Map != null || request.Name != null || request.Visibility != null || request.Settings?.StartingCash != null || request.Settings?.LimitSuperweapons != null
				|| request.Settings?.VanillaTeams != null || request.Settings?.AllowObservers != null))
			{
				return Result.Fail(ErrorCodes.LobbyNotHost);
			}
			return null;
		}

		// The password of a quick match lobby is none, so nobody sets one
		public static Failure? RefuseQuickMatchPassword(Lobby lobby) =>
			lobby.LobbyType == ELobbyType.QuickMatch ? Result.Fail(ErrorCodes.LobbyNotHost) : null;

		public static string RequestStateOf(Lobby lobby) => lobby.State + (lobby.LobbyType == ELobbyType.QuickMatch ? ", quick match" : ", custom game");

		public static AnticheatIllegalRequest IllegalRequestOf(Lobby lobby, Int64 userID, string request, string detail, DateTime now)
		{
			static string Clamp(string text, int max) => text.Length > max ? text[..max] : text;
			return new AnticheatIllegalRequest
			{
				UserId = userID,
				MatchId = (Int64)lobby.MatchID,
				LobbyId = lobby.LobbyID,
				Request = Clamp(request, AnticheatIllegalRequestConfiguration.RequestMaxLength),
				Detail = Clamp(detail, AnticheatIllegalRequestConfiguration.DetailMaxLength),
				State = Clamp(RequestStateOf(lobby), AnticheatIllegalRequestConfiguration.StateMaxLength),
				CreatedAt = now
			};
		}

		public static string DescribeEdit(UpdateLobbyRequest request)
		{
			List<string> parts = [];
			if (request.Map != null) parts.Add("map");
			if (request.Name != null) parts.Add("name");
			if (request.Visibility != null) parts.Add("visibility");
			if (request.Settings?.StartingCash != null) parts.Add("starting cash");
			if (request.Settings?.LimitSuperweapons != null) parts.Add("superweapon limit");
			if (request.Settings?.MaxCameraHeight != null) parts.Add("camera height");
			if (request.Settings?.VanillaTeams != null) parts.Add("vanilla teams");
			if (request.Settings?.AllowObservers != null) parts.Add("observers");
			return "lobby edit: " + String.Join(", ", parts);
		}

		public static string DescribeEdit(UpdateMySlotRequest request)
		{
			List<string> parts = [];
			if (request.Side is int side) parts.Add("side " + side);
			if (request.Color is int color) parts.Add("color " + color);
			if (request.Team.HasValue) parts.Add("team " + (request.Team.Value ?? -1));
			if (request.StartPosition is int startPosition) parts.Add("start position " + startPosition);
			return "own slot: " + String.Join(", ", parts);
		}

		public static string DescribeEdit(UpdateSlotsRequest request)
		{
			List<string> parts = [];
			if (request.Slots.Any(patch => patch.Side != null)) parts.Add("side");
			if (request.Slots.Any(patch => patch.Color != null)) parts.Add("color");
			if (request.Slots.Any(patch => patch.Team.HasValue)) parts.Add("team");
			if (request.Slots.Any(patch => patch.StartPosition != null)) parts.Add("start position");
			return "slot edit of " + request.Slots.Count + " slots: " + String.Join(", ", parts);
		}

		public static SlotState SlotStateOf(EPlayerType type) => type switch
		{
			EPlayerType.SLOT_OPEN => SlotState.Open,
			EPlayerType.SLOT_CLOSED => SlotState.Closed,
			EPlayerType.SLOT_EASY_AI => SlotState.AiEasy,
			EPlayerType.SLOT_MED_AI => SlotState.AiMedium,
			EPlayerType.SLOT_BRUTAL_AI => SlotState.AiBrutal,
			_ => SlotState.Player,
		};

		public static EPlayerType PlayerTypeOf(SlotState state) => state switch
		{
			SlotState.Open => EPlayerType.SLOT_OPEN,
			SlotState.Closed => EPlayerType.SLOT_CLOSED,
			SlotState.AiEasy => EPlayerType.SLOT_EASY_AI,
			SlotState.AiMedium => EPlayerType.SLOT_MED_AI,
			SlotState.AiBrutal => EPlayerType.SLOT_BRUTAL_AI,
			_ => EPlayerType.SLOT_PLAYER,
		};

		public static LobbyStage StageOf(ELobbyState state) => state switch
		{
			ELobbyState.COUNTDOWN => LobbyStage.Countdown,
			ELobbyState.INGAME => LobbyStage.InGame,
			ELobbyState.COMPLETE => LobbyStage.Complete,
			_ => LobbyStage.Setup,
		};

		public static AnticheatKind AnticheatOf(EKnownAnticheatID anticheatID) => anticheatID switch
		{
			EKnownAnticheatID.GO_INTEGRATED_AC => AnticheatKind.GoIntegrated,
			EKnownAnticheatID.EASY_ANTICHEAT => AnticheatKind.EasyAnticheat,
			_ => AnticheatKind.None,
		};

		// A member who is not online here, such as one of a restored lobby before it reconnects, shows the name the lobby stored
		private static Member MemberOf(LobbyMember member) => WebSocketManager.GetSharedDataForUser(member.UserID) == null
			? RealtimeMembers.Of(member.UserID, member.DisplayNameNotDeduped, null, member.NameTag)
			: RealtimeMembers.Of(member.UserID);

		public static Slot SlotOf(LobbyMember member)
		{
			bool human = member.IsHuman();
			bool ai = member.IsAI();
			return new Slot
			{
				Index = member.SlotIndex,
				State = SlotStateOf(member.SlotState),
				Member = human ? MemberOf(member) : null,
				Name = human ? member.DisplayName : null,
				Ready = (human || ai) && member.IsReady,
				HasMap = human ? member.HasMap : ai,
				Side = member.Side,
				Color = member.Color,
				Team = member.Team < 0 ? null : member.Team,
				StartPosition = member.StartingPosition,
				MutedUntil = human ? AccessControl.GetMute(member.UserID) : null
			};
		}

		public static LobbyInfo StateOf(Lobby lobby)
		{
			return new LobbyInfo
			{
				Id = lobby.LobbyID,
				Name = lobby.Name,
				HostId = lobby.Owner,
				Type = lobby.LobbyType == ELobbyType.QuickMatch ? LobbyType.QuickMatch : LobbyType.Custom,
				State = StageOf(lobby.State),
				Visibility = VisibilityOf(lobby),
				HasPassword = lobby.IsPassworded,
				Map = new MapRef { Name = lobby.MapName, Path = lobby.MapPathWire, Official = lobby.IsMapOfficial },
				Region = lobby.Region,
				Anticheat = AnticheatOf(lobby.AnticheatID),
				Settings = new LobbySettings
				{
					StartingCash = (int)lobby.StartingCash,
					LimitSuperweapons = lobby.IsLimitSuperweapons,
					VanillaTeams = lobby.IsVanillaTeamsOnly,
					TrackStats = lobby.IsTrackingStats,
					AllowObservers = lobby.AllowObservers,
					MaxCameraHeight = lobby.MaximumCameraHeight
				},
				MatchId = lobby.MatchID == 0 ? null : (long)lobby.MatchID,
				CreatedAt = lobby.TimeCreated,
				Slots = lobby.Members.Select(SlotOf).ToArray()
			};
		}

		public static LobbyVisibility VisibilityOf(Lobby lobby) => lobby.LobbyJoinability == ELobbyJoinability.FriendsOnly ? LobbyVisibility.Friends : LobbyVisibility.Public;

		// Without a viewer session (a REST caller with no hub connection) the latency estimate is 0
		public static LobbySummary SummaryOf(Lobby lobby, UserSession? viewer)
		{
			(int latencyMs, LatencySource latencySource) = viewer == null ? (0, LatencySource.Geo) : EstimateLatency(lobby, viewer);
			return new LobbySummary
			{
				Id = lobby.LobbyID,
				Name = lobby.Name,
				Host = RealtimeMembers.Of(lobby.Owner),
				State = StageOf(lobby.State),
				Map = new LobbySummaryMap { Name = lobby.MapName, Official = lobby.IsMapOfficial, Path = lobby.MapPathWire },
				Players = lobby.NumCurrentPlayers,
				MaxPlayers = lobby.MaxPlayers,
				Visibility = VisibilityOf(lobby),
				HasPassword = lobby.IsPassworded,
				Region = lobby.Region,
				LatencyMs = latencyMs,
				LatencySource = latencySource,
				Anticheat = AnticheatOf(lobby.AnticheatID),
				Settings = new LobbySummarySettings
				{
					StartingCash = (int)lobby.StartingCash,
					LimitSuperweapons = lobby.IsLimitSuperweapons,
					VanillaTeams = lobby.IsVanillaTeamsOnly,
					TrackStats = lobby.IsTrackingStats,
					AllowObservers = lobby.AllowObservers,
					MaxCameraHeight = lobby.MaximumCameraHeight
				},
				Build = new BuildIdentity { ExeCrc = lobby.ExeCRC, IniCrc = lobby.IniCRC },
				CreatedAt = lobby.TimeCreated,
				Slots = lobby.Members.Where(member => member.IsHuman() || member.IsAI()).Select(SlotOf)
					.Select(slot => new LobbySummarySlot { Index = slot.Index, State = slot.State, Member = slot.Member, Name = slot.Name }).ToArray()
			};
		}

		// joins of another build are refused at join
		public static bool IsListedFor(Lobby lobby, UserSession viewer)
		{
			Int64 viewerID = viewer.m_UserID;
			if (lobby.LobbyType != ELobbyType.CustomGame || (lobby.State != ELobbyState.GAME_SETUP && lobby.State != ELobbyState.COUNTDOWN) || lobby.GetNumberOfHumans() == 0)
			{
				return false;
			}

			if (viewerID == lobby.Owner)
			{
				return true;
			}

			// a host without a session here has its sets loaded for the lobby, the lobby stays out until they are read and is listed again then
			UserSocialContainer? social = LobbyHosts.For(lobby);
			if (social == null)
			{
				return false;
			}

			lock (social)
			{
				if (social.Blocked.Contains(viewerID))
				{
					return false;
				}

				return lobby.LobbyJoinability != ELobbyJoinability.FriendsOnly || social.Friends.Contains(viewerID);
			}
		}

		// The slowest link to any other player bounds the viewer, so the lobby reports the largest estimate
		public static (int Ms, LatencySource Source) EstimateLatency(Lobby lobby, UserSession viewer)
		{
			List<UserSession> others = new();
			foreach (LobbyMember member in lobby.Members)
			{
				if (member.SlotState == EPlayerType.SLOT_PLAYER && member.UserID != viewer.m_UserID && member.GetSession().TryGetTarget(out UserSession? session) && session != null)
				{
					others.Add(session);
				}
			}
			return EstimateLatency(viewer, others);
		}

		public static (int Ms, LatencySource Source) EstimateLatency(UserSession viewer, IEnumerable<UserSession> others)
		{
			int largest = 0;
			bool any = false;
			bool allBeacon = true;
			foreach (UserSession other in others)
			{
				int? bound = RegionLatency.BoundMs(viewer.RegionRtts, other.RegionRtts);
				allBeacon &= bound != null;
				any = true;
				largest = Math.Max(largest, bound ?? GeoLatencyMs(viewer, other));
			}
			return (largest, any && allBeacon ? LatencySource.Beacon : LatencySource.Geo);
		}

		private static int GeoLatencyMs(UserSession a, UserSession b)
		{
			double distanceKm = HaversineDistanceKm(a.m_dLatitude, a.m_dLongitude, b.m_dLatitude, b.m_dLongitude);
			return Convert.ToInt32(2.0 * (PropagationDelayMs(distanceKm) * 2.0));
		}

		private static double ToRadians(double angleInDegrees) => angleInDegrees * Math.PI / 180.0;

		private static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
		{
			const double EarthRadiusKm = 6371;
			double dLat = ToRadians(lat2 - lat1);
			double dLon = ToRadians(lon2 - lon1);
			double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
				Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
				Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
			return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
		}

		// One way delay through fiber at about 200000 km/s
		private static double PropagationDelayMs(double distanceKm) => distanceKm / 200000.0 * 1000;

		public static IRealtimeClient GameOf(LobbyMember member) =>
			RealtimeOut.Session(member.GetSession().TryGetTarget(out UserSession? session) ? session : null);

		public static string? ConnectionOf(UserSession? session) =>
			session == null ? null : WebSocketManager.GetWebSocketForSession(session)?.ConnectionId;
	}
}
