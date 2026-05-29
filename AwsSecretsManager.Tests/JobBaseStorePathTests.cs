// Copyright 2026 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

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
    /// Exercises the private ParseStorePath logic via SetStoreProperties.
    /// SetStoreProperties is private protected; reachable from this assembly via reflection.
    /// </summary>
    public class JobBaseStorePathTests
    {
        private static (CertStoreProperties storeProps, AwsSecretsManagerJobParameters jobParams)
            InvokeSetStoreProperties(string storePath, string clientMachine = "arn:aws:iam::123:role/test")
        {
            var resolverMock = new Mock<IPAMSecretResolver>();
            resolverMock.Setup(r => r.Resolve(It.IsAny<string>())).Returns<string>(s => s);

            var job = new TestableManagement(resolverMock.Object)
            {
                PublicJobParameters = new AwsSecretsManagerJobParameters()
            };

            var certStore = new CertificateStore
            {
                StorePath = storePath,
                ClientMachine = clientMachine,
                Properties = "{}"
            };

            var m = typeof(JobBase<Management>).GetMethod(
                "SetStoreProperties",
                BindingFlags.NonPublic | BindingFlags.Instance);
            m.Should().NotBeNull("SetStoreProperties must be reachable via reflection");

            m!.Invoke(job, new object[] { certStore });

            return (job.PublicJobParameters.StoreProperties, job.PublicJobParameters);
        }

        // ── Region-only paths ──────────────────────────────────────────────

        [Fact]
        public void ParseStorePath_RegionOnly_SetsRegionAndNoFilters()
        {
            var (sp, _) = InvokeSetStoreProperties("us-east-1");

            sp.AwsRegion.Should().Be("us-east-1");
            sp.NamePrefix.Should().BeNullOrEmpty();
            sp.TagName.Should().BeNullOrEmpty();
            sp.TagValue.Should().BeNullOrEmpty();
            sp.UseTags.Should().BeFalse();
            sp.UsePrefix.Should().BeFalse();
        }

        // ── Prefix-only paths ──────────────────────────────────────────────

        [Fact]
        public void ParseStorePath_WithPrefix_ExtractsPrefixAndRegion()
        {
            var (sp, _) = InvokeSetStoreProperties("us-east-2 [prefix=\"dev/testing/\"]");

            sp.AwsRegion.Should().Be("us-east-2");
            sp.NamePrefix.Should().Be("dev/testing/");
            sp.UsePrefix.Should().BeTrue();
            sp.UseTags.Should().BeFalse();
        }

        // ── Tag-only paths ─────────────────────────────────────────────────

        [Fact]
        public void ParseStorePath_TagNameOnly_SetsTagNameAndNoValue()
        {
            var (sp, _) = InvokeSetStoreProperties("us-west-1 [tagName=\"managedBy\"]");

            sp.AwsRegion.Should().Be("us-west-1");
            sp.TagName.Should().Be("managedBy");
            sp.TagValue.Should().BeNullOrEmpty();
            sp.UseTags.Should().BeTrue();
        }

        [Fact]
        public void ParseStorePath_TagNameAndValue_SetsBoth()
        {
            var (sp, _) = InvokeSetStoreProperties(
                "us-east-1 [tagName=\"managedBy\" tagValue=\"Keyfactor\"]");

            sp.TagName.Should().Be("managedBy");
            sp.TagValue.Should().Be("Keyfactor");
            sp.UseTags.Should().BeTrue();
        }

        // ── Combined prefix + tags ─────────────────────────────────────────

        [Fact]
        public void ParseStorePath_PrefixAndTags_ExtractsAll()
        {
            var (sp, _) = InvokeSetStoreProperties(
                "us-east-1 [prefix=\"web/certs/\" tagName=\"managedBy\" tagValue=\"Keyfactor\"]");

            sp.AwsRegion.Should().Be("us-east-1");
            sp.NamePrefix.Should().Be("web/certs/");
            sp.TagName.Should().Be("managedBy");
            sp.TagValue.Should().Be("Keyfactor");
            sp.UsePrefix.Should().BeTrue();
            sp.UseTags.Should().BeTrue();
        }

        // ── Edge cases ─────────────────────────────────────────────────────

        [Theory]
        [InlineData("us-east-1 [prefix=\"/\"]")]
        public void ParseStorePath_PrefixIsJustSlash_DoesNotSetPrefix(string storePath)
        {
            var (sp, _) = InvokeSetStoreProperties(storePath);

            // "/" alone is treated as "no prefix" so we don't filter
            sp.NamePrefix.Should().BeNullOrEmpty();
            sp.UsePrefix.Should().BeFalse();
        }
    }
}
