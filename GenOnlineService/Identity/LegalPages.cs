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

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace GenOnlineService.Identity
{
	// HTML comments in a fragment are operator notes (open facts) and never served.
	public static partial class LegalPages
	{
		public const string Rules = "rules";
		public const string Terms = "terms";
		public const string Privacy = "privacy";

		public const string RulesPath = "/legal/" + Rules;
		public const string TermsPath = "/legal/" + Terms;
		public const string PrivacyPath = "/legal/" + Privacy;

		private static readonly ConcurrentDictionary<string, string> s_fragments = new(StringComparer.Ordinal);

		[GeneratedRegex("<!--.*?-->\\s*", RegexOptions.Singleline)]
		private static partial Regex Comment();

		public static string Fragment(string name) => s_fragments.GetOrAdd(name, static n =>
		{
			using Stream stream = typeof(LegalPages).Assembly.GetManifestResourceStream($"GenOnlineService.Identity.Legal.{n}.html")
				?? throw new InvalidOperationException($"The built-in page {n} is not embedded.");
			using var reader = new StreamReader(stream);
			return Comment().Replace(reader.ReadToEnd(), String.Empty).Trim();
		});

		public static string Body(string name, AgreementDocument? document)
		{
			string meta = Meta(document);
			return $"<div class=\"gp-doc\" lang=\"en\" dir=\"ltr\">{(meta.Length == 0 ? String.Empty : $"<p class=\"gp-doc-meta\">{WebUtility.HtmlEncode(meta)}</p>")}{Fragment(name)}</div>";
		}

		private static string Meta(AgreementDocument? document)
		{
			string? day = document?.Date?.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
			return (document?.Version, day) switch
			{
				(string version, string date) => $"Version {version}, in effect from {date}",
				(string version, null) => $"Version {version}",
				(null, string date) => $"In effect from {date}",
				_ => String.Empty
			};
		}
	}
}
