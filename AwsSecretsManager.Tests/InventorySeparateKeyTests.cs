// Copyright 2026 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Amazon.SecretsManager.Model;
using FluentAssertions;
using Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Jobs;
using Keyfactor.Extensions.Orchestrators.AwsSecretsManager.models;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Tests
{
    /// <summary>
    /// Verifies that inventory tolerates BOTH the legacy concatenated-PEM format and the
    /// new SeparatePrivateKey JSON format, so a format mismatch never drops a secret from
    /// the returned inventory set (which Command would otherwise interpret as a removal).
    /// </summary>
    public class InventorySeparateKeyTests
    {
        [Fact]
        public void ConvertSecretsPem_JsonSplitFormat_IsInventoried()
        {
            var secret = new AWSSecret
            {
                Name = "json-cert",
                ARN = "arn:aws:secretsmanager:us-east-1:123:secret:json-cert-AbCdEf",
                SecretString = BuildSeparateKeyJsonSecret("CN=json-cert"),
                Tags = new List<Tag>()
            };

            var items = InvokeConvertPem(secret);

            items.Should().HaveCount(1);
            items[0].Alias.Should().Be("json-cert");
            items[0].Certificates.Should().NotBeNullOrEmpty();
            items[0].PrivateKeyEntry.Should().BeTrue("the JSON document carries a private_key");
        }

        [Fact]
        public void ConvertSecretsPem_MixedFormats_BothReturned()
        {
            var jsonSecret = new AWSSecret
            {
                Name = "json-cert",
                SecretString = BuildSeparateKeyJsonSecret("CN=json-cert"),
                Tags = new List<Tag>()
            };
            var pemSecret = new AWSSecret
            {
                Name = "pem-cert",
                SecretString = BuildRawPemSecret("CN=pem-cert"),
                Tags = new List<Tag>()
            };

            var items = InvokeConvertPem(jsonSecret, pemSecret);

            items.Select(i => i.Alias).Should().BeEquivalentTo(new[] { "json-cert", "pem-cert" });
        }

        [Fact]
        public void ConvertSecretsPem_JsonSplitWithTags_SerializesTagsAsJsonString()
        {
            var secret = new AWSSecret
            {
                Name = "json-cert",
                SecretString = BuildSeparateKeyJsonSecret("CN=json-cert"),
                Tags = new List<Tag> { new Tag { Key = "Environment", Value = "Prod" } }
            };

            var item = InvokeConvertPem(secret).Single();

            item.Parameters.Should().ContainKey("CertificateTags");
            item.Parameters["CertificateTags"].Should().BeOfType<string>(
                "the CertificateTags regression contract still applies in the JSON format");

            var tags = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                (string)item.Parameters["CertificateTags"]);
            tags.Should().NotBeNull();
            tags!["Environment"].Should().Be("Prod");
        }

        // ── helpers ────────────────────────────────────────────────────────

        private static string BuildSeparateKeyJsonSecret(string subject)
        {
            var pfx = TestCertFactory.CreateSelfSignedRsaPfxBase64(subject);
            var (certPem, keyPem) = CertUtilities.ConvertPfxToCertAndKeyPem(pfx, TestCertFactory.Password);
            return JsonConvert.SerializeObject(new PemSecret { Certificate = certPem, PrivateKey = keyPem });
        }

        private static string BuildRawPemSecret(string subject)
        {
            // Build a legacy-style concatenated PEM (cert + key) without going through the
            // .NET/CNG export path, which can fail on Windows for keys imported from a PFX.
            var pfx = TestCertFactory.CreateSelfSignedRsaPfxBase64(subject);
            var (certPem, keyPem) = CertUtilities.ConvertPfxToCertAndKeyPem(pfx, TestCertFactory.Password);
            return certPem + "\n" + keyPem;
        }

        private static List<CurrentInventoryItem> InvokeConvertPem(params AWSSecret[] secrets)
        {
            var resolverMock = new Mock<IPAMSecretResolver>();
            resolverMock.Setup(r => r.Resolve(It.IsAny<string>())).Returns<string>(s => s);

            var inv = new TestableInventory(resolverMock.Object)
            {
                PublicJobParameters = new AwsSecretsManagerJobParameters { StoreType = "AWSSMPEM" }
            };

            var m = typeof(Inventory).GetMethod(
                "ConvertSecretsPem",
                BindingFlags.NonPublic | BindingFlags.Instance);
            m.Should().NotBeNull("ConvertSecretsPem must be reachable via reflection");

            var result = m!.Invoke(inv, new object[] { secrets.ToList() });

            var tuple = (System.Runtime.CompilerServices.ITuple)result!;
            return (List<CurrentInventoryItem>)tuple[0]!;
        }
    }
}
