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

using GenOnlineService.TextRules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace GenOnlineService.Rest
{
	// The list ships to every client, so the route is anonymous
	[ApiController]
	[AllowAnonymous]
	public sealed class ChatFilterController : ControllerBase
	{
		[EndpointSummary("Reads the chat filter list; If-None-Match with the ETag answers 304.")]
		[ProducesResponseType<ChatFilterList>(StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status304NotModified)]
		[HttpGet("~/api/v1/chat/filter")]
		public IActionResult Get()
		{
			TextRuleSet set = TextChecks.Current;
			ChatFilterList list = set.ChatFilter;
			Response.Headers[HeaderNames.ETag] = list.Etag;
			Response.Headers[HeaderNames.CacheControl] = "public, max-age=300";

			if (ChatFilterList.EtagMatches(Request.Headers.IfNoneMatch.Select(v => (string?)v), list.Etag))
			{
				return StatusCode(StatusCodes.Status304NotModified);
			}
			return File(set.ChatFilterJson, "application/json");
		}
	}
}
