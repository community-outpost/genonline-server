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

namespace GenOnlineService
{
	// The deploy environment this service runs as, a name the operator picks (live, test, staging, eu-prod, ...).
	// It is separate from the ASP.NET environment, which only switches framework behaviour, so a live and a test pod can both run as Production.
	public sealed class ServiceEnvironment
	{
		public const string ConfigSection = "Service";
		public const string ConfigKey = "environment";

		public const string Dev = "dev";

		// a label for flag targeting and Sentry: lower case letters, digits and dashes, starting with a letter
		private static readonly System.Text.RegularExpressions.Regex s_name =
			new System.Text.RegularExpressions.Regex("^[a-z][a-z0-9-]{0,31}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

		public string Name { get; }

		private ServiceEnvironment(string name)
		{
			Name = name;
		}

		// An unset value is dev on an ASP.NET Development host and stops the start everywhere else, so a live pod never reports itself as test or dev by accident
		public static ServiceEnvironment Read(IConfiguration config, bool isDevelopment)
		{
			string? value = config.GetSection(ConfigSection)[ConfigKey];
			if (string.IsNullOrWhiteSpace(value))
			{
				if (isDevelopment)
				{
					return new ServiceEnvironment(Dev);
				}
				throw new InvalidOperationException($"{ConfigSection}:{ConfigKey} is not set. Set it to the name of this deploy environment, for example live or test.");
			}
			if (!s_name.IsMatch(value))
			{
				throw new InvalidOperationException($"{ConfigSection}:{ConfigKey} is `{value}`. It must be 1 to 32 lower case letters, digits and dashes, starting with a letter.");
			}
			return new ServiceEnvironment(value);
		}

		public static ServiceEnvironment Configure(IConfiguration config, IHostEnvironment environment)
		{
			return Read(config, environment.IsDevelopment());
		}

		public override string ToString() => Name;
	}
}
