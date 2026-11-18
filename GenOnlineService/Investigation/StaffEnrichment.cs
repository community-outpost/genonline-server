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

namespace GenOnlineService.Investigation
{
	public sealed class StaffEnrichment
	{
		public StaffEnrichment(IAddressDirectory addresses, OuiRegistry vendors)
		{
			Addresses = addresses;
			Vendors = vendors;
		}

		public IAddressDirectory Addresses { get; }

		public OuiRegistry Vendors { get; }

		public static StaffEnrichment Of(IServiceProvider services)
		{
			return new StaffEnrichment(services.GetService<IAddressDirectory>() ?? MaxMindAddressDirectory.Default, services.GetService<OuiRegistry>() ?? OuiRegistry.Default);
		}

		public string? VendorOf(string kind, string? valueRaw) => kind == DeviceKinds.Mac ? Vendors.VendorOf(valueRaw) : null;
	}
}
