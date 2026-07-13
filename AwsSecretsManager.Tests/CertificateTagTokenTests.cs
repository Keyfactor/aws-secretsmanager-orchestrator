// Copyright 2026 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using FluentAssertions;
using Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Jobs;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Moq;
using Xunit;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Tests
{
    /// <summary>
    /// Tests for the CertificateTags placeholder tokens (%SERIAL_NUMBER%, %NOT_BEFORE%,
    /// %NOT_AFTER%): the certificate-evaluated values, the pure substitution, and the
    /// end-to-end wiring through SetCertProperties.
    /// </summary>
    public class CertificateTagTokenTests
    {
        private const string IsoUtc = "yyyy-MM-ddTHH:mm:ssZ";

        // ── GetCertificateTagTokenValues (certificate evaluation) ──────────

        [Fact]
        public void GetCertificateTagTokenValues_ReturnsSerialAndIsoDates()
        {
            var (pfx, serial, notBefore, notAfter) = TestCertFactory.CreateRsaPfxWithDetails();

            var tokens = CertUtilities.GetCertificateTagTokenValues(pfx, TestCertFactory.Password);

            tokens[CertificateTagTokens.SERIAL_NUMBER].Should().Be(serial);
            tokens[CertificateTagTokens.NOT_BEFORE].Should().Be(notBefore.ToString(IsoUtc, CultureInfo.InvariantCulture));
            tokens[CertificateTagTokens.NOT_AFTER].Should().Be(notAfter.ToString(IsoUtc, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void GetCertificateTagTokenValues_DatesAreIso8601Utc()
        {
            var (pfx, _, _, _) = TestCertFactory.CreateRsaPfxWithDetails();

            var tokens = CertUtilities.GetCertificateTagTokenValues(pfx, TestCertFactory.Password);

            const string isoUtc = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$";
            tokens[CertificateTagTokens.NOT_BEFORE].Should().MatchRegex(isoUtc);
            tokens[CertificateTagTokens.NOT_AFTER].Should().MatchRegex(isoUtc);
        }

        // ── ApplyTagTokens (pure substitution) ─────────────────────────────

        [Fact]
        public void ApplyTagTokens_ReplacesTokensInValuesOnly()
        {
            var tokenValues = new Dictionary<string, string>
            {
                [CertificateTagTokens.SERIAL_NUMBER] = "00AABBCCDD",
                [CertificateTagTokens.NOT_BEFORE] = "2026-01-01T00:00:00Z",
                [CertificateTagTokens.NOT_AFTER] = "2027-01-01T00:00:00Z",
            };
            var tags = new Dictionary<string, string>
            {
                ["serial"] = "%SERIAL_NUMBER%",
                ["window"] = "valid %NOT_BEFORE% through %NOT_AFTER%",
                ["static"] = "no-tokens-here",
                ["empty"] = "",
            };

            var result = CertUtilities.ApplyTagTokens(tags, tokenValues);

            result["serial"].Should().Be("00AABBCCDD");
            result["window"].Should().Be("valid 2026-01-01T00:00:00Z through 2027-01-01T00:00:00Z");
            result["static"].Should().Be("no-tokens-here");
            result["empty"].Should().Be("");
        }

        [Fact]
        public void ApplyTagTokens_LeavesKeysUntouched()
        {
            var tokenValues = new Dictionary<string, string> { [CertificateTagTokens.SERIAL_NUMBER] = "DEADBEEF" };
            // A token sitting in the KEY must not be substituted; only values are resolved.
            var tags = new Dictionary<string, string> { ["%SERIAL_NUMBER%"] = "literal" };

            var result = CertUtilities.ApplyTagTokens(tags, tokenValues);

            result.Should().ContainKey("%SERIAL_NUMBER%");
            result["%SERIAL_NUMBER%"].Should().Be("literal");
        }

        // ── End-to-end through SetCertProperties ───────────────────────────

        [Fact]
        public void SetCertProperties_ResolvesPlaceholdersIntoTags()
        {
            var (pfx, serial, notBefore, _) = TestCertFactory.CreateRsaPfxWithDetails();

            var tags = InvokeSetCertProperties(
                certificateTagsJson: "{\"serial\":\"%SERIAL_NUMBER%\",\"valid_from\":\"%NOT_BEFORE%\"}",
                contents: pfx);

            tags["serial"].Should().Be(serial);
            tags["valid_from"].Should().Be(notBefore.ToString(IsoUtc, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void SetCertProperties_NoTokens_LeavesTagsUnchanged()
        {
            var (pfx, _, _, _) = TestCertFactory.CreateRsaPfxWithDetails();

            var tags = InvokeSetCertProperties(
                certificateTagsJson: "{\"environment\":\"production\"}",
                contents: pfx);

            tags["environment"].Should().Be("production");
        }

        [Fact]
        public void SetCertProperties_InvalidTagJson_ThrowsClearError()
        {
            var (pfx, _, _, _) = TestCertFactory.CreateRsaPfxWithDetails();

            // Extra trailing brace - the exact shape that produced the raw, unhelpful failure.
            Action act = () => InvokeSetCertProperties(
                certificateTagsJson: "{\"sn\":\"%SERIAL_NUMBER%\"}}",
                contents: pfx);

            act.Should().Throw<TargetInvocationException>()
                .WithInnerException<InvalidOperationException>()
                .WithMessage("*CertificateTags*");
        }

        [Fact]
        public void SetCertProperties_InvalidReplicaRegionsJson_ThrowsClearError()
        {
            var (pfx, _, _, _) = TestCertFactory.CreateRsaPfxWithDetails();

            Action act = () => InvokeSetCertProperties(
                certificateTagsJson: "{}",
                contents: pfx,
                replicaRegionsJson: "{\"Region\":\"us-west-2\"}}"); // extra brace

            act.Should().Throw<TargetInvocationException>()
                .WithInnerException<InvalidOperationException>()
                .WithMessage("*ReplicaRegions*");
        }

        // ── helpers ────────────────────────────────────────────────────────

        private static Dictionary<string, string> InvokeSetCertProperties(string certificateTagsJson, string contents, string replicaRegionsJson = null)
        {
            var resolverMock = new Mock<IPAMSecretResolver>();
            resolverMock.Setup(r => r.Resolve(It.IsAny<string>())).Returns<string>(s => s);

            var job = new TestableManagement(resolverMock.Object)
            {
                PublicJobParameters = new AwsSecretsManagerJobParameters { StoreType = "AWSSMPEM" }
            };

            var jobProperties = new Dictionary<string, object>
            {
                ["CertificateTags"] = certificateTagsJson
            };
            if (replicaRegionsJson != null)
            {
                jobProperties["ReplicaRegions"] = replicaRegionsJson;
            }

            var certProps = new ManagementJobCertificate
            {
                Alias = "token-test",
                Contents = contents,
                PrivateKeyPassword = TestCertFactory.Password
            };

            var m = typeof(JobBase<Management>).GetMethod(
                "SetCertProperties",
                BindingFlags.NonPublic | BindingFlags.Instance);
            m.Should().NotBeNull("SetCertProperties must be reachable via reflection");

            m!.Invoke(job, new object[] { jobProperties, certProps, false });

            return job.PublicJobParameters.CertProperties.Tags;
        }
    }
}
