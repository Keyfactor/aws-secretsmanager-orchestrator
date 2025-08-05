using System;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using Org.BouncyCastle.Crypto;
using System.Collections.Generic;
using Org.BouncyCastle.OpenSsl;
using System.Linq;

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
    }

}
