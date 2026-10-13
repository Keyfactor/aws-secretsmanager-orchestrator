// Copyright 2026 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using FluentAssertions;
using Moq;
using Xunit;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Tests
{
    /// <summary>
    /// Verifies that for JKS / PFX stores the password secret ("&lt;alias&gt;-pw") is written with the
    /// same KMS key and replica regions as the cert store secret, and that its value is updated
    /// when an existing cert store secret is overwritten.
    /// </summary>
    public class ClientPasswordSecretTests
    {
        private const string Alias = "my-cert";
        private const string PwdSecretName = Alias + "-pw";
        private const string KmsKeyId = "arn:aws:kms:us-east-1:123:key/abcd-1234";

        // ── Add ─────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("AWSSMPFX")]
        [InlineData("AWSSMJKS")]
        public async Task Add_WithKmsKeyAndReplicas_AppliesBothToPasswordSecret(string storeType)
        {
            var awsMock = MockAws(secretExists: false);
            var jp = BuildJobParameters(storeType, KmsKeyId, "us-west-2");

            await BuildClient(awsMock.Object).AddOrUpdateSecret(jp);

            awsMock.Verify(c => c.CreateSecretAsync(
                It.Is<CreateSecretRequest>(r =>
                    r.Name == PwdSecretName &&
                    r.SecretString == TestCertFactory.Password &&
                    r.KmsKeyId == KmsKeyId &&
                    r.AddReplicaRegions.Count == 1 &&
                    r.AddReplicaRegions[0].Region == "us-west-2"),
                It.IsAny<CancellationToken>()), Times.Once);

            awsMock.Verify(c => c.CreateSecretAsync(
                It.Is<CreateSecretRequest>(r => r.Name == Alias && r.KmsKeyId == KmsKeyId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Add_WithoutKmsKey_LeavesPasswordSecretOnDefaultKey()
        {
            var awsMock = MockAws(secretExists: false);
            var jp = BuildJobParameters("AWSSMPFX", kmsKeyId: null);

            await BuildClient(awsMock.Object).AddOrUpdateSecret(jp);

            awsMock.Verify(c => c.CreateSecretAsync(
                It.Is<CreateSecretRequest>(r => r.Name == PwdSecretName && string.IsNullOrEmpty(r.KmsKeyId)),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Add_WithIdentificationTag_DoesNotTagPasswordSecretForInventory()
        {
            var awsMock = MockAws(secretExists: false);
            var jp = BuildJobParameters("AWSSMPFX", KmsKeyId);
            jp.StoreProperties.TagName = "managedBy";
            jp.StoreProperties.TagValue = "Keyfactor";

            await BuildClient(awsMock.Object).AddOrUpdateSecret(jp);

            awsMock.Verify(c => c.CreateSecretAsync(
                It.Is<CreateSecretRequest>(r => r.Name == PwdSecretName && r.Tags.All(t => t.Key != "managedBy")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── Update ──────────────────────────────────────────────────────────

        [Theory]
        [InlineData("AWSSMPFX")]
        [InlineData("AWSSMJKS")]
        public async Task Update_WritesNewPasswordWithKmsKey(string storeType)
        {
            var awsMock = MockAws(secretExists: true);
            var jp = BuildJobParameters(storeType, KmsKeyId);

            await BuildClient(awsMock.Object).AddOrUpdateSecret(jp);

            awsMock.Verify(c => c.UpdateSecretAsync(
                It.Is<UpdateSecretRequest>(r =>
                    r.SecretId == PwdSecretName &&
                    r.SecretString == TestCertFactory.Password &&
                    r.KmsKeyId == KmsKeyId),
                It.IsAny<CancellationToken>()), Times.Once);

            awsMock.Verify(c => c.UpdateSecretAsync(
                It.Is<UpdateSecretRequest>(r => r.SecretId == Alias && r.KmsKeyId == KmsKeyId),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Update_WithReplicas_ReplicatesPasswordSecret()
        {
            var awsMock = MockAws(secretExists: true);
            var jp = BuildJobParameters("AWSSMPFX", KmsKeyId, "us-west-2");

            await BuildClient(awsMock.Object).AddOrUpdateSecret(jp);

            awsMock.Verify(c => c.ReplicateSecretToRegionsAsync(
                It.Is<ReplicateSecretToRegionsRequest>(r =>
                    r.SecretId == PwdSecretName &&
                    r.AddReplicaRegions.Single().Region == "us-west-2"),
                It.IsAny<CancellationToken>()), Times.Once);

            awsMock.Verify(c => c.ReplicateSecretToRegionsAsync(
                It.Is<ReplicateSecretToRegionsRequest>(r => r.SecretId == Alias),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Update_Pem_DoesNotTouchPasswordSecret()
        {
            var awsMock = MockAws(secretExists: true);
            var jp = BuildJobParameters("AWSSMPEM", KmsKeyId);

            await BuildClient(awsMock.Object).AddOrUpdateSecret(jp);

            awsMock.Verify(c => c.UpdateSecretAsync(
                It.Is<UpdateSecretRequest>(r => r.SecretId == PwdSecretName),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        // ── helpers ────────────────────────────────────────────────────────

        private static AwsSecretsManagerJobParameters BuildJobParameters(string storeType, string? kmsKeyId, params string[] replicaRegions)
        {
            var jp = new AwsSecretsManagerJobParameters { StoreType = storeType };
            jp.CertProperties.Alias = Alias;
            jp.CertProperties.Overwrite = true;
            jp.CertProperties.KmsKeyId = kmsKeyId;
            jp.CertProperties.Contents = TestCertFactory.CreateSelfSignedRsaPfxBase64("CN=" + Alias);
            jp.CertProperties.PrivateKeyPassword = TestCertFactory.Password;
            jp.CertProperties.ReplicaRegions = replicaRegions.Select(r => new ReplicaRegionType { Region = r }).ToList();
            return jp;
        }

        private static Mock<IAmazonSecretsManager> MockAws(bool secretExists)
        {
            var awsMock = new Mock<IAmazonSecretsManager>();
            awsMock.Setup(c => c.ListSecretsAsync(It.IsAny<ListSecretsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ListSecretsResponse
                {
                    SecretList = secretExists
                        ? new List<SecretListEntry> { new SecretListEntry { Name = Alias } }
                        : new List<SecretListEntry>()
                });
            awsMock.Setup(c => c.CreateSecretAsync(It.IsAny<CreateSecretRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CreateSecretResponse());
            awsMock.Setup(c => c.UpdateSecretAsync(It.IsAny<UpdateSecretRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UpdateSecretResponse());
            awsMock.Setup(c => c.DescribeSecretAsync(It.IsAny<DescribeSecretRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DescribeSecretResponse { Tags = new List<Tag>(), ReplicationStatus = new List<ReplicationStatusType>() });
            awsMock.Setup(c => c.TagResourceAsync(It.IsAny<TagResourceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TagResourceResponse());
            awsMock.Setup(c => c.ReplicateSecretToRegionsAsync(It.IsAny<ReplicateSecretToRegionsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ReplicateSecretToRegionsResponse());
            return awsMock;
        }

        private static AwsSecretsManagerClient BuildClient(IAmazonSecretsManager aws)
        {
            var client = new AwsSecretsManagerClient();
            var prop = typeof(AwsSecretsManagerClient).GetProperty(
                "_secretsManagerClient", BindingFlags.NonPublic | BindingFlags.Instance);
            prop.Should().NotBeNull("the AWS client must be injectable via reflection");
            prop!.SetValue(client, aws);
            return client;
        }
    }
}
