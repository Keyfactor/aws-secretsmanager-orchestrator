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
    /// Verifies GetSecrets chunks SecretIdList to AWS's 20-item-per-call limit on
    /// BatchGetSecretValue, instead of sending every matched secret name in one request.
    /// </summary>
    public class ClientGetSecretsBatchingTests
    {
        [Fact]
        public async Task GetSecrets_MoreThan20Matches_ChunksBatchGetSecretValueCalls()
        {
            var secretNames = Enumerable.Range(1, 25).Select(i => $"secret-{i}").ToList();
            var capturedRequests = new List<BatchGetSecretValueRequest>();

            var awsMock = new Mock<IAmazonSecretsManager>();
            awsMock.Setup(c => c.ListSecretsAsync(It.IsAny<ListSecretsRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ListSecretsResponse
                {
                    SecretList = secretNames.Select(n => new SecretListEntry { Name = n }).ToList()
                });
            awsMock.Setup(c => c.BatchGetSecretValueAsync(It.IsAny<BatchGetSecretValueRequest>(), It.IsAny<CancellationToken>()))
                .Callback<BatchGetSecretValueRequest, CancellationToken>((req, _) => capturedRequests.Add(req))
                .ReturnsAsync((BatchGetSecretValueRequest req, CancellationToken _) => new BatchGetSecretValueResponse
                {
                    SecretValues = req.SecretIdList
                        .Select(id => new SecretValueEntry { Name = id, SecretString = "dummy" })
                        .ToList()
                });
            awsMock.Setup(c => c.DescribeSecretAsync(It.IsAny<DescribeSecretRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DescribeSecretResponse { Tags = new List<Tag>() });

            var client = new AwsSecretsManagerClient();
            var prop = typeof(AwsSecretsManagerClient).GetProperty(
                "_secretsManagerClient", BindingFlags.NonPublic | BindingFlags.Instance);
            prop.Should().NotBeNull("the AWS client must be injectable via reflection");
            prop!.SetValue(client, awsMock.Object);

            var results = await client.GetSecrets(new List<Filter>());

            results.Select(r => r.Name).Should().BeEquivalentTo(secretNames);

            capturedRequests.Should().HaveCount(2, "25 secrets must be split across two calls of at most 20 each");
            capturedRequests.Should().OnlyContain(r => r.SecretIdList.Count <= 20,
                "BatchGetSecretValue rejects SecretIdList with more than 20 entries");
            capturedRequests.Sum(r => r.SecretIdList.Count).Should().Be(25);
        }
    }
}
