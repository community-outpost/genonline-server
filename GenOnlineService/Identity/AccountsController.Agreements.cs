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

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Net;
using System.Text;

namespace GenOnlineService.Identity
{
	// Nothing is recorded until the last step's post, which carries both steps.
	public sealed partial class AccountsController
	{
		public const string AgreementsPath = "/users/agreements";

		private const string AgreementStepField = "step";
		private const string AgreementRulesField = "rules";
		private const string AgreementTermsField = "agree_terms";
		private const string RulesStep = "rules";
		// step of the old one-card-at-a-time page, still posted by pages opened before it went
		private const string LegacyCardStep = "card";
		private const string TermsStep = "terms";

		private const string AgreementSource = "web";

		private static string AgreementsUrl(string target) => $"{AgreementsPath}?{ReturnUrlField}={Uri.EscapeDataString(target)}";

		private static string TermsStepUrl(string target, string rules) => $"{AgreementsUrl(target)}&{AgreementStepField}={TermsStep}&{AgreementRulesField}={Uri.EscapeDataString(rules)}";

		private async Task<string> AfterName(Int64 userID, string target)
			=> (await OwedAgreements(userID)).Count > 0 ? AgreementsUrl(target) : target;

		private async Task AcceptRequiredAgreements(Int64 userID)
		{
			foreach (AgreementDocument document in AgreementSettings.Required)
			{
				await Agreements.AcceptAsync(DbFactory, AgreementSettings, userID, document.Name, document.Version!, AgreementSource);
			}
		}

		private AgreementsSettings AgreementSettings => m_settings.Agreements;

		private IDbContextFactory<AppDbContext> DbFactory => HttpContext.RequestServices.GetRequiredService<IDbContextFactory<AppDbContext>>();

		[HttpGet("~" + AgreementsPath)]
		public async Task<IActionResult> AgreementsPage([FromQuery(Name = ReturnUrlField)] string? returnUrl, [FromQuery(Name = AgreementStepField)] string? step,
			[FromQuery(Name = AgreementRulesField)] string? rules)
		{
			string target = SafeReturnUrl(returnUrl);
			User? user = await CookieUser();
			if (user == null)
			{
				return SignInFirst(target);
			}
			IReadOnlyList<AgreementDocument> owed = await OwedAgreements(user.ID);
			if (owed.Count == 0)
			{
				return LocalRedirect(target);
			}

			AgreementDocument? rulesOwed = owed.FirstOrDefault(d => d.Name == AgreementsSettings.Rules);
			AgreementDocument? termsOwed = owed.FirstOrDefault(d => d.Name == AgreementsSettings.Terms);
			if (termsOwed != null && (rulesOwed == null || (step == TermsStep && rules == rulesOwed.Version)))
			{
				return await TermsForm(200, user.ID, target, termsOwed, rulesOwed?.Version, null);
			}
			return await RulesForm(user.ID, target, rulesOwed!);
		}

		[HttpPost("~" + AgreementsPath)]
		public async Task<IActionResult> Agree([FromForm(Name = ReturnUrlField)] string? returnUrl, [FromForm(Name = AgreementStepField)] string? step,
			[FromForm(Name = AgreementRulesField)] string? rules, [FromForm(Name = AgreementTermsField)] string? agreeTerms)
		{
			if (!await ValidForm())
			{
				return Expired();
			}
			string target = SafeReturnUrl(returnUrl);
			User? user = await CookieUser();
			if (user == null)
			{
				return SignInFirst(target);
			}
			IReadOnlyList<AgreementDocument> owed = await OwedAgreements(user.ID);
			if (owed.Count == 0)
			{
				return LocalRedirect(target);
			}
			AgreementDocument? rulesOwed = owed.FirstOrDefault(d => d.Name == AgreementsSettings.Rules);
			AgreementDocument? termsOwed = owed.FirstOrDefault(d => d.Name == AgreementsSettings.Terms);

			if (rulesOwed != null && rules != rulesOwed.Version)
			{
				return LocalRedirect(AgreementsUrl(target));
			}

			if ((step == RulesStep || step == LegacyCardStep) && rulesOwed != null)
			{
				if (termsOwed != null)
				{
					return LocalRedirect(TermsStepUrl(target, rulesOwed.Version!));
				}
				await Agreements.AcceptAsync(DbFactory, AgreementSettings, user.ID, rulesOwed.Name, rulesOwed.Version!, AgreementSource);
				return LocalRedirect(target);
			}

			if (step != TermsStep || termsOwed == null)
			{
				return LocalRedirect(AgreementsUrl(target));
			}
			if (String.IsNullOrEmpty(agreeTerms))
			{
				return await TermsForm(400, user.ID, target, termsOwed, rulesOwed?.Version, T("Agreements.Terms.Required"));
			}

			if (rulesOwed != null)
			{
				await Agreements.AcceptAsync(DbFactory, AgreementSettings, user.ID, rulesOwed.Name, rulesOwed.Version!, AgreementSource);
			}
			await Agreements.AcceptAsync(DbFactory, AgreementSettings, user.ID, termsOwed.Name, termsOwed.Version!, AgreementSource);
			return LocalRedirect(target);
		}

