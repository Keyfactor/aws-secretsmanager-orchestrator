
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using System;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager
{
    static class Constants
    {
        public const string STORE_TYPE_PEM = "AWSSMPEM";
        public const string STORE_TYPE_PFX = "AWSSMPFX";
        public const string STORE_TYPE_JKS = "AWSSMJKS";
    }
    public static class KeyfactorJobType
    {
        public const string INVENTORY = "Inventory";
        public const string MANAGEMENT = "Management";
    }

    public static class AWSFilterParameter
    {
        public const string NAME = "name";
        public const string TAG_KEY = "tag-key";
        public const string TAG_VALUE = "tag-value";
    }

    public static class EntryParameterKeys {
        public const string TAGS = "CertificateTags";
        public const string REPLICAREGIONS = "ReplicaRegions";
    }

    public static class CertUtilities
    {
        public static string ConvertPfxToPem(string base64Pfx, string password)
        {
            if (string.IsNullOrEmpty(base64Pfx))
                throw new ArgumentException("Base64 PFX string cannot be null or empty", nameof(base64Pfx));

            try
            {
                // Convert base64 string to byte array
                byte[] pfxBytes = Convert.FromBase64String(base64Pfx);

                // Load the certificate with private key
                X509Certificate2 cert = new X509Certificate2(pfxBytes, password,
                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

                StringBuilder pemBuilder = new StringBuilder();

                // Export the certificate as PEM
                byte[] certBytes = cert.Export(X509ContentType.Cert);
                string certPem = Convert.ToBase64String(certBytes);
                pemBuilder.AppendLine("-----BEGIN CERTIFICATE-----");
                pemBuilder.AppendLine(FormatBase64String(certPem));
                pemBuilder.AppendLine("-----END CERTIFICATE-----");

                // Export the private key as PEM
                if (cert.HasPrivateKey)
                {
                    // For RSA keys
                    if (cert.GetRSAPrivateKey() != null)
                    {
                        RSA rsa = cert.GetRSAPrivateKey();
                        byte[] privateKeyBytes = rsa.ExportPkcs8PrivateKey();
                        string privateKeyPem = Convert.ToBase64String(privateKeyBytes);

                        pemBuilder.AppendLine("-----BEGIN PRIVATE KEY-----");
                        pemBuilder.AppendLine(FormatBase64String(privateKeyPem));
                        pemBuilder.AppendLine("-----END PRIVATE KEY-----");
                    }
                    // For ECDSA keys
                    else if (cert.GetECDsaPrivateKey() != null)
                    {
                        ECDsa ecdsa = cert.GetECDsaPrivateKey();
                        byte[] privateKeyBytes = ecdsa.ExportPkcs8PrivateKey();
                        string privateKeyPem = Convert.ToBase64String(privateKeyBytes);

                        pemBuilder.AppendLine("-----BEGIN PRIVATE KEY-----");
                        pemBuilder.AppendLine(FormatBase64String(privateKeyPem));
                        pemBuilder.AppendLine("-----END PRIVATE KEY-----");
                    }
                    else
                    {
                        throw new NotSupportedException("Unsupported private key algorithm");
                    }
                }
                else
                {
                    throw new InvalidOperationException("Certificate does not contain a private key");
                }

                return pemBuilder.ToString();
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException("Failed to process PFX certificate. Check the base64 string and password.", ex);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Invalid base64 string format", nameof(base64Pfx), ex);
            }
        }

        /// <summary>
        /// Formats a base64 string to 64 characters per line (PEM standard)
        /// </summary>
        /// <param name="base64String">Base64 string to format</param>
        /// <returns>Formatted base64 string with line breaks</returns>
        private static string FormatBase64String(string base64String)
        {
            StringBuilder formatted = new StringBuilder();
            for (int i = 0; i < base64String.Length; i += 64)
            {
                int length = Math.Min(64, base64String.Length - i);
                formatted.AppendLine(base64String.Substring(i, length));
            }
            return formatted.ToString().TrimEnd();
        }
    }
}
