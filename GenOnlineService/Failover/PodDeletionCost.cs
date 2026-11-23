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

using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace GenOnlineService.Failover
{
	// The pod's controller.kubernetes.io/pod-deletion-cost: a ReplicaSet scaling down removes the pods with the lower cost first,
	// so in a rolling restart the old standby goes before the lease holder and the holder hands over once, to a new copy
	public interface IPodDeletionCost
	{
		Task SetAsync(int cost, CancellationToken cancellationToken);
	}

	// Patches the own pod through the Kubernetes API with the pod's service account (patch on pods: genonline-infra's Role, narrowed by outpost-infra's ValidatingAdmissionPolicy genonline-pod-deletion-cost)
	public sealed class KubernetesPodDeletionCost : IPodDeletionCost
	{
		public const string Annotation = "controller.kubernetes.io/pod-deletion-cost";
		private const string AccountDirectory = "/var/run/secrets/kubernetes.io/serviceaccount";

		private readonly HttpClient m_http;
		private readonly string m_podPath;
		private readonly string m_tokenPath;

		private KubernetesPodDeletionCost(HttpClient http, string podPath, string tokenPath)
		{
			m_http = http;
			m_podPath = podPath;
			m_tokenPath = tokenPath;
		}

		// Null outside a pod: no API address, no POD_NAME or no service account files
		public static KubernetesPodDeletionCost? FromEnvironment()
		{
			string? host = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
			string? port = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_PORT");
			string? pod = Environment.GetEnvironmentVariable("POD_NAME");
			string namespacePath = Path.Combine(AccountDirectory, "namespace");
			string caPath = Path.Combine(AccountDirectory, "ca.crt");
			string tokenPath = Path.Combine(AccountDirectory, "token");
			if (String.IsNullOrEmpty(host) || String.IsNullOrEmpty(port) || String.IsNullOrEmpty(pod) || !File.Exists(namespacePath) || !File.Exists(caPath) || !File.Exists(tokenPath))
			{
				return null;
			}

			X509Certificate2 ca = X509CertificateLoader.LoadCertificateFromFile(caPath);
			HttpClientHandler handler = new()
			{
				// the API server's certificate chains to the cluster CA of the service account, not to a public root
				ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
				{
					if (certificate == null || (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None)
					{
						return false;
					}
					using X509Chain chain = new();
					chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
					chain.ChainPolicy.CustomTrustStore.Add(ca);
					chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
					return chain.Build(certificate);
				}
			};
			string hostPart = host.Contains(':') ? "[" + host + "]" : host;
			HttpClient http = new(handler) { BaseAddress = new Uri($"https://{hostPart}:{port}"), Timeout = TimeSpan.FromSeconds(5) };
			string ns = File.ReadAllText(namespacePath).Trim();
			return new KubernetesPodDeletionCost(http, $"/api/v1/namespaces/{Uri.EscapeDataString(ns)}/pods/{Uri.EscapeDataString(pod)}", tokenPath);
		}

		public async Task SetAsync(int cost, CancellationToken cancellationToken)
		{
			string body = "{\"metadata\":{\"annotations\":{\"" + Annotation + "\":\"" + cost.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\"}}}";
			using HttpRequestMessage request = new(HttpMethod.Patch, m_podPath)
			{
				Content = new StringContent(body, Encoding.UTF8, "application/merge-patch+json")
			};
			// the projected token rotates, so it is read for every request
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", (await File.ReadAllTextAsync(m_tokenPath, cancellationToken)).Trim());
			using HttpResponseMessage response = await m_http.SendAsync(request, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				throw new HttpRequestException($"Patching the pod's {Annotation} returned {(int)response.StatusCode}", null, response.StatusCode);
			}
		}
	}
}
