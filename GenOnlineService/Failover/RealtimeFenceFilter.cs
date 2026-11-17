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

using GenOnlineService.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace GenOnlineService.Failover
{
	public sealed class RealtimeFenceFilter : IHubFilter
	{
		private readonly Func<bool> m_fenced;

		public RealtimeFenceFilter(Func<bool>? fenced = null)
		{
			m_fenced = fenced ?? (() => RealtimeFailover.Fenced);
		}

		public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
		{
			if (!m_fenced())
			{
				return await next(invocationContext);
			}

			await CloseAsync(invocationContext.Context);
			return null;
		}

		public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
		{
			if (!m_fenced())
			{
				await next(context);
				return;
			}

			HubConnectionContext? transport = RealtimeLifetimeManager.Get(context.Context.ConnectionId);
			if (transport != null)
			{
				await RealtimeAdmission.CloseAsync(transport, RealtimeAdmission.Standby());
				return;
			}
			context.Context.Abort();
		}

		private static async Task CloseAsync(HubCallerContext caller)
		{
			if (caller.Items.TryGetValue(RealtimeHub.ConnectionItem, out object? item) && item is RealtimeConnection connection)
			{
				await connection.CloseAsync(CloseCodes.ServerRestarting, allowReconnect: true);
				return;
			}

			HubConnectionContext? transport = RealtimeLifetimeManager.Get(caller.ConnectionId);
			if (transport != null)
			{
				await RealtimeHub.RealtimeConnectionClose(transport, CloseCodes.ServerRestarting, allowReconnect: true);
				return;
			}
			caller.Abort();
		}
	}
}
