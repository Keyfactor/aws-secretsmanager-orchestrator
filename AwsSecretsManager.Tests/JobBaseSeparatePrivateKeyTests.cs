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
    /// Verifies that the SeparatePrivateKey store-type custom field is read out of the
    /// serialized store Properties JSON, tolerant of the various shapes Command may use
    /// to serialize a boolean custom field.
    /// </summary>
    public class JobBaseSeparatePrivateKeyTests
    {
        private static CertStoreProperties InvokeSetStoreProperties(string propertiesJson)
        {
            var resolverMock = new Mock<IPAMSecretResolver>();
            resolverMock.Setup(r => r.Resolve(It.IsAny<string>())).Returns<string>(s => s);

            var job = new TestableManagement(resolverMock.Object)
            {
                PublicJobParameters = new AwsSecretsManagerJobParameters()
            };

            var certStore = new CertificateStore
            {
                StorePath = "us-east-1",
                ClientMachine = "arn:aws:iam::123:role/test",
                Properties = propertiesJson
            };

            var m = typeof(JobBase<Management>).GetMethod(
                "SetStoreProperties",
                BindingFlags.NonPublic | BindingFlags.Instance);
            m.Should().NotBeNull("SetStoreProperties must be reachable via reflection");

            m!.Invoke(job, new object[] { certStore });

            return job.PublicJobParameters.StoreProperties;
        }

        [Fact]
        public void SeparatePrivateKey_DefaultsToFalse_WhenAbsent()
        {
            InvokeSetStoreProperties("{}").SeparatePrivateKey.Should().BeFalse();
        }

        [Theory]
        [InlineData("{\"SeparatePrivateKey\":true}")]
        [InlineData("{\"SeparatePrivateKey\":\"true\"}")]
        [InlineData("{\"SeparatePrivateKey\":\"True\"}")]
        [InlineData("{\"SeparatePrivateKey\":{\"value\":\"true\"}}")]
        [InlineData("{\"separateprivatekey\":\"true\"}")] // case-insensitive name match
        public void SeparatePrivateKey_ParsesTrue(string properties)
        {
            InvokeSetStoreProperties(properties).SeparatePrivateKey.Should().BeTrue();
        }

        [Theory]
        [InlineData("{\"SeparatePrivateKey\":false}")]
        [InlineData("{\"SeparatePrivateKey\":\"false\"}")]
        [InlineData("{\"SeparatePrivateKey\":\"\"}")]
        [InlineData("{\"SomethingElse\":\"true\"}")]
        [InlineData("")]
        [InlineData("not valid json")]
        public void SeparatePrivateKey_ParsesFalse(string properties)
        {
            InvokeSetStoreProperties(properties).SeparatePrivateKey.Should().BeFalse();
        }
    }
}
