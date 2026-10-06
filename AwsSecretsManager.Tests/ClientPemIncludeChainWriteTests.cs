// Copyright 2026 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Amazon.SecretsManager.Model;
using FluentAssertions;
using Xunit;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Tests
{
    /// <summary>
    /// Exercises the client's single-PEM write generators (AWSSMPEM without SeparatePrivateKey)
    /// to verify the IncludeChain store property controls whether the issuer chain is written.
    /// The generate methods are private and AWS-free, so they are invoked via reflection.
    /// </summary>
    public class ClientPemIncludeChainWriteTests
    {
        [Theory]
        [InlineData("GenerateAddSecretPemRequest")]
        [InlineData("GenerateUpdateSecretPemRequest")]
        public void PemRequest_IncludeChainFalse_WritesLeafOnly(string methodName)
        {
            var jp = BuildJobParameters(includeChain: false, out var leafSubject, out _);

            var secretString = GetSecretString(methodName, jp);

            var certs = new X509Certificate2Collection();
            certs.ImportFromPem(secretString);
            certs.Count.Should().Be(1, "the default format is unchanged: leaf and key only");
            certs[0].Subject.Should().Be(leafSubject);
            secretString.Should().Contain("-----BEGIN PRIVATE KEY-----");
        }

        [Theory]
        [InlineData("GenerateAddSecretPemRequest")]
        [InlineData("GenerateUpdateSecretPemRequest")]
        public void PemRequest_IncludeChainTrue_WritesLeafAndChain(string methodName)
        {
            var jp = BuildJobParameters(includeChain: true, out var leafSubject, out var rootSubject);

            var secretString = GetSecretString(methodName, jp);

            var certs = new X509Certificate2Collection();
            certs.ImportFromPem(secretString);
            certs.Count.Should().Be(2);
            certs[0].Subject.Should().Be(leafSubject);
            certs[1].Subject.Should().Be(rootSubject);
            secretString.Should().Contain("-----BEGIN PRIVATE KEY-----");
        }

        // ── helpers ────────────────────────────────────────────────────────

        private static AwsSecretsManagerJobParameters BuildJobParameters(bool includeChain, out string leafSubject, out string rootSubject)
        {
            var jp = new AwsSecretsManagerJobParameters { StoreType = "AWSSMPEM" };
            jp.StoreProperties.IncludeChain = includeChain;
            jp.CertProperties.Alias = "chain-write-test";
            jp.CertProperties.Contents = TestCertFactory.CreateChainPfxBase64(out leafSubject, out rootSubject);
            jp.CertProperties.PrivateKeyPassword = TestCertFactory.Password;
            return jp;
        }

        private static string GetSecretString(string methodName, AwsSecretsManagerJobParameters jp)
        {
            var client = new AwsSecretsManagerClient();
            var m = typeof(AwsSecretsManagerClient).GetMethod(
                methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            m.Should().NotBeNull($"{methodName} must be reachable via reflection");

            var result = m!.Invoke(client, new object[] { jp });
            return result switch
            {
                CreateSecretRequest c => c.SecretString,
                UpdateSecretRequest u => u.SecretString,
                _ => throw new System.InvalidOperationException($"unexpected return type from {methodName}")
            };
        }
    }
}
