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

using System.Globalization;

namespace GenOnlineService.Identity
{
	// Without a version the document is not required and nobody is asked to agree.
	public sealed class AgreementDocument
	{
		public const int VersionMaxLength = 32;

		public string Name { get; init; } = String.Empty;
		public string? Version { get; init; }
		public DateOnly? Date { get; init; }
		public string? Url { get; init; }
		public bool BuiltIn { get; init; }

		public bool Required => Version != null;
	}

	// Neither version configured means nothing is gated (self-hosters, tests).
	// A wording-only fix keeps the version; a new version asks every player again at their next sign-in.
	public sealed class AgreementsSettings
	{
		public const string Rules = "rules";
		public const string Terms = "terms";

		public static readonly IReadOnlyList<string> DocumentNames = [Rules, Terms];

		public AgreementDocument RulesDocument { get; init; } = new() { Name = Rules };
		public AgreementDocument TermsDocument { get; init; } = new() { Name = Terms };
		public string? PrivacyUrl { get; init; }

		public IEnumerable<AgreementDocument> Documents => [RulesDocument, TermsDocument];

		public IEnumerable<AgreementDocument> Required => Documents.Where(d => d.Required);

		public bool Any => Required.Any();

		public AgreementDocument? Find(string? name) => Documents.FirstOrDefault(d => d.Name == name);

		// issuer: Accounts:issuer. With it built-in links are absolute so the game client can open them; otherwise they are relative.
		public static AgreementsSettings Read(IConfigurationSection section, string? issuer = null)
		{
			string? privacy = Url(section.GetSection("privacy"), "Agreements:privacy:url");
			return new AgreementsSettings
			{
				RulesDocument = ReadDocument(section, Rules, BuiltInLink(issuer, LegalPages.RulesPath)),
				TermsDocument = ReadDocument(section, Terms, BuiltInLink(issuer, LegalPages.TermsPath)),
				PrivacyUrl = privacy ?? BuiltInLink(issuer, LegalPages.PrivacyPath)
			};
		}

		private static string BuiltInLink(string? issuer, string path)
		{
			string value = (issuer ?? String.Empty).Trim();
			return Uri.TryCreate(value, UriKind.Absolute, out Uri? parsed) && (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp)
				&& String.IsNullOrEmpty(parsed.Query) && String.IsNullOrEmpty(parsed.Fragment)
				? value.TrimEnd('/') + path
				: path;
		}

		private static AgreementDocument ReadDocument(IConfigurationSection section, string name, string builtIn)
		{
			IConfigurationSection document = section.GetSection(name);
			string? version = Blank(document.GetValue<string>("version"));
			string? url = Url(document, $"Agreements:{name}:url");
			string? dateText = Blank(document.GetValue<string>("date"));

			DateOnly? date = null;
			if (dateText != null)
			{
				if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly parsed))
				{
					throw new InvalidOperationException($"Agreements:{name}:date must be a day as yyyy-MM-dd, such as 2026-11-18.");
				}
				date = parsed;
			}
			if (version != null)
			{
				if (version.Length > AgreementDocument.VersionMaxLength)
				{
					throw new InvalidOperationException($"Agreements:{name}:version must be at most {AgreementDocument.VersionMaxLength} characters.");
				}
			}

			return new AgreementDocument { Name = name, Version = version, Date = date, Url = url ?? builtIn, BuiltIn = url == null };
		}

		private static string? Url(IConfigurationSection section, string key)
		{
			string? url = Blank(section.GetValue<string>("url"));
			if (url != null && (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed) || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
				|| String.IsNullOrEmpty(parsed.Host) || url.Any(c => Char.IsWhiteSpace(c) || c == '"' || c == '\'')))
			{
				throw new InvalidOperationException($"{key} must be an absolute http or https URL without spaces or quotes, such as https://example.org/rules.");
			}
			return url;
		}

		private static string? Blank(string? value) => String.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}
}
