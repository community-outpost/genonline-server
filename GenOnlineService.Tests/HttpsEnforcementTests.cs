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
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Server.AspNetCore;
using Xunit;

namespace GenOnlineService.Tests;

// Core:enforce_https: one value for the accounts server and the pipeline, true when missing
public class HttpsEnforcementTests
{
	private static IConfiguration Config(string? enforceHttps)
	{
		Dictionary<string, string?> values = new() { ["Accounts:ephemeral_keys"] = "true" };
		if (enforceHttps != null)
		{
			values["Core:enforce_https"] = enforceHttps;
		}
		return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
	}

	[Theory]
	[InlineData(null, true)]
	[InlineData("false", false)]
	public void CookiesAndTransportSecurityFollowTheKey(string? value, bool enforced)
	{
		ServiceCollection services = new();
		services.AddLogging();
		services.AddGenOnlineIdentity(AccountsSettings.Read(Config(value)), development: true);
		using ServiceProvider provider = services.BuildServiceProvider();

		CookieSecurePolicy expected = enforced ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
		Assert.Equal(expected, provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme).Cookie.SecurePolicy);
		Assert.Equal(expected, provider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.Cookie.SecurePolicy);
		Assert.Equal(!enforced, provider.GetRequiredService<IOptionsMonitor<OpenIddictServerAspNetCoreOptions>>().CurrentValue.DisableTransportSecurityRequirement);
	}

	private static async Task<HttpContext> Send(RequestDelegate pipeline, string scheme)
	{
		DefaultHttpContext context = new();
		context.Request.Scheme = scheme;
		context.Request.Host = new HostString("game.example");
		context.Request.Path = "/api/v1/me";
		await pipeline(context);
		return context;
	}

	// An expected outage is one Warning line without the exception; anything else keeps the full Error
}

internal sealed class ListLogger : ILogger
{
	public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

	public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

	public bool IsEnabled(LogLevel logLevel) => true;

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		lock (Entries)
		{
			Entries.Add((logLevel, formatter(state, exception), exception));
		}
	}
}
