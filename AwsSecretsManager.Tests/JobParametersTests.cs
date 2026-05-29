// Copyright 2026 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using FluentAssertions;
using Xunit;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Tests
{
    public class JobParametersTests
    {
        // ── SecretName resolution ──────────────────────────────────────────

        [Fact]
        public void SecretName_NoPrefix_ReturnsAliasOnly()
        {
            var p = new AwsSecretsManagerJobParameters();
            p.CertProperties.Alias = "my-cert";

            p.SecretName.Should().Be("my-cert");
        }

        [Fact]
        public void SecretName_WithPrefix_ConcatenatesPrefixAndAlias()
        {
            var p = new AwsSecretsManagerJobParameters();
            p.StoreProperties.NamePrefix = "dev/web/";
            p.CertProperties.Alias = "my-cert";

            p.SecretName.Should().Be("dev/web/my-cert");
        }

        [Fact]
        public void SecretName_EmptyPrefix_ReturnsAliasOnly()
        {
            var p = new AwsSecretsManagerJobParameters();
            p.StoreProperties.NamePrefix = string.Empty;
            p.CertProperties.Alias = "my-cert";

            p.SecretName.Should().Be("my-cert");
        }

        // ── CertStoreProperties flags ──────────────────────────────────────

        [Fact]
        public void UseTags_TagNameSet_ReturnsTrue()
        {
            var sp = new CertStoreProperties { TagName = "managedBy" };
            sp.UseTags.Should().BeTrue();
        }

        [Fact]
        public void UseTags_TagNameNullOrEmpty_ReturnsFalse()
        {
            new CertStoreProperties { TagName = null }.UseTags.Should().BeFalse();
            new CertStoreProperties { TagName = "" }.UseTags.Should().BeFalse();
        }

        [Fact]
        public void UsePrefix_PrefixSet_ReturnsTrue()
        {
            var sp = new CertStoreProperties { NamePrefix = "dev/" };
            sp.UsePrefix.Should().BeTrue();
        }

        [Fact]
        public void UsePrefix_PrefixNullOrEmpty_ReturnsFalse()
        {
            new CertStoreProperties { NamePrefix = null }.UsePrefix.Should().BeFalse();
            new CertStoreProperties { NamePrefix = "" }.UsePrefix.Should().BeFalse();
        }

        // ── ctor defaults ──────────────────────────────────────────────────

        [Fact]
        public void Constructor_InitializesNestedProperties()
        {
            var p = new AwsSecretsManagerJobParameters();

            p.StoreProperties.Should().NotBeNull();
            p.CertProperties.Should().NotBeNull();
            p.CertProperties.ReplicaRegions.Should().NotBeNull().And.BeEmpty();
            p.CertProperties.Tags.Should().NotBeNull().And.BeEmpty();
        }
    }
}
