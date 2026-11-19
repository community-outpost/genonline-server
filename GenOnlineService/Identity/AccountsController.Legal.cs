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

namespace GenOnlineService.Identity
{
	// Served even when Agreements:<doc>:url points elsewhere; the links then go there.
	public sealed partial class AccountsController
	{
		[HttpGet("~" + LegalPages.RulesPath)]
		public IActionResult RulesDocumentPage() => LegalPage(LegalPages.Rules, "Footer.Rules", m_settings.Agreements.RulesDocument);

		[HttpGet("~" + LegalPages.TermsPath)]
		public IActionResult TermsDocumentPage() => LegalPage(LegalPages.Terms, "Agreements.TermsLink", m_settings.Agreements.TermsDocument);

		[HttpGet("~" + LegalPages.PrivacyPath)]
		public IActionResult PrivacyDocumentPage() => LegalPage(LegalPages.Privacy, "Agreements.PrivacyLink", null);

		private ContentResult LegalPage(string name, string titleKey, AgreementDocument? document)
			=> Page(200, T(titleKey), LegalPages.Body(name, document), wide: true);
	}
}
