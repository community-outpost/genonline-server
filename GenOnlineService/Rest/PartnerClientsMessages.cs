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

using System.Text.Json.Serialization;

namespace GenOnlineService.Rest
{
	// An OAuth client of a partner, whose type is public or confidential and whose first_party is false when it shows the consent page
	public sealed record PartnerClientInfo
	{
		public string ClientId { get; init; } = "";
		public string Name { get; init; } = "";
		public string Kind { get; init; } = "";
		public string Type { get; init; } = "";
		public bool FirstParty { get; init; }
		public bool Enabled { get; init; }
		public IReadOnlyList<string> Scopes { get; init; } = [];
		public IReadOnlyList<string> RedirectUris { get; init; } = [];
		public DateTime CreatedAt { get; init; }
		public DateTime UpdatedAt { get; init; }
	}

	public sealed record PartnerClientsResponse
	{
		public IReadOnlyList<PartnerClientInfo> Clients { get; init; } = [];
	}

	// The client secret is set only for a confidential client that was just created or rotated, and is never shown again
	public sealed record SavedPartnerClient
	{
		public PartnerClientInfo Client { get; init; } = new PartnerClientInfo();
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? ClientSecret { get; init; }
	}

	// A partner's client is always an app, and its scopes default to the app's own
	public sealed record CreatePartnerClientRequest
	{
		public string? ClientId { get; init; }
		public string? Name { get; init; }
		public string? Type { get; init; }
		public bool? ThirdParty { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public IReadOnlyList<string>? Scopes { get; init; }
		public IReadOnlyList<string>? RedirectUris { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Reason { get; init; }
	}

	// Each part is optional, but the type of a client is fixed
	public sealed record UpdatePartnerClientRequest
	{
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Name { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public IReadOnlyList<string>? RedirectUris { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public IReadOnlyList<string>? Scopes { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public bool? ThirdParty { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public bool? Enabled { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public bool? RotateSecret { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Reason { get; init; }
	}
}
