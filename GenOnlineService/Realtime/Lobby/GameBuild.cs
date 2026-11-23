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
using GenOnlineService.Realtime;

namespace GenOnlineService
{
	// The build a player runs: game, executable and INI CRC and anticheat, players only meet players of the same build
	public readonly record struct GameBuild(string Game, UInt32 ExeCRC, UInt32 IniCRC, EKnownAnticheatID AnticheatID)
	{
		// The game comes from the client registry row of the registered session, never from the client id
		public static GameBuild Of(GameSessionRegistration registration) =>
			new GameBuild(registration.Game, registration.ExeCrc ?? 0, registration.IniCrc ?? 0, (EKnownAnticheatID)registration.AnticheatID);
	}

	// A lobby requires Easy Anti-Cheat or the integrated anti-cheat, every client build runs one of them
	public static class LobbyAnticheat
	{
		public static EKnownAnticheatID RequirementOf(bool requireEasyAnticheat) =>
			requireEasyAnticheat ? EKnownAnticheatID.EASY_ANTICHEAT : EKnownAnticheatID.GO_INTEGRATED_AC;

		public static string? CreateRefusal(EKnownAnticheatID hostAnticheat, bool requireEasyAnticheat)
		{
			if (hostAnticheat != EKnownAnticheatID.GO_INTEGRATED_AC && hostAnticheat != EKnownAnticheatID.EASY_ANTICHEAT)
				return ErrorCodes.LobbyAnticheatRequired;
			return requireEasyAnticheat && hostAnticheat != EKnownAnticheatID.EASY_ANTICHEAT ? ErrorCodes.LobbyEasyAnticheatUnavailable : null;
		}

		public static bool Admits(EKnownAnticheatID lobbyRequirement, EKnownAnticheatID build) =>
			lobbyRequirement == EKnownAnticheatID.EASY_ANTICHEAT
				? build == EKnownAnticheatID.EASY_ANTICHEAT
				: build == EKnownAnticheatID.GO_INTEGRATED_AC || build == EKnownAnticheatID.EASY_ANTICHEAT;
	}
}
