// Copyright 2026 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Jobs;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Tests
{
    /// <summary>
    /// Verifies that every entry parameter the jobs read (EntryParameterKeys) is declared on each
    /// store type in integration-manifest.json.  Command only sends entry parameters that are
    /// defined on the store type, so a key read by the code but missing from the manifest is
    /// silently never populated.
    /// </summary>
    public class EntryParameterManifestTests
    {
        public static IEnumerable<object[]> StoreTypes =>
            new[] { "AWSSMPEM", "AWSSMPFX", "AWSSMJKS" }.Select(s => new object[] { s });

        public static IEnumerable<object[]> EntryParameterKeyNames =>
            typeof(EntryParameterKeys)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => new object[] { (string)f.GetRawConstantValue()! });

        [Theory]
        [MemberData(nameof(StoreTypes))]
        public void Manifest_DeclaresEveryEntryParameterKey(string storeType)
        {
            var declared = GetEntryParameters(storeType).Select(e => e["Name"]?.ToString()).ToList();
            var expected = EntryParameterKeyNames.Select(k => (string)k[0]).ToList();

            expected.Should().NotBeEmpty();
            declared.Should().Contain(expected, $"{storeType} must declare every entry parameter the jobs read");
        }

        [Theory]
        [MemberData(nameof(StoreTypes))]
        public void Manifest_KmsKeyId_IsOptionalString(string storeType)
        {
            var kms = GetEntryParameters(storeType).Single(e => e["Name"]?.ToString() == EntryParameterKeys.KMSKEYID);

            kms["Type"]!.ToString().Should().Be("string");
            kms["DefaultValue"]!.ToString().Should().BeEmpty();
            kms["RequiredWhen"]!.Children<JProperty>().Should().OnlyContain(p => p.Value.Value<bool>() == false,
                "an empty KmsKeyId falls back to the AWS managed key, so it must never be required");
        }

        [Fact]
        public void SetCertProperties_KmsKeyIdEntryParameter_IsApplied()
        {
            const string keyArn = "arn:aws:kms:us-east-1:123:key/abcd-1234";

            var jp = InvokeSetCertProperties(new Dictionary<string, object> { [EntryParameterKeys.KMSKEYID] = keyArn });

            jp.CertProperties.KmsKeyId.Should().Be(keyArn);
        }

        [Fact]
        public void SetCertProperties_NoKmsKeyIdEntryParameter_LeavesKmsKeyIdNull()
        {
            var jp = InvokeSetCertProperties(new Dictionary<string, object>());

            jp.CertProperties.KmsKeyId.Should().BeNull();
        }

        // ── helpers ────────────────────────────────────────────────────────

        private static List<JObject> GetEntryParameters(string storeType)
        {
            var manifest = JObject.Parse(File.ReadAllText(FindManifestPath()));
            var st = manifest.SelectTokens("$.about.orchestrator.store_types[*]")
                .OfType<JObject>()
                .SingleOrDefault(s => s["ShortName"]?.ToString() == storeType);

            st.Should().NotBeNull($"{storeType} must be defined in integration-manifest.json");
            return st!["EntryParameters"]!.Children<JObject>().ToList();
        }

        private static string FindManifestPath()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "integration-manifest.json");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new FileNotFoundException("integration-manifest.json was not found in any parent of the test output directory");
        }

        private static AwsSecretsManagerJobParameters InvokeSetCertProperties(Dictionary<string, object> jobProperties)
        {
            var resolverMock = new Mock<IPAMSecretResolver>();
            resolverMock.Setup(r => r.Resolve(It.IsAny<string>())).Returns<string>(s => s);

            var job = new TestableManagement(resolverMock.Object)
            {
                PublicJobParameters = new AwsSecretsManagerJobParameters { StoreType = "AWSSMPEM" }
            };

            var certProps = new ManagementJobCertificate
            {
                Alias = "kms-test",
                Contents = TestCertFactory.CreateSelfSignedRsaPfxBase64("CN=kms-test"),
                PrivateKeyPassword = TestCertFactory.Password
            };

            var m = typeof(JobBase<Management>).GetMethod(
                "SetCertProperties",
                BindingFlags.NonPublic | BindingFlags.Instance);
            m.Should().NotBeNull("SetCertProperties must be reachable via reflection");

            m!.Invoke(job, new object[] { jobProperties, certProps, false });

            return job.PublicJobParameters;
        }
    }
}
