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

namespace GenOnlineService.Investigation
{
	public static class MacAddresses
	{
		public const string Marker = "ZZ";

		public static string? DigitsOf(string? text)
		{
			if (String.IsNullOrWhiteSpace(text))
			{
				return null;
			}

			string value = text.Trim().ToUpperInvariant();
			if (value.EndsWith(Marker, StringComparison.Ordinal))
			{
				value = value.Substring(0, value.Length - Marker.Length);
			}

			var digits = new System.Text.StringBuilder(12);
			foreach (char c in value)
			{
				if (c is ':' or '-' or '.' or ' ')
				{
					continue;
				}
				if (!Uri.IsHexDigit(c))
				{
					return null;
				}
				digits.Append(c);
			}
			return digits.Length == 12 ? digits.ToString() : null;
		}

		public static string Format(string digits) => String.Join(":", Enumerable.Range(0, 6).Select(i => digits.Substring(i * 2, 2)));

		// The U/L bit of the first octet: virtual adapters and randomized addresses set it, vendors never assign it
		public static bool IsLocallyAdministered(string digits) => (Int32.Parse(digits.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) & 0x02) != 0;
	}

	public sealed class OuiRegistry
	{
		public const string DefaultPath = "data/oui.tsv";
		public const string LocallyAdministered = "locally administered / virtual";

		private static readonly int[] s_prefixLengths = [9, 7, 6];

		private static readonly ILogger s_log = AppLog.For(typeof(OuiRegistry));
		private static readonly Lazy<OuiRegistry> s_default = new(() => Load(DefaultPath));

		private readonly Dictionary<string, string> m_vendors;

		private OuiRegistry(Dictionary<string, string> vendors)
		{
			m_vendors = vendors;
		}

		public static OuiRegistry Default => s_default.Value;

		public int Count => m_vendors.Count;

		public static OuiRegistry Load(string path)
		{
			if (!File.Exists(path))
			{
				s_log.LogWarning("The OUI registry {Path} is missing; MAC vendors are unknown", path);
				return new OuiRegistry(new Dictionary<string, string>(StringComparer.Ordinal));
			}
			return Parse(File.ReadLines(path));
		}

		public static OuiRegistry Parse(IEnumerable<string> lines)
		{
			var vendors = new Dictionary<string, string>(StringComparer.Ordinal);
			foreach (string line in lines)
			{
				if (line.Length == 0 || line[0] == '#')
				{
					continue;
				}
				int tab = line.IndexOf('\t');
				if (tab < 1)
				{
					continue;
				}
				string prefix = line.Substring(0, tab).Trim().ToUpperInvariant();
				string vendor = line.Substring(tab + 1).Trim();
				if (s_prefixLengths.Contains(prefix.Length) && prefix.All(Uri.IsHexDigit) && vendor.Length > 0)
				{
					vendors[prefix] = vendor;
				}
			}
			return new OuiRegistry(vendors);
		}

		public string? VendorOf(string? macAddress)
		{
			string? digits = MacAddresses.DigitsOf(macAddress);
			if (digits == null)
			{
				return null;
			}
			if (MacAddresses.IsLocallyAdministered(digits))
			{
				return LocallyAdministered;
			}
			foreach (int length in s_prefixLengths)
			{
				if (m_vendors.TryGetValue(digits.Substring(0, length), out string? vendor))
				{
					return vendor;
				}
			}
			return null;
		}
	}
}