		private async Task<string> ChangedNotice(Int64 userID, AgreementDocument document, string withDate, string withoutDate)
		{
			bool earlier;
			await using (AppDbContext db = await DbFactory.CreateDbContextAsync(HttpContext.RequestAborted))
			{
				earlier = (await Agreements.LatestAsync(db, userID, HttpContext.RequestAborted)).ContainsKey(document.Name);
			}
			if (!earlier)
			{
				return String.Empty;
			}
			string text = document.Date is DateOnly date ? T(withDate, Day(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))) : T(withoutDate);
			return $"<div role=\"status\" class=\"gp-alert gp-alert-info\">{PortalLook.Icon("info")}<span>{WebUtility.HtmlEncode(text)}</span></div>";
		}

		private static string ExternalLink(string href, string text)
			=> $"<a href=\"{WebUtility.HtmlEncode(href)}\" target=\"_blank\" rel=\"noopener noreferrer\">{WebUtility.HtmlEncode(text)}</a>";

		private static string RuleIcon(string id) => id switch
		{
			"respect" => "users",
			"chat" => "message-square",
			"fair_play" => "shield-check",
			"match" => "swords",
			"accounts" => "id-card",
			_ => "triangle-alert"
		};

		private async Task<ContentResult> RulesForm(Int64 userID, string target, AgreementDocument rules)
		{
			var body = new StringBuilder();
			body.Append(await ChangedNotice(userID, rules, "Agreements.RulesChanged", "Agreements.RulesChangedNoDate"));
			foreach ((string id, int items) in Agreements.RuleCards)
			{
				body.Append($"<section class=\"gp-rule-group\"><h2 class=\"gp-rule-title\">{PortalLook.Icon(RuleIcon(id))}<span>{WebUtility.HtmlEncode(T($"Rules.{id}.Title"))}</span></h2>");
				body.Append($"<p class=\"gp-plain\">{WebUtility.HtmlEncode(T($"Rules.{id}.Intro"))}</p>");
				body.Append("<ul class=\"gp-rules\">");
				for (int item = 1; item <= items; item++)
				{
					body.Append($"<li>{PortalLook.Icon("check")}<span>{WebUtility.HtmlEncode(T($"Rules.{id}.{item.ToString(CultureInfo.InvariantCulture)}"))}</span></li>");
				}
				body.Append("</ul></section>");
			}
			body.Append(PortalLook.Muted(ExternalLink(rules.Url!, T("Agreements.ReadRules"))));
			body.Append(FormStart(AgreementsPath, target));
			body.Append(Hidden(AgreementStepField, RulesStep)).Append(Hidden(AgreementRulesField, rules.Version));
			body.Append(PortalLook.Actions(null, PortalLook.Submit(T("Agreements.Agree"), narrow: true, large: true, fill: true))).Append("</form>");
			return Page(200, T("Agreements.Rules.Title"), body.ToString(), T("Agreements.Rules.Description"), icon: "shield-check", step: 2);
		}

		private async Task<ContentResult> TermsForm(int status, Int64 userID, string target, AgreementDocument terms, string? rulesVersion, string? message)
		{
			var body = new StringBuilder(Message(message));
			body.Append(await ChangedNotice(userID, terms, "Agreements.TermsChanged", "Agreements.TermsChangedNoDate"));
			body.Append(PortalLook.Text(WebUtility.HtmlEncode(T("Agreements.Terms.Intro"))));
			string links = ExternalLink(terms.Url!, T("Agreements.TermsLink"));
			if (AgreementSettings.PrivacyUrl != null)
			{
				links += " · " + ExternalLink(AgreementSettings.PrivacyUrl, T("Agreements.PrivacyLink"));
			}
			body.Append(PortalLook.Muted(links));
			body.Append(FormStart(AgreementsPath, target));
			body.Append(Hidden(AgreementStepField, TermsStep));
			if (rulesVersion != null)
			{
				body.Append(Hidden(AgreementRulesField, rulesVersion));
			}
			string invalid = message == null ? String.Empty : " aria-invalid=\"true\"";
			body.Append($"<label class=\"gp-check\"><input type=\"checkbox\" name=\"{AgreementTermsField}\" value=\"1\" required{invalid}><span class=\"gp-check-text\"><strong>{WebUtility.HtmlEncode(T("Agreements.Terms.Tick"))}</strong></span></label>");
			string? back = rulesVersion == null ? null : PortalLook.GhostLink(AgreementsUrl(target), T("Back"));
			body.Append(PortalLook.Actions(back, PortalLook.Submit(T("Agreements.Start"), narrow: true, large: true, fill: true))).Append("</form>");
			return Page(status, T("Agreements.Terms.Title"), body.ToString(), T("Agreements.Terms.Description"), step: 3);
		}
	}
}
