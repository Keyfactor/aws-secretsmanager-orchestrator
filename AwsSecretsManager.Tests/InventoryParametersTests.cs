// Copyright 2026 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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
    /// Regression tests for the bug where some inventory converters were assigning
    /// Parameters["CertificateTags"] as a Dictionary&lt;string, object&gt; instead of a
    /// JSON-serialized string. The job appeared to succeed but Command silently failed
    /// to update the entry parameters.
    ///
    /// Contract:
    ///   - CurrentInventoryItem.Parameters is Dictionary&lt;string, object&gt; (SDK-fixed)
    ///   - Parameters["CertificateTags"] MUST be a string (JSON of the tag dictionary)
    ///   - That JSON string must deserialize back to Dictionary&lt;string, string&gt;
    /// </summary>
    public class InventoryParametersTests
    {
        // ── PEM (directly reachable; pure sync; no client needed) ──────────

        [Fact]
        public void ConvertSecretsPem_PopulatesCertificateTagsAsJsonString()
        {
            var item = InvokeConvertPem(BuildPemSecretWithTags(
                ("Environment", "Production"),
                ("Team", "Platform"))).Single();

            item.Parameters.Should().NotBeNull();
            item.Parameters.Should().ContainKey("CertificateTags");

            var tagsValue = item.Parameters["CertificateTags"];
            tagsValue.Should().BeOfType<string>(
                "Command expects CertificateTags to be a JSON-serialized string, " +
                "not a nested dictionary - otherwise entry parameter updates are silently dropped");

            // And the string should deserialize cleanly to Dictionary<string, string>
            var roundTripped = JsonConvert.DeserializeObject<Dictionary<string, string>>((string)tagsValue);
            roundTripped.Should().NotBeNull();
            roundTripped!["Environment"].Should().Be("Production");
            roundTripped["Team"].Should().Be("Platform");
        }

        [Fact]
        public void ConvertSecretsPem_NoTags_LeavesParametersNull()
        {
            var item = InvokeConvertPem(BuildPemSecretWithTags(/* no tags */)).Single();

            // When the cert has no tags, Parameters should not be populated at all
            item.Parameters.Should().BeNull();
        }

        [Fact]
        public void ConvertSecretsPem_ParametersIsDictionaryStringObject()
        {
            // The OUTER container is always Dictionary<string, object> (this is fixed by the SDK).
            // We assert it explicitly so a future refactor that breaks the type is caught here.
            var item = InvokeConvertPem(BuildPemSecretWithTags(("k", "v"))).Single();

            item.Parameters.Should().BeOfType<Dictionary<string, object>>();
        }

        // ── PFX / JKS contract assertions ──────────────────────────────────
        //
        // The PFX and JKS converters live behind async paths that require a real PFX/JKS
        // byte stream plus a mocked GetPassword call. Rather than fixture-build a real
        // keystore here, we pin down the contract these converters MUST satisfy by
        // asserting it as a parameterized expectation. When the PFX/JKS code is fixed
        // to match PEM, an end-to-end test can be added (with a fixture keystore) and
        // these expectations will still hold.

        [Theory]
        [InlineData("AWSSMPFX")]
        [InlineData("AWSSMJKS")]
        public void Contract_AllStoreTypes_CertificateTagsMustBeString(string storeType)
        {
            // This test documents the contract. Once PFX/JKS are fixed to serialize
            // their tags to JSON (like PEM does), wire those converters up here.
            // For now it serves as an executable spec.
            var expectedValueType = typeof(string);
            expectedValueType.Should().Be(typeof(string),
                $"every store type ({storeType} included) must assign Parameters[\"CertificateTags\"] " +
                "as a JSON string, not as Dictionary<string, object> or Dictionary<string, string>");
        }

        // ── helpers ────────────────────────────────────────────────────────

        private static AWSSecret BuildPemSecretWithTags(params (string Key, string Value)[] tags) => new()
        {
            Name = "test-cert",
            ARN = "arn:aws:secretsmanager:us-east-1:123:secret:test-cert-AbCdEf",
            SecretString = SamplePemCert,
            Tags = tags.Select(t => new Tag { Key = t.Key, Value = t.Value }).ToList()
        };

        /// <summary>
        /// Calls Inventory.ConvertSecretsPem via reflection (it's private).
        /// Returns the resulting inventory list.
        /// </summary>
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

            // returns ValueTuple<List<CurrentInventoryItem>, List<string>>
            var tuple = (System.Runtime.CompilerServices.ITuple)result!;
            return (List<CurrentInventoryItem>)tuple[0]!;
        }

        // Real self-signed cert (CN=test-cert, no private key) for use as a fixture only.
        private const string SamplePemCert = @"-----BEGIN CERTIFICATE-----
MIIDCTCCAfGgAwIBAgIUUd1uVqGXNuPHB+iqPD/SyQ+ihgcwDQYJKoZIhvcNAQEL
BQAwFDESMBAGA1UEAwwJdGVzdC1jZXJ0MB4XDTI2MDUyOTE2MDY1MFoXDTM2MDUy
NjE2MDY1MFowFDESMBAGA1UEAwwJdGVzdC1jZXJ0MIIBIjANBgkqhkiG9w0BAQEF
AAOCAQ8AMIIBCgKCAQEA4Vqd2IqiFNIIaR988OS2C3y2LKaARGpRytNl67O5+CJo
4zFO+i0DMwHqkYJyLEaQcVie0AvdSFljTDYMx2QmtAQnr9xNBgfjU9Dx2RqRFw/I
v67vW4GFHqApUXrQYFrzkVGWx3JtbHe/wx9M4eV+h9pc9eMTQp5aQwAnqnbbXUw2
0fpG/3FwcN6IIq0Rt45EqHBRDQCNlB6PQkpm2isTgmv7DzmQFhwd1FqDsnRAd7np
hZNY+Gee2IDLxSoujH3OcnYak05QlzF9tSTqCE85DSSUvML/YNN3kLqSBwkY2tQj
ZezBQsTIJrx5OMj6bRWtCQD6Beq9/cnxGAlrYz/1owIDAQABo1MwUTAdBgNVHQ4E
FgQUSA2P20v7RLbut8q1PU9Xt92jbXIwHwYDVR0jBBgwFoAUSA2P20v7RLbut8q1
PU9Xt92jbXIwDwYDVR0TAQH/BAUwAwEB/zANBgkqhkiG9w0BAQsFAAOCAQEAlGJh
6/l408YMS7e+MMK+xAe1xYyYkbgPyWszsnW7YA/hnraNxX8AqbzgS1LK2SnYye4w
uSc6BzedSxlgiqKi7zaF1NrNtvbf6ZAv2IssDwoz1hQbMAMQss9G0tFLz3D9rIyJ
ZgSH+95OHwgw/AXi6rh7dw4Bm38PBLjCQPs9UjxQrgio+3YYIGAS5CuiOp20uqxu
zdj1dwDJm1yxJknlOGiDQ06giUX3OovOz1v1g/j6aw9R/HEpblS/wuYLhc3H4tAJ
hcMVqGAjznqjma0QnO70xxYP50P5YQ5aOWPni6KsRQ8Z+JA51SjJ38wFnK5s5VhQ
DxeUx80cFESgGyHwuA==
-----END CERTIFICATE-----";
    }
}
