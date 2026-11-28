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

namespace GenOnlineService.Realtime
{
	public readonly record struct PeerPairState(int Attempt, int Step);

	public readonly record struct PeerCandidate(string Transport, bool Relay);

	// Transport and Relay are those of the first candidate
	public readonly record struct PeerStepValues(int Attempt, IReadOnlyList<PeerCandidate> Candidates, int TimeoutS)
	{
		public string Transport => Candidates[0].Transport;
		public bool Relay => Candidates[0].Relay;
	}

	// The connection order of a pair: direct GNS raced against a plugin transport both members share, or direct GNS alone
	// when they share none, then GNS over TURN alone
	public static class ConnectionOrder
	{
		public const string Gns = "gns";

		public const int DirectStep = 1;
		public const int RaceStep = 2;
		public const int RelayStep = 3;

		// A plain RequestSignalling this soon after a restart of the pair is ignored, so both members asking at once restart once
		public const long RequestDebounceMs = 2000;

		// A transport name is 1 to 32 characters of a-z, 0-9 and _, and at most 8 plugin names of one list count
		public const int MaxTransportNameLength = 32;
		public const int MaxPluginTransports = 8;

		public static PeerPairState First => new PeerPairState(1, DirectStep);

		public static bool IsTransportName(string? name)
		{
			if (String.IsNullOrEmpty(name) || name.Length > MaxTransportNameLength)
			{
				return false;
			}
			foreach (char c in name)
			{
				if (!(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))
				{
					return false;
				}
			}
			return true;
		}

		// A name a plugin reports, gns is reserved for the built-in transport
		public static bool IsPluginTransport(string? name) => name != Gns && IsTransportName(name);

		// Keeps the first distinct plugin names in the order sent, anything else is ignored
		public static string[] PluginTransports(IEnumerable<string?>? transports)
		{
			return (transports ?? []).Where(IsPluginTransport).Select(name => name!).Distinct(StringComparer.Ordinal).Take(MaxPluginTransports).ToArray();
		}

		// The supported provider both members listed, the first in ordinal order when they share several, null when they share none.
		// A null list means the member has not sent SetPeerTransports in its current session and counts as listing none
		public static string? SharedTransport(IReadOnlyList<string>? a, IReadOnlyList<string>? b, IEnumerable<string> supported)
		{
			if (a == null || b == null)
			{
				return null;
			}
			return a.Where(name => b.Contains(name, StringComparer.Ordinal) && supported.Contains(name, StringComparer.Ordinal)).Order(StringComparer.Ordinal).FirstOrDefault();
		}

		public static int Settle(int step, bool shared) => step == RelayStep ? RelayStep : shared ? RaceStep : DirectStep;

		// A restart counts one more attempt and moves to the TURN step only when it advances
		public static PeerPairState Restart(PeerPairState? current, bool advance, bool shared)
		{
			if (current is not PeerPairState state)
			{
				return First;
			}

			int step = Settle(state.Step, shared);
			return new PeerPairState(state.Attempt + 1, advance ? RelayStep : step);
		}

		// A RequestSignalling advances on the current attempt, restarts the same step without one, and ignores any other attempt
		public static PeerPairState? OnRequest(PeerPairState? current, int? failedAttempt, bool shared)
		{
			// a report for a pair without state names no attempt the service handed out
			if (current == null && failedAttempt != null)
			{
				return null;
			}
			if (current is not PeerPairState state || failedAttempt == null)
			{
				return Restart(current, false, shared);
			}

			return failedAttempt.Value == state.Attempt ? Restart(state, true, shared) : null;
		}

		// shared is null when the pair cannot race a plugin transport, the race lists the transport that won last first
		public static PeerStepValues ValuesOf(PeerPairState state, string? shared, string? winner)
		{
			PeerCandidate direct = new PeerCandidate(Gns, false);
			return Settle(state.Step, shared != null) switch
			{
				DirectStep => new PeerStepValues(state.Attempt, [direct], 6),
				RaceStep when winner == shared => new PeerStepValues(state.Attempt, [new PeerCandidate(shared!, false), direct], 10),
				RaceStep => new PeerStepValues(state.Attempt, [direct, new PeerCandidate(shared!, false)], 10),
				_ => new PeerStepValues(state.Attempt, [new PeerCandidate(Gns, true)], 0),
			};
		}
	}

