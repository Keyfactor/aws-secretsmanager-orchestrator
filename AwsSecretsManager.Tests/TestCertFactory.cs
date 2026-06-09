// Copyright 2026 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Tests
{
    /// <summary>
    /// Builds real, self-contained certificate material (PFX) in-memory for tests, so no
    /// external fixture files are required. Returns base64-encoded PFX bytes, matching the
    /// shape the orchestrator receives in CertProperties.Contents.
    /// </summary>
    internal static class TestCertFactory
    {
        public const string Password = "pfx-pass";

        public static string CreateSelfSignedRsaPfxBase64(string subject = "CN=test-leaf")
        {
            using var rsa = RSA.Create(2048);
            var req = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            return Convert.ToBase64String(cert.Export(X509ContentType.Pfx, Password));
        }

        public static string CreateSelfSignedEcPfxBase64(string subject = "CN=ec-leaf")
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var req = new CertificateRequest(subject, ecdsa, HashAlgorithmName.SHA256);
            using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
            return Convert.ToBase64String(cert.Export(X509ContentType.Pfx, Password));
        }

        /// <summary>
        /// Creates a PFX containing a leaf certificate (with private key) signed by a root CA,
        /// plus the root certificate, so the chain can be exercised. Outputs the exact subject
        /// strings for ordering assertions.
        /// </summary>
        public static string CreateChainPfxBase64(out string leafSubject, out string rootSubject)
        {
            rootSubject = "CN=test-root-ca";
            leafSubject = "CN=test-leaf.example";

            using var rootRsa = RSA.Create(2048);
            var rootReq = new CertificateRequest(rootSubject, rootRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            rootReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            rootReq.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            using var rootCert = rootReq.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(5));

            using var leafRsa = RSA.Create(2048);
            var leafReq = new CertificateRequest(leafSubject, leafRsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            leafReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));

            var serial = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            using var leafNoKey = leafReq.Create(
                rootCert, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1), serial);
            using var leafWithKey = leafNoKey.CopyWithPrivateKey(leafRsa);

            // Add the root as a public-only certificate (no private key), mirroring a real
            // chain PFX from Command where only the leaf carries a key.
            using var rootPublicOnly = new X509Certificate2(rootCert.Export(X509ContentType.Cert));

            var collection = new X509Certificate2Collection { leafWithKey, rootPublicOnly };
            return Convert.ToBase64String(collection.Export(X509ContentType.Pfx, Password));
        }
    }
}
