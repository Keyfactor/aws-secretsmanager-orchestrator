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
    /// Verifies UpdateSecretTagsAsync merges tags: keys provided by Command replace existing
    /// values, while tags not provided (e.g. added by other tooling) are left on the secret.
    /// </summary>
    public class ClientUpdateSecretTagsTests
    {
        [Fact]
        public async Task UpdateSecretTags_OverlappingKey_UntagsOnlyOverlappingKeys()
        {
            var awsMock = MockAwsWithCurrentTags(
                new Tag { Key = "Environment", Value = "Dev" },
                new Tag { Key = "CostCenter", Value = "1234" }); // not managed by Command

            await InvokeUpdateSecretTags(awsMock.Object, "my-cert",
                new List<Tag> { new Tag { Key = "Environment", Value = "Prod" } });

            awsMock.Verify(c => c.UntagResourceAsync(
                It.Is<UntagResourceRequest>(r =>
                    r.SecretId == "my-cert" &&
                    r.TagKeys.Count == 1 &&
                    r.TagKeys[0] == "Environment"),
                It.IsAny<CancellationToken>()), Times.Once);

            awsMock.Verify(c => c.TagResourceAsync(
                It.Is<TagResourceRequest>(r =>
                    r.Tags.Count == 1 &&
                    r.Tags[0].Key == "Environment" &&
                    r.Tags[0].Value == "Prod"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateSecretTags_NoOverlap_DoesNotUntag()
        {
            var awsMock = MockAwsWithCurrentTags(new Tag { Key = "CostCenter", Value = "1234" });

            await InvokeUpdateSecretTags(awsMock.Object, "my-cert",
                new List<Tag> { new Tag { Key = "Environment", Value = "Prod" } });

            awsMock.Verify(c => c.UntagResourceAsync(
                It.IsAny<UntagResourceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
            awsMock.Verify(c => c.TagResourceAsync(
                It.IsAny<TagResourceRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateSecretTags_NeverUntagsKeysNotProvided()
        {
            var awsMock = MockAwsWithCurrentTags(
                new Tag { Key = "managedBy", Value = "Keyfactor" },
                new Tag { Key = "Environment", Value = "Dev" },
                new Tag { Key = "Owner", Value = "team-a" },
                new Tag { Key = "aws:cloudformation:stack-name", Value = "stack" });

            await InvokeUpdateSecretTags(awsMock.Object, "my-cert", new List<Tag>
            {
                new Tag { Key = "managedBy", Value = "Keyfactor" },
                new Tag { Key = "Environment", Value = "Prod" }
            });

            awsMock.Verify(c => c.UntagResourceAsync(
                It.Is<UntagResourceRequest>(r =>
                    r.TagKeys.OrderBy(k => k).SequenceEqual(new[] { "Environment", "managedBy" })),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── helpers ────────────────────────────────────────────────────────

        private static Mock<IAmazonSecretsManager> MockAwsWithCurrentTags(params Tag[] currentTags)
        {
            var awsMock = new Mock<IAmazonSecretsManager>();
            awsMock.Setup(c => c.DescribeSecretAsync(It.IsAny<DescribeSecretRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DescribeSecretResponse { Tags = currentTags.ToList() });
            awsMock.Setup(c => c.UntagResourceAsync(It.IsAny<UntagResourceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new UntagResourceResponse());
            awsMock.Setup(c => c.TagResourceAsync(It.IsAny<TagResourceRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TagResourceResponse());
            return awsMock;
        }

        private static Task InvokeUpdateSecretTags(IAmazonSecretsManager aws, string secretName, List<Tag> newTags)
        {
            var client = new AwsSecretsManagerClient();

            var prop = typeof(AwsSecretsManagerClient).GetProperty(
                "_secretsManagerClient", BindingFlags.NonPublic | BindingFlags.Instance);
            prop.Should().NotBeNull("the AWS client must be injectable via reflection");
            prop!.SetValue(client, aws);

            var m = typeof(AwsSecretsManagerClient).GetMethod(
                "UpdateSecretTagsAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            m.Should().NotBeNull("UpdateSecretTagsAsync must be reachable via reflection");

            return (Task)m!.Invoke(client, new object[] { secretName, newTags })!;
        }
    }
}
