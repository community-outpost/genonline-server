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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Net.Http.Headers;
using System.Globalization;
using System.Resources;

namespace GenOnlineService.Identity
{
	// English is the neutral resource (AccountsText.resx); de, ru and ar are satellites.
	// The staff pages (/admin) never come here.
	public static class AccountsText
	{
		public static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

		public static readonly IReadOnlyList<CultureInfo> Supported =
		[
			English,
			CultureInfo.GetCultureInfo("de"),
			CultureInfo.GetCultureInfo("ru"),
			CultureInfo.GetCultureInfo("ar")
		];

		// it wins over the browser's Accept-Language
		public const string CookieName = "go_culture";

		private static readonly ResourceManager s_resources = new("GenOnlineService.Identity.AccountsText", typeof(AccountsText).Assembly);

		public static ResourceManager Resources => s_resources;

		public static CultureInfo Resolve(CultureInfo? culture) => SupportedLanguageOf(culture) ?? English;

		private static CultureInfo? SupportedLanguageOf(CultureInfo? culture)
		{
			for (CultureInfo? current = culture; current != null && !current.Equals(CultureInfo.InvariantCulture); current = current.Parent)
			{
				CultureInfo? match = Supported.FirstOrDefault(s => s.Name == current.Name);
				if (match != null)
				{
					return match;
				}
			}
			return null;
		}

		public static string Get(string key, CultureInfo? culture = null)
			=> s_resources.GetString(key, Resolve(culture)) ?? throw new KeyNotFoundException($"No accounts text named {key}.");

		public static string Format(string key, CultureInfo? culture, params object[] values)
		{
			CultureInfo resolved = Resolve(culture);
			return String.Format(resolved, Get(key, resolved), values);
		}

		// ISO 8601 in English, else the language's short date on the Gregorian calendar (Arabic defaults to Umm al-Qura)
		public static string Day(DateTime value, CultureInfo? culture)
		{
			CultureInfo resolved = Resolve(culture);
			DateTime utc = value.ToUniversalTime();
			if (resolved.Name == English.Name)
			{
				return utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
			}
			var format = (DateTimeFormatInfo)resolved.DateTimeFormat.Clone();
			if (format.Calendar is not GregorianCalendar)
			{
				format.Calendar = resolved.OptionalCalendars.OfType<GregorianCalendar>().FirstOrDefault() ?? new GregorianCalendar();
			}
			return utc.ToString(format.ShortDatePattern, format);
		}

		public static bool IsRightToLeft(CultureInfo culture) => culture.TextInfo.IsRightToLeft;

		// fixed here because ICU's names differ in case between versions
		public static string NativeName(CultureInfo culture) => Resolve(culture).Name switch
		{
			"de" => "Deutsch",
			"ru" => "Русский",
			"ar" => "العربية",
			_ => "English"
		};

		public static string CookieValue(CultureInfo culture) => CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture));

		// Passed explicitly, never set on the thread, so the API, realtime hub and staff pages keep the invariant culture.
		public static CultureInfo CultureOf(HttpRequest request)
		{
			string? cookie = request.Cookies[CookieName];
			ProviderCultureResult? chosen = cookie == null ? null : CookieRequestCultureProvider.ParseCookieValue(cookie);
			foreach (Microsoft.Extensions.Primitives.StringSegment name in chosen?.UICultures ?? [])
			{
				if (Match(name.Value) is CultureInfo fromCookie)
				{
					return fromCookie;
				}
			}

			IList<StringWithQualityHeaderValue> accepted = request.GetTypedHeaders().AcceptLanguage;
			foreach (StringWithQualityHeaderValue language in accepted.Where(l => (l.Quality ?? 1) > 0).OrderByDescending(l => l.Quality ?? 1))
			{
				if (Match(language.Value.Value) is CultureInfo fromBrowser)
				{
					return fromBrowser;
				}
			}
			return English;
		}

		private static CultureInfo? Match(string? name)
		{
			if (String.IsNullOrWhiteSpace(name) || name == "*")
			{
				return null;
			}
			try
			{
				return SupportedLanguageOf(CultureInfo.GetCultureInfo(name));
			}
			catch (CultureNotFoundException)
			{
				return null;
			}
		}
	}
}
