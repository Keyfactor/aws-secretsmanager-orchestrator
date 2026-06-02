// Copyright 2026 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Moq;
using Xunit;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Tests
{
    public class ManagementTests
    {
        private const string Alias = "my-cert";
        private const long JobHistoryId = 42;
        private const string TestArn = "arn:aws:secretsmanager:us-east-1:123:secret:my-cert-AbCdEf";

        // ── helpers ────────────────────────────────────────────────────────

        private static TestableManagement BuildJob(
            Mock<AwsSecretsManagerClient> clientMock,
            string alias = Alias)
        {
            var resolverMock = new Mock<IPAMSecretResolver>();
            resolverMock.Setup(r => r.Resolve(It.IsAny<string>())).Returns<string>(s => s);

            var job = new TestableManagement(resolverMock.Object)
            {
                _secretsManagerClient = clientMock.Object,
                PublicJobParameters = new AwsSecretsManagerJobParameters
                {
                    StoreType = "AWSSMPEM",
                    JobHistoryId = JobHistoryId
                }
            };
            job.PublicJobParameters.CertProperties.Alias = alias;

            return job;
        }

        private static ManagementJobConfiguration BuildConfig(CertStoreOperationType op) => new()
        {
            OperationType = op,
            JobHistoryId = JobHistoryId,
            Capability = "CertStores.AWSSMPEM.Management",
            CertificateStoreDetails = new CertificateStore
            {
                StorePath = "us-east-1",
                ClientMachine = "arn:aws:iam::123:role/test",
                Properties = "{}"
            },
            JobCertificate = new ManagementJobCertificate { Alias = Alias }
        };

        // ── Add ─────────────────────────────────────────────────────────────

        [Fact]
        public void ProcessJob_Add_Success_ReturnsSuccess()
        {
            var clientMock = new Mock<AwsSecretsManagerClient>();
            clientMock
                .Setup(c => c.AddOrUpdateSecret(It.IsAny<AwsSecretsManagerJobParameters>()))
                .ReturnsAsync(TestArn);

            var job = BuildJob(clientMock);
            var result = job.ProcessJob(BuildConfig(CertStoreOperationType.Add));

            result.Should().NotBeNull();
            result.Result.Should().Be(OrchestratorJobStatusJobResult.Success);
            result.JobHistoryId.Should().Be(JobHistoryId);
            result.FailureMessage.Should().Contain(TestArn);
            clientMock.Verify(c => c.AddOrUpdateSecret(It.IsAny<AwsSecretsManagerJobParameters>()), Times.Once);
        }

        [Fact]
        public void ProcessJob_Add_ClientThrows_ReturnsFailure()
        {
            var clientMock = new Mock<AwsSecretsManagerClient>();
            clientMock
                .Setup(c => c.AddOrUpdateSecret(It.IsAny<AwsSecretsManagerJobParameters>()))
                .ThrowsAsync(new InvalidOperationException("AWS exploded"));

            var job = BuildJob(clientMock);
            var result = job.ProcessJob(BuildConfig(CertStoreOperationType.Add));

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Failure);
            result.FailureMessage.Should().Contain("AWS exploded");
        }

        // ── Remove ─────────────────────────────────────────────────────────

        [Fact]
        public void ProcessJob_Remove_Success_NoPasswordSecret_ReturnsSuccess()
        {
            var clientMock = new Mock<AwsSecretsManagerClient>();
            clientMock
                .Setup(c => c.RemoveSecret(It.IsAny<string>()))
                .ReturnsAsync((TestArn, string.Empty));

            var job = BuildJob(clientMock);
            // For Remove we need the SecretName resolved from the alias
            job.PublicJobParameters.CertProperties.Alias = Alias;

            var result = job.ProcessJob(BuildConfig(CertStoreOperationType.Remove));

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Success);
            result.FailureMessage.Should().Contain(TestArn);
            result.FailureMessage.Should().NotContain("password secret");
        }

        [Fact]
        public void ProcessJob_Remove_Success_WithPasswordSecret_ReportsBothArns()
        {
            const string pwdArn = "arn:aws:secretsmanager:us-east-1:123:secret:my-cert-pw-XyZ";
            var clientMock = new Mock<AwsSecretsManagerClient>();
            clientMock
                .Setup(c => c.RemoveSecret(It.IsAny<string>()))
                .ReturnsAsync((TestArn, pwdArn));

            var job = BuildJob(clientMock);
            var result = job.ProcessJob(BuildConfig(CertStoreOperationType.Remove));

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Success);
            result.FailureMessage.Should().Contain(TestArn).And.Contain(pwdArn);
        }

        [Fact]
        public void ProcessJob_Remove_ClientThrows_ReturnsFailure()
        {
            var clientMock = new Mock<AwsSecretsManagerClient>();
            clientMock
                .Setup(c => c.RemoveSecret(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("secret not found"));

            var job = BuildJob(clientMock);
            var result = job.ProcessJob(BuildConfig(CertStoreOperationType.Remove));

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Failure);
            result.FailureMessage.Should().Contain("secret not found");
        }

        // ── Unsupported operations ────────────────────────────────────────

        [Theory]
        [InlineData(CertStoreOperationType.Create)]
        [InlineData(CertStoreOperationType.Reenrollment)]
        [InlineData(CertStoreOperationType.CreateAdd)]
        public void ProcessJob_UnsupportedOperation_ReturnsFailure(CertStoreOperationType op)
        {
            var clientMock = new Mock<AwsSecretsManagerClient>();
            var job = BuildJob(clientMock);

            var result = job.ProcessJob(BuildConfig(op));

            result.Result.Should().Be(OrchestratorJobStatusJobResult.Failure);
            result.FailureMessage.Should().Contain("Unsupported operation");

            clientMock.Verify(c => c.AddOrUpdateSecret(It.IsAny<AwsSecretsManagerJobParameters>()), Times.Never);
            clientMock.Verify(c => c.RemoveSecret(It.IsAny<string>()), Times.Never);
        }
    }
}
