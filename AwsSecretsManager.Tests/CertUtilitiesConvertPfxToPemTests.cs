// Copyright 2026 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Xunit;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Tests
{
    /// <summary>
    /// Tests for the legacy CertUtilities.ConvertPfxToPem (the concatenated-PEM format used by
    /// AWSSMPEM without SeparatePrivateKey). The RSA/EC cases specifically guard against the
    /// Windows CNG export failure ("The requested operation is not supported") that occurs when
    /// exporting a private key imported from a PFX via the .NET key APIs.
    /// </summary>
    public class CertUtilitiesConvertPfxToPemTests
    {
        [Fact]
        public void ConvertPfxToPem_Rsa_ReturnsLeafAndPkcs8Key()
        {
            // Regression guard: before routing the key export through BouncyCastle, this threw
            // a CryptographicException on Windows for any PFX whose key originated from CNG.
            var pfx = TestCertFactory.CreateSelfSignedRsaPfxBase64();

            var pem = CertUtilities.ConvertPfxToPem(pfx, TestCertFactory.Password);

            pem.Should().Contain("-----BEGIN CERTIFICATE-----").And.Contain("-----END CERTIFICATE-----");
            pem.Should().Contain("-----BEGIN PRIVATE KEY-----").And.Contain("-----END PRIVATE KEY-----");
            pem.Should().NotContain("BEGIN RSA PRIVATE KEY", "the key must be PKCS#8");
        }

        [Fact]
        public void ConvertPfxToPem_Ec_ReturnsPkcs8Key()
        {
            var pfx = TestCertFactory.CreateSelfSignedEcPfxBase64();

            var pem = CertUtilities.ConvertPfxToPem(pfx, TestCertFactory.Password);

            pem.Should().Contain("-----BEGIN PRIVATE KEY-----");
            pem.Should().NotContain("BEGIN EC PRIVATE KEY");
        }

        [Fact]
        public void ConvertPfxToPem_KeyMatchesCertificate()
        {
            var pfx = TestCertFactory.CreateSelfSignedRsaPfxBase64("CN=pem-match");

            var pem = CertUtilities.ConvertPfxToPem(pfx, TestCertFactory.Password);

            // CreateFromPem throws if the key does not correspond to the certificate.
            using var rebuilt = X509Certificate2.CreateFromPem(pem, pem);
            rebuilt.HasPrivateKey.Should().BeTrue();
            rebuilt.Subject.Should().Contain("pem-match");
        }

        [Fact]
        public void ConvertPfxToPem_ChainPfx_ReturnsLeafOnly()
        {
            // The legacy format is leaf + key only (no chain). This locks that contract and
            // distinguishes it from ConvertPfxToCertAndKeyPem, which includes the full chain.
            var pfx = TestCertFactory.CreateChainPfxBase64(out var leafSubject, out _);

            var pem = CertUtilities.ConvertPfxToPem(pfx, TestCertFactory.Password);

            var certs = new X509Certificate2Collection();
            certs.ImportFromPem(pem);

            certs.Count.Should().Be(1, "the legacy PEM format includes only the leaf certificate");
            certs[0].Subject.Should().Be(leafSubject);
        }

        [Fact]
        public void ConvertPfxToPem_IncludeChainFalse_ReturnsLeafOnly()
        {
            var pfx = TestCertFactory.CreateChainPfxBase64(out var leafSubject, out _);

            var pem = CertUtilities.ConvertPfxToPem(pfx, TestCertFactory.Password, includeChain: false);

            var certs = new X509Certificate2Collection();
            certs.ImportFromPem(pem);

            certs.Count.Should().Be(1);
            certs[0].Subject.Should().Be(leafSubject);
        }

        [Fact]
        public void ConvertPfxToPem_IncludeChain_ReturnsLeafThenChainThenKey()
        {
            var pfx = TestCertFactory.CreateChainPfxBase64(out var leafSubject, out var rootSubject);

            var pem = CertUtilities.ConvertPfxToPem(pfx, TestCertFactory.Password, includeChain: true);

            var certs = new X509Certificate2Collection();
            certs.ImportFromPem(pem);

            certs.Count.Should().Be(2, "the leaf and its issuer are both included");
            certs[0].Subject.Should().Be(leafSubject, "the leaf comes first");
            certs[1].Subject.Should().Be(rootSubject);

            pem.IndexOf("-----BEGIN PRIVATE KEY-----", StringComparison.Ordinal).Should().BeGreaterThan(
                pem.LastIndexOf("-----END CERTIFICATE-----", StringComparison.Ordinal),
                "the private key follows the full chain");
        }

        [Fact]
        public void ConvertPfxToPem_IncludeChain_KeyMatchesLeaf()
        {
            var pfx = TestCertFactory.CreateChainPfxBase64(out var leafSubject, out _);

            var pem = CertUtilities.ConvertPfxToPem(pfx, TestCertFactory.Password, includeChain: true);

            // CreateFromPem loads the first certificate and throws if the key does not match it.
            using var rebuilt = X509Certificate2.CreateFromPem(pem, pem);
            rebuilt.HasPrivateKey.Should().BeTrue();
            rebuilt.Subject.Should().Be(leafSubject);
        }

        [Fact]
        public void ConvertPfxToPem_IncludeChain_SelfSigned_ReturnsSingleCert()
        {
            var pfx = TestCertFactory.CreateSelfSignedRsaPfxBase64("CN=no-chain");

            var pem = CertUtilities.ConvertPfxToPem(pfx, TestCertFactory.Password, includeChain: true);

            var certs = new X509Certificate2Collection();
            certs.ImportFromPem(pem);

            certs.Count.Should().Be(1, "a PFX with no issuer certs has nothing to add");
            pem.Should().Contain("-----BEGIN PRIVATE KEY-----");
        }

        [Fact]
        public void ConvertPfxToPem_InvalidBase64_Throws()
        {
            Action act = () => CertUtilities.ConvertPfxToPem("not-base64!!!", TestCertFactory.Password);

            act.Should().Throw<ArgumentException>();
        }
    }
}
