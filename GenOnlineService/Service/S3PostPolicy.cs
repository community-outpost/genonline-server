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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace GenOnlineService.Service
{
	// the file field is added last by the sender
	public sealed record S3PostForm(string Url, IReadOnlyList<KeyValuePair<string, string>> Fields, DateTime ExpiresAt);

	public sealed record UploadTarget(string Url, IReadOnlyDictionary<string, string>? Fields = null);

	public static class S3PostPolicy
	{
		private const string Algorithm = "AWS4-HMAC-SHA256";

		public static S3PostForm Create(string endpoint, string bucket, string region, string accessKey, string secretKey,
			string objectKey, string contentType, long minBytes, long maxBytes, DateTime nowUtc, TimeSpan lifetime, string? acl = null)
		{
			if (minBytes < 0 || maxBytes < minBytes)
			{
				throw new ArgumentException("The size range is not valid");
			}

			DateTime expiry = nowUtc.Add(lifetime);
			string date = nowUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
			string amzDate = nowUtc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
			string credential = $"{accessKey}/{date}/{region}/s3/aws4_request";

			var conditions = new JsonArray
			{
				new JsonObject { ["bucket"] = bucket },
				new JsonObject { ["key"] = objectKey },
				new JsonObject { ["Content-Type"] = contentType },
				new JsonArray("content-length-range", minBytes, maxBytes),
				new JsonObject { ["x-amz-algorithm"] = Algorithm },
				new JsonObject { ["x-amz-credential"] = credential },
				new JsonObject { ["x-amz-date"] = amzDate }
			};

			// A canned ACL such as public-read makes only this object readable, and every form field needs a condition
			if (acl != null)
			{
				conditions.Add(new JsonObject { ["acl"] = acl });
			}

			var policy = new JsonObject
			{
				["expiration"] = expiry.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
				["conditions"] = conditions
			};

			string encodedPolicy = Convert.ToBase64String(Encoding.UTF8.GetBytes(policy.ToJsonString()));
			string signature = Convert.ToHexString(Hmac(SigningKey(secretKey, date, region), encodedPolicy)).ToLowerInvariant();

			var fields = new List<KeyValuePair<string, string>>
			{
				new("key", objectKey),
				new("Content-Type", contentType),
				new("x-amz-algorithm", Algorithm),
				new("x-amz-credential", credential),
				new("x-amz-date", amzDate)
			};
			if (acl != null)
			{
				fields.Add(new("acl", acl));
			}
			fields.Add(new("policy", encodedPolicy));
			fields.Add(new("x-amz-signature", signature));

			return new S3PostForm(endpoint.TrimEnd('/') + "/" + bucket, fields, expiry);
		}

		internal static byte[] SigningKey(string secretKey, string date, string region, string service = "s3")
		{
			byte[] dateKey = Hmac(Encoding.UTF8.GetBytes("AWS4" + secretKey), date);
			byte[] regionKey = Hmac(dateKey, region);
			byte[] serviceKey = Hmac(regionKey, service);
			return Hmac(serviceKey, "aws4_request");
		}

		private static byte[] Hmac(byte[] key, string data)
		{
			return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));
		}
	}
}
