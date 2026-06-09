// Copyright 2026 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System.Reflection;
using Amazon.SecretsManager.Model;
using FluentAssertions;
using Keyfactor.Extensions.Orchestrators.AwsSecretsManager.models;
using Newtonsoft.Json;
using Xunit;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Tests
{
    /// <summary>
    /// Exercises the client's PEM-JSON write generators (used when AWSSMPEM has
    /// SeparatePrivateKey enabled). The generate methods are private and AWS-free, so they
    /// are invoked via reflection; the resulting SecretString is validated as the expected
    /// JSON document.
    /// </summary>
    public class ClientPemJsonWriteTests
    {
        [Fact]
        public void GenerateAddSecretPemJsonRequest_ProducesJsonSecretWithBothFields()
        {
            var jp = BuildJobParameters();

            var req = InvokeGenerate<CreateSecretRequest>("GenerateAddSecretPemJsonRequest", jp);

            req.Name.Should().Be("write-test");
            AssertValidPemSecretJson(req.SecretString);
        }

        [Fact]
        public void GenerateUpdateSecretPemJsonRequest_ProducesJsonSecretWithBothFields()
        {
            var jp = BuildJobParameters();

            var req = InvokeGenerate<UpdateSecretRequest>("GenerateUpdateSecretPemJsonRequest", jp);

            req.SecretId.Should().Be("write-test");
            AssertValidPemSecretJson(req.SecretString);
        }

        // ── helpers ────────────────────────────────────────────────────────

        private static AwsSecretsManagerJobParameters BuildJobParameters()
        {
            var jp = new AwsSecretsManagerJobParameters { StoreType = "AWSSMPEM" };
            jp.StoreProperties.SeparatePrivateKey = true;
            jp.CertProperties.Alias = "write-test";
            jp.CertProperties.Contents = TestCertFactory.CreateSelfSignedRsaPfxBase64("CN=write-test");
            jp.CertProperties.PrivateKeyPassword = TestCertFactory.Password;
            return jp;
        }

        private static T InvokeGenerate<T>(string methodName, AwsSecretsManagerJobParameters jp)
        {
            var client = new AwsSecretsManagerClient();
            var m = typeof(AwsSecretsManagerClient).GetMethod(
                methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            m.Should().NotBeNull($"{methodName} must be reachable via reflection");
            return (T)m!.Invoke(client, new object[] { jp })!;
        }

        private static void AssertValidPemSecretJson(string secretString)
        {
            var parsed = JsonConvert.DeserializeObject<PemSecret>(secretString);
            parsed.Should().NotBeNull();
            parsed!.Certificate.Should().Contain("-----BEGIN CERTIFICATE-----");
            parsed.PrivateKey.Should().Contain("-----BEGIN PRIVATE KEY-----");
        }
    }
}
