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
    /// Tests for CertUtilities.ConvertPfxToCertAndKeyPem, which backs the SeparatePrivateKey
    /// JSON format. Verifies PEM shape, PKCS#8 key header, key/cert correspondence, and the
    /// critical leaf-first chain ordering.
    /// </summary>
    public class CertUtilitiesSeparateKeyTests
    {
        [Fact]
        public void ConvertPfxToCertAndKeyPem_Rsa_ReturnsPemCertAndPkcs8Key()
        {
            var pfx = TestCertFactory.CreateSelfSignedRsaPfxBase64();

            var (certPem, keyPem) = CertUtilities.ConvertPfxToCertAndKeyPem(pfx, TestCertFactory.Password);

            certPem.Should().Contain("-----BEGIN CERTIFICATE-----").And.Contain("-----END CERTIFICATE-----");
            keyPem.Should().Contain("-----BEGIN PRIVATE KEY-----").And.Contain("-----END PRIVATE KEY-----");
            keyPem.Should().NotContain("BEGIN RSA PRIVATE KEY", "the key must be PKCS#8, matching the legacy PEM format");
            keyPem.Should().NotContain("ENCRYPTED", "the key is stored unencrypted (relies on KMS at rest)");
        }

        [Fact]
        public void ConvertPfxToCertAndKeyPem_Ec_ReturnsPkcs8Key()
        {
            var pfx = TestCertFactory.CreateSelfSignedEcPfxBase64();

            var (_, keyPem) = CertUtilities.ConvertPfxToCertAndKeyPem(pfx, TestCertFactory.Password);

            keyPem.Should().Contain("-----BEGIN PRIVATE KEY-----");
            keyPem.Should().NotContain("BEGIN EC PRIVATE KEY", "EC keys also use the PKCS#8 header convention");
        }

        [Fact]
        public void ConvertPfxToCertAndKeyPem_KeyMatchesCertificate()
        {
            var pfx = TestCertFactory.CreateSelfSignedRsaPfxBase64("CN=match-test");

            var (certPem, keyPem) = CertUtilities.ConvertPfxToCertAndKeyPem(pfx, TestCertFactory.Password);

            // CreateFromPem throws if the private key does not correspond to the certificate.
            using var rebuilt = X509Certificate2.CreateFromPem(certPem, keyPem);
            rebuilt.HasPrivateKey.Should().BeTrue();
            rebuilt.Subject.Should().Contain("match-test");
        }

        [Fact]
        public void ConvertPfxToCertAndKeyPem_IncludesChain_LeafFirst()
        {
            var pfx = TestCertFactory.CreateChainPfxBase64(out var leafSubject, out var rootSubject);

            var (certPem, _) = CertUtilities.ConvertPfxToCertAndKeyPem(pfx, TestCertFactory.Password);

            var certs = new X509Certificate2Collection();
            certs.ImportFromPem(certPem);

            certs.Count.Should().Be(2, "both the leaf and its issuer should be present in the certificate field");
            certs[0].Subject.Should().Be(leafSubject, "the leaf certificate must come first");
            certs[1].Subject.Should().Be(rootSubject, "the issuer must follow the leaf");
        }

        [Fact]
        public void ConvertPfxToCertAndKeyPem_InvalidBase64_Throws()
        {
            Action act = () => CertUtilities.ConvertPfxToCertAndKeyPem("not-base64!!!", TestCertFactory.Password);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ConvertPfxToCertAndKeyPem_NullOrEmpty_Throws()
        {
            Action act = () => CertUtilities.ConvertPfxToCertAndKeyPem("", TestCertFactory.Password);

            act.Should().Throw<ArgumentException>();
        }
    }
}