	// The connection order of every pair of one lobby, keyed by the unordered pair and used only under the lobby gate
	public sealed class PeerPairs
	{
		private readonly Dictionary<(long Low, long High), PeerPairState> m_pairs = new();
		private readonly Dictionary<(long Low, long High), long> m_restartedAtMs = new();
		private readonly Dictionary<(long Low, long High), string> m_winners = new();

		private static (long Low, long High) Key(long userA, long userB) => (Math.Min(userA, userB), Math.Max(userA, userB));

		public PeerPairState Current(long userA, long userB)
		{
			(long Low, long High) key = Key(userA, userB);
			if (!m_pairs.TryGetValue(key, out PeerPairState state))
			{
				state = ConnectionOrder.First;
				m_pairs[key] = state;
			}
			return state;
		}

		public PeerPairState Restart(long userA, long userB, bool advance, bool shared, long nowMs)
		{
			(long Low, long High) key = Key(userA, userB);
			PeerPairState next = ConnectionOrder.Restart(m_pairs.TryGetValue(key, out PeerPairState state) ? state : null, advance, shared);
			m_pairs[key] = next;
			m_restartedAtMs[key] = nowMs;
			return next;
		}

		// Null when the report changes nothing
		public PeerPairState? Request(long userA, long userB, int? failedAttempt, bool shared, long nowMs)
		{
			(long Low, long High) key = Key(userA, userB);
			if (failedAttempt == null && m_restartedAtMs.TryGetValue(key, out long restartedAtMs) && nowMs - restartedAtMs < ConnectionOrder.RequestDebounceMs)
			{
				return null;
			}
			PeerPairState? next = ConnectionOrder.OnRequest(m_pairs.TryGetValue(key, out PeerPairState state) ? state : null, failedAttempt, shared);
			if (next is PeerPairState restarted)
			{
				m_pairs[key] = restarted;
				m_restartedAtMs[key] = nowMs;
			}
			return next;
		}

		// The transport a member reported its link to the other member on, the next race lists it first
		public void SetWinner(long userA, long userB, string transport) => m_winners[Key(userA, userB)] = transport;

		public string? Winner(long userA, long userB) => m_winners.TryGetValue(Key(userA, userB), out string? transport) ? transport : null;

		// Stores the step the pair is sent on, so a pair that fell back to step 3 stays there
		public PeerPairState Settle(long userA, long userB, bool shared)
		{
			PeerPairState state = Current(userA, userB);
			int step = ConnectionOrder.Settle(state.Step, shared);
			if (step != state.Step)
			{
				state = state with { Step = step };
				m_pairs[Key(userA, userB)] = state;
			}
			return state;
		}

		public IEnumerable<(long Low, long High, PeerPairState? State, string? Winner)> All() =>
			m_pairs.Keys.Union(m_winners.Keys).Select(key => (key.Low, key.High, m_pairs.TryGetValue(key, out PeerPairState state) ? state : (PeerPairState?)null, Winner(key.Low, key.High))).ToList();

		public void Restore(long userA, long userB, PeerPairState? state, string? winner)
		{
			if (state is PeerPairState restored)
			{
				m_pairs[Key(userA, userB)] = restored;
			}
			if (winner != null)
			{
				SetWinner(userA, userB, winner);
			}
		}

		// A member who leaves or joins starts each of its pairs over
		public void Forget(long userID)
		{
			foreach ((long Low, long High) key in m_pairs.Keys.Concat(m_restartedAtMs.Keys).Concat(m_winners.Keys).Where(key => key.Low == userID || key.High == userID).ToList())
			{
				m_pairs.Remove(key);
				m_restartedAtMs.Remove(key);
				m_winners.Remove(key);
			}
		}
	}
}
