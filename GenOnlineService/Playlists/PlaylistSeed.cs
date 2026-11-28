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

namespace GenOnlineService.Playlists
{
	// The playlists a new database starts with, the same two the service shipped before they became editable
	public static class PlaylistSeed
	{
		public const string Actor = "system";

		public static readonly IReadOnlyList<PlaylistDraft> Playlists =
		[
			new PlaylistDraft(0, "1v1 (Random Armies)", true, 2, 2, 1, false, -1, false, 0,
			[
				new("[RANK] Snowy Drought ZH v5 (2)", "[RANK] Snowy Drought ZH v5", true, 2),
				new("[RANK] Natural Threats ZH v4 (2)", "[RANK] Natural Threats ZH v4", true, 2),
				new("[RANK] Arctic Lagoon ZH v2 (2)", "[RANK] Arctic Lagoon ZH v2", true, 2),
				new("[RANK] ZH Carrier is Over v2 (2)", "[RANK] ZH Carrier is Over v2", true, 2),
				new("[RANK] Vendetta ZH v1 (2)", "[RANK] Vendetta ZH v1", true, 2),
				new("[RANK] TD NoBugsCars ZH v1 (2)", "[RANK] TD NoBugsCars ZH v1", true, 2),
				new("[RANK] Sand Scorpion (2)", "[RANK] Sand Scorpion", true, 2),
				new("[RANK] Mountain Mayhem v2 (2)", "[RANK] Mountain Mayhem v2", true, 2),
				new("[RANK] Liquid Gold ZH v2 (2)", "[RANK] Liquid Gold ZH v2", true, 2),
				new("[RANK] Imminent Victory ZH v2 (2)", "[RANK] Imminent Victory ZH v2", true, 2),
				new("[RANK] Egyptian Oasis ZH v1 (2)", "[RANK] Egyptian Oasis ZH v1", true, 2),
				new("[RANK] Desolated District ZH v1 (2)", "[RANK] Desolated District ZH v1", true, 2),
				new("[RANK] Canyon of the Dead ZH v2 (2)", "[RANK] Canyon of the Dead ZH v2", true, 2),
				new("[RANK] Blossoming Valley ZH v1 (2)", "[RANK] Blossoming Valley ZH v1", true, 2),
				new("[RANK] Barren Badlands Balanced ZH v2 (2)", "[RANK] Barren Badlands Balanced ZH v2", true, 2),
				new("Battle Plan ZH v3", "Battle Plan ZH v3", true, 2),
				new("Canyon Frost ZH v1", "Canyon Frost ZH v1", true, 2),
				new("Koujou Okawa v3", "Koujou Okawa v3", true, 2),
				new("Oxygen 1", "Oxygen 1", true, 2),
				new("Shivas Paradise v4", "Shivas Paradise v4", true, 2),
				new("Thermopylae v5", "Thermopylae v5", true, 2),
				new("Tiny Tactics ZH v2", "Tiny Tactics ZH v2", true, 2),
				new("yota nation arena v3", "yota nation arena v3", true, 2),
				new("[RANK] AKAs Magic ZH v1", "[RANK] AKAs Magic ZH v1", true, 2),
				new("[RANK] Arctic Arena ZH v1", "[RANK] Arctic Arena ZH v1", true, 2),
				new("[RANK] Black Hell ZH v1", "[RANK] Black Hell ZH v1", true, 2),
				new("[RANK] Blue Hole ZH v1", "[RANK] Blue Hole ZH v1", true, 2),
				new("[RANK] Dammed Scorpion ZH v1", "[RANK] Dammed Scorpion ZH v1", true, 2),
				new("[RANK] Drallim Desert ZH v2", "[RANK] Drallim Desert ZH v2", true, 2),
				new("[RANK] Farmlands of the Fallen ZH v1", "[RANK] Farmlands of the Fallen ZH v1", true, 2),
				new("[RANK] Sakura Forest II ZH v1", "[RANK] Sakura Forest II ZH v1", true, 2),
				new("[RANK] Sovereignty ZH v1", "[RANK] Sovereignty ZH v1", true, 2),
				new("Arabia v2 (2)", "Arabia v2", true, 2),
				new("SandForest Domination v5 (2)", "SandForest Domination v5", true, 2),
				new("Toxic Lake v4 (2)", "Toxic Lake v4", true, 2),
				new("Tremble v2 (2)", "Tremble v2", true, 2),
				new("[RANK] Hanamura Temple ZH v1 (2)", "[RANK] Hanamura Temple ZH v1", true, 2),
				new("Forest of Camelot ZH v4 (2)", "Forest of Camelot ZH v4", true, 2),
				new("Mirmulnir v4 (2)", "Mirmulnir v4", true, 2),
				new("Terraform v2 (2)", "Terraform v2", true, 2),
				new("Toxic Lake v5 (2)", "Toxic Lake v5", true, 2)
			]),
			new PlaylistDraft(1, "6-8P FFA (Random Armies)", true, 6, 8, 1, false, -1, false, 30000,
			[
				new("Beijing Uprise v4 (6)", "Beijing Uprise v4", true, 6),
				new("Defcon6 (6)", "Defcon6", false, 6),
				new("Taiga Terror (6)", "Taiga Terror", true, 6),
				new("Swamp Assault v3 [WBC2021] (7)", "Swamp Assault v3 [WBC2021]", true, 7),
				new("[RANK] Muddy Madness ZH v1 (8)", "[RANK] Muddy Madness ZH v1", true, 8),
				new("[RANK] Wastelands Dust ZH v1 (8)", "[RANK] Wastelands Dust ZH v1", true, 8)
			])
		];
	}
}
