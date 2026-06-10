
//  Copyright 2026 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using System;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using Org.BouncyCastle.Crypto;
using System.Collections.Generic;
using Org.BouncyCastle.OpenSsl;
using System.Linq;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager
{
    public static class CertUtilities
    {
        /// <summary>
        /// Check if binary data is a valid PFX using .NET's X509Certificate2
        /// </summary>
        public static bool IsValidPfx(byte[] data, string password = null)
        {
            if (data == null || data.Length == 0)
                return false;

            try
            {
                // .NET 6 compatible - use MachineKeySet or DefaultKeySet
                var cert = new X509Certificate2(data, password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);

                // Additional validation - check if it has a private key (typical for PFX)
                bool hasPrivateKey = cert.HasPrivateKey;

                cert.Dispose();
                return true; // Successfully loaded as PFX
            }
            catch (CryptographicException)
            {
                return false; // Not a valid PFX or wrong password
            }
            catch (Exception)
            {
                return false; // Other errors
            }
        }

        /// <summary>
        /// Simple validation method for JKS certificate stores
        /// Note: This is a basic structural validation since full JKS parsing requires Java interop
        /// </summary>
        public static bool IsValidJks(byte[] data, string password = null)
        {
            if (data == null || data.Length == 0)
                return false;

            try
            {
                // Check JKS file signature first
                if (!HasJksSignature(data))
                    return false;

                // Basic structural validation
                return ValidateJksStructure(data);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string ConvertPfxToPem(string base64Pfx, string password)
        {
            if (string.IsNullOrEmpty(base64Pfx))
                throw new ArgumentException("Base64 PFX string cannot be null or empty", nameof(base64Pfx));

            byte[] pfxBytes;
            try
            {
                pfxBytes = Convert.FromBase64String(base64Pfx);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Invalid base64 string format", nameof(base64Pfx), ex);
            }

            try
            {
                // Legacy PEM format: the leaf certificate followed by its private key (no chain).
                // Certificates are read via .NET (public data only, no key export); the private
                // key is exported via BouncyCastle to avoid the .NET/CNG export failure on Windows.
                var leafPem = BuildCertPemFromPfx(pfxBytes, password, leafOnly: true);
                var keyPem = GetPrivateKeyPem(pfxBytes, password);
                return leafPem + "\n" + keyPem;
            }
            catch (InvalidOperationException)
            {
                throw; // preserve the specific "no private key" contract
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to process PFX certificate. Check the base64 string and password.", ex);
            }
        }

        /// <summary>
        /// Converts a base64-encoded PFX into a (certificate, private_key) pair of PEM strings
        /// for the SeparatePrivateKey JSON format.
        ///   - certificate: the leaf certificate followed by the rest of the chain, leaf first.
        ///   - private_key: the private key in PKCS#8 form ("-----BEGIN PRIVATE KEY-----"),
        ///     identical to the encoding used by ConvertPfxToPem.
        /// </summary>
        public static (string certificatePem, string privateKeyPem) ConvertPfxToCertAndKeyPem(string base64Pfx, string password)
        {
            if (string.IsNullOrEmpty(base64Pfx))
                throw new ArgumentException("Base64 PFX string cannot be null or empty", nameof(base64Pfx));

            byte[] pfxBytes;
            try
            {
                pfxBytes = Convert.FromBase64String(base64Pfx);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Invalid base64 string format", nameof(base64Pfx), ex);
            }

            var certificatePem = BuildCertPemFromPfx(pfxBytes, password, leafOnly: false);
            var privateKeyPem = GetPrivateKeyPem(pfxBytes, password);

            return (certificatePem, privateKeyPem);
        }

        /// <summary>
        /// Reads the certificates from a PFX (public data only, via .NET) and returns them as
        /// concatenated PEM blocks. The leaf (the entry that carries the private key) is placed
        /// first; when leafOnly is false the issuer chain follows, ordered by walking
        /// issuer/subject links rather than trusting any library's chain association.
        /// </summary>
        private static string BuildCertPemFromPfx(byte[] pfxBytes, string password, bool leafOnly)
        {
            var collection = new X509Certificate2Collection();
            collection.Import(pfxBytes, password, X509KeyStorageFlags.EphemeralKeySet);
            try
            {
                var all = collection.Cast<X509Certificate2>().ToList();
                if (!all.Any())
                    throw new InvalidOperationException("No certificates found in PFX");

                var leaf = all.FirstOrDefault(c => c.HasPrivateKey);
                if (leaf == null)
                    throw new InvalidOperationException("Certificate does not contain a private key");

                var ordered = new List<X509Certificate2> { leaf };

                if (!leafOnly)
                {
                    var remaining = all.Where(c => !ReferenceEquals(c, leaf)).ToList();
                    var current = leaf;

                    // Walk issuer links: find the cert whose Subject matches the current Issuer.
                    while (remaining.Count > 0 && !IsSelfSigned(current))
                    {
                        var issuer = remaining.FirstOrDefault(
                            c => c.SubjectName.RawData.SequenceEqual(current.IssuerName.RawData));
                        if (issuer == null) break;
                        ordered.Add(issuer);
                        remaining.Remove(issuer);
                        current = issuer;
                    }

                    // Defensive: include any certs we couldn't place rather than dropping them.
                    ordered.AddRange(remaining);
                }

                var sb = new StringBuilder();
                foreach (var cert in ordered)
                {
                    sb.AppendLine("-----BEGIN CERTIFICATE-----");
                    sb.AppendLine(FormatBase64String(Convert.ToBase64String(cert.RawData)));
                    sb.AppendLine("-----END CERTIFICATE-----");
                }
                return sb.ToString().TrimEnd();
            }
            finally
            {
                foreach (var cert in collection)
                {
                    cert.Dispose();
                }
            }
        }

        private static bool IsSelfSigned(X509Certificate2 cert)
            => cert.SubjectName.RawData.SequenceEqual(cert.IssuerName.RawData);

        /// <summary>
        /// Exports the PFX private key as a PKCS#8 PEM block via BouncyCastle, avoiding the
        /// platform-specific .NET/CNG export path.
        /// </summary>
        private static string GetPrivateKeyPem(byte[] pfxBytes, string password)
        {
            var store = new Pkcs12StoreBuilder().Build();
            using (var ms = new MemoryStream(pfxBytes))
            {
                store.Load(ms, password?.ToCharArray() ?? new char[0]);
            }

            var keyAlias = store.Aliases.Cast<string>().FirstOrDefault(a => store.IsKeyEntry(a));
            if (keyAlias == null)
                throw new InvalidOperationException("Certificate does not contain a private key");

            return ExportPrivateKeyPem(store.GetKey(keyAlias).Key);
        }

        /// <summary>
        /// Exports a BouncyCastle private key as an unencrypted PKCS#8 PEM block
        /// ("-----BEGIN PRIVATE KEY-----"), matching the header convention used elsewhere.
        /// Sourcing the key from the PKCS12 store avoids the platform-specific .NET/CNG export
        /// path and works uniformly for RSA and ECDSA keys.
        /// </summary>
        private static string ExportPrivateKeyPem(AsymmetricKeyParameter privateKey)
        {
            var pkcs8 = PrivateKeyInfoFactory.CreatePrivateKeyInfo(privateKey);
            var der = pkcs8.GetDerEncoded();

            var sb = new StringBuilder();
            sb.AppendLine("-----BEGIN PRIVATE KEY-----");
            sb.AppendLine(FormatBase64String(Convert.ToBase64String(der)));
            sb.AppendLine("-----END PRIVATE KEY-----");
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Evaluates the CertificateTags placeholder tokens against the leaf certificate of a PFX.
        /// Returns a token-to-value map: serial number as the certificate's hexadecimal serial,
        /// and validity dates as ISO-8601 UTC (yyyy-MM-ddTHH:mm:ssZ).
        /// </summary>
        public static IReadOnlyDictionary<string, string> GetCertificateTagTokenValues(string base64Pfx, string password)
        {
            if (string.IsNullOrEmpty(base64Pfx))
                throw new ArgumentException("Base64 PFX string cannot be null or empty", nameof(base64Pfx));

            byte[] pfxBytes;
            try
            {
                pfxBytes = Convert.FromBase64String(base64Pfx);
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("Invalid base64 string format", nameof(base64Pfx), ex);
            }

            var collection = new X509Certificate2Collection();
            collection.Import(pfxBytes, password, X509KeyStorageFlags.EphemeralKeySet);
            try
            {
                var all = collection.Cast<X509Certificate2>().ToList();
                var leaf = all.FirstOrDefault(c => c.HasPrivateKey) ?? all.FirstOrDefault();
                if (leaf == null)
                    throw new InvalidOperationException("No certificates found in PFX");

                return new Dictionary<string, string>
                {
                    [CertificateTagTokens.SERIAL_NUMBER] = leaf.SerialNumber,
                    [CertificateTagTokens.NOT_BEFORE] = leaf.NotBefore.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                    [CertificateTagTokens.NOT_AFTER] = leaf.NotAfter.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                };
            }
            finally
            {
                foreach (var cert in collection)
                {
                    cert.Dispose();
                }
            }
        }

        /// <summary>
        /// Returns a copy of the tag dictionary with every occurrence of each token (in the tag
        /// VALUES only) replaced by its evaluated value. Tag keys are left untouched.
        /// </summary>
        public static Dictionary<string, string> ApplyTagTokens(
            IReadOnlyDictionary<string, string> tags, IReadOnlyDictionary<string, string> tokenValues)
        {
            var result = new Dictionary<string, string>();
            if (tags == null) return result;

            foreach (var kvp in tags)
            {
                var value = kvp.Value;
                if (!string.IsNullOrEmpty(value) && tokenValues != null)
                {
                    foreach (var token in tokenValues)
                    {
                        value = value.Replace(token.Key, token.Value);
                    }
                }
                result[kvp.Key] = value;
            }
            return result;
        }

        /// <summary>
        /// Converts a PFX certificate store to a JKS certificate store
        /// </summary>
        /// <param name="pfxData">PFX certificate store binary data</param>
        /// <param name="pfxPassword">PFX password</param>
        /// <param name="jksPassword">JKS password (if null, uses same as PFX password)</param>
        /// <param name="defaultAlias">Default alias name for certificates (if null, uses certificate subject CN)</param>
        /// <returns>JKS certificate store as byte array</returns>
        public static byte[] ConvertPfxToJks(byte[] pfxData, string pfxPassword)
        {
            if (pfxData == null || pfxData.Length == 0)
                throw new ArgumentException("PFX data cannot be null or empty", nameof(pfxData));

            // Use same password for JKS if not specified
            var jksPassword = pfxPassword;

            try
            {
                // Load the PFX using BouncyCastle
                var pfxStore = new Pkcs12StoreBuilder().Build();
                using (var pfxStream = new MemoryStream(pfxData))
                {
                    pfxStore.Load(pfxStream, pfxPassword?.ToCharArray() ?? new char[0]);
                }

                // Create new JKS store (using PKCS12 format which is compatible)
                var jksStore = new Pkcs12StoreBuilder().Build();

                // Get all aliases from PFX
                var aliases = pfxStore.Aliases.Cast<string>().ToList();
                var aliasCounter = 1;

                foreach (string originalAlias in aliases)
                {
                    // Get private key if it exists
                    AsymmetricKeyParameter privateKey = null;
                    if (pfxStore.IsKeyEntry(originalAlias))
                    {
                        var keyEntry = pfxStore.GetKey(originalAlias);
                        privateKey = keyEntry?.Key;
                    }

                    // Get certificate chain
                    var certChain = pfxStore.GetCertificateChain(originalAlias);
                    if (certChain == null || certChain.Length == 0)
                    {
                        // Try to get standalone certificate
                        var cert = pfxStore.GetCertificate(originalAlias);
                        if (cert != null)
                        {
                            certChain = new X509CertificateEntry[] { cert };
                        }
                    }

                    if (certChain != null && certChain.Length > 0)
                    {
                        // Determine alias name for JKS
                        string jksAlias = GenerateJksAlias(originalAlias, certChain[0].Certificate, null, aliasCounter);

                        if (privateKey != null)
                        {
                            // Add private key entry with certificate chain
                            jksStore.SetKeyEntry(jksAlias, new AsymmetricKeyEntry(privateKey), certChain);
                        }
                        else
                        {
                            // Add certificate-only entry
                            jksStore.SetCertificateEntry(jksAlias, certChain[0]);
                        }

                        aliasCounter++;
                    }
                }

                // Save JKS to byte array
                using (var jksStream = new MemoryStream())
                {
                    jksStore.Save(jksStream, jksPassword?.ToCharArray() ?? new char[0], new SecureRandom());
                    return jksStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to convert PFX to JKS: {ex.Message}", ex);
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

        /// <summary>
        /// Basic JKS structure validation
        /// </summary>
        private static bool ValidateJksStructure(byte[] data)
        {
            if (data.Length < 8) // Minimum size: magic(4) + version(4)
                return false;

            try
            {
                using (var stream = new MemoryStream(data))
                using (var reader = new BinaryReader(stream))
                {
                    // Read magic number (already validated)
                    var magic = ReadBigEndianInt32(reader);

                    // Read version (should be 1 or 2)
                    var version = ReadBigEndianInt32(reader);
                    if (version < 1 || version > 2)
                        return false;

                    // Read entry count
                    var entryCount = ReadBigEndianInt32(reader);
                    if (entryCount < 0 || entryCount > 10000) // Reasonable limit
                        return false;

                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check JKS file signature (magic bytes)
        /// </summary>
        private static bool HasJksSignature(byte[] data)
        {
            if (data.Length < 4)
                return false;

            // JKS files start with magic number 0xFEEDFACE (big-endian)
            return data[0] == 0xFE && data[1] == 0xED &&
                   data[2] == 0xFA && data[3] == 0xCE;
        }

        /// <summary>
        /// Read big-endian 32-bit integer (Java format)
        /// </summary>
        private static int ReadBigEndianInt32(BinaryReader reader)
        {
            var bytes = reader.ReadBytes(4);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToInt32(bytes, 0);
        }
        public static (List<string>, bool) ConvertPemToFullChainBase64(string pemCertificateWithKey)
        {
            (var certificates, var hasPrivateKey) = ExtractCertificatesFromPem(pemCertificateWithKey);
            var encodedCerts = new List<string>();

            if (!certificates.Any())
            {
                throw new InvalidOperationException("No certificates found in PEM data");
            }
            var includesChain = certificates.Count > 1;

            // Combine all certificates into a single byte array
            var allCertBytes = new List<byte>();
            foreach (var cert in certificates)
            {
                var encodedCert = Convert.ToBase64String(cert.GetEncoded());
                encodedCerts.Add(encodedCert);
            }

            return (encodedCerts, hasPrivateKey);
        }

        // Extract all certificates from PEM, preserving order
        private static (List<Org.BouncyCastle.X509.X509Certificate>, bool) ExtractCertificatesFromPem(string pemData)
        {
            var certificates = new List<Org.BouncyCastle.X509.X509Certificate>();
            var privateKeyIncluded = false;

            try
            {
                using (var stringReader = new StringReader(pemData))
                {
                    var pemReader = new PemReader(stringReader);

                    object pemObject;
                    while ((pemObject = pemReader.ReadObject()) != null)
                    {
                        if (pemObject is Org.BouncyCastle.X509.X509Certificate cert)
                        {
                            certificates.Add(cert);
                        }
                        if (pemObject is AsymmetricCipherKeyPair || pemObject is AsymmetricKeyParameter)
                        {
                            privateKeyIncluded = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error extracting certificates from PEM: {ex.Message}", ex);
            }

            return (certificates, privateKeyIncluded);
        }

        /// <summary>
        /// Generate appropriate alias name for JKS entry
        /// </summary>
        private static string GenerateJksAlias(string originalAlias, Org.BouncyCastle.X509.X509Certificate certificate, string defaultAlias, int counter)
        {
            if (!string.IsNullOrEmpty(defaultAlias))
                return counter > 1 ? $"{defaultAlias}_{counter}" : defaultAlias;

            if (!string.IsNullOrEmpty(originalAlias) && !originalAlias.Equals("1", StringComparison.OrdinalIgnoreCase))
                return SanitizeAlias(originalAlias);

            // Try to extract CN from certificate subject
            try
            {
                var subjectDN = certificate.SubjectDN.ToString();
                var cnStart = subjectDN.IndexOf("CN=", StringComparison.OrdinalIgnoreCase);
                if (cnStart >= 0)
                {
                    cnStart += 3; // Skip "CN="
                    var cnEnd = subjectDN.IndexOf(",", cnStart);
                    var cn = cnEnd > cnStart ? subjectDN.Substring(cnStart, cnEnd - cnStart) : subjectDN.Substring(cnStart);
                    cn = SanitizeAlias(cn.Trim());
                    if (!string.IsNullOrEmpty(cn))
                        return cn;
                }
            }
            catch
            {
                // Fall back to default naming
            }

            return $"certificate_{counter}";
        }

        /// <summary>
        /// Sanitize alias name for JKS compatibility
        /// </summary>
        private static string SanitizeAlias(string alias)
        {
            if (string.IsNullOrEmpty(alias))
                return alias;

            // Replace invalid characters and convert to lowercase
            var sanitized = alias
                .Replace(" ", "_")
                .Replace(".", "_")
                .Replace("*", "wildcard")
                .Replace("@", "_at_")
                .ToLowerInvariant();

            // Remove any remaining invalid characters
            var validChars = sanitized.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray();
            return new string(validChars);
        }
    }

}
