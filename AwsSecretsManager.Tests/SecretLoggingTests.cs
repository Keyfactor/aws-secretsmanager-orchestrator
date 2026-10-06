// Copyright 2026 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Amazon.SecretsManager.Model;
using FluentAssertions;
using Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Jobs;
using Keyfactor.Extensions.Orchestrators.AwsSecretsManager.models;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Tests
{
    /// <summary>
    /// Guards against secret material (private keys, credentials) being written to the
    /// orchestrator log.
    /// </summary>
    public class SecretLoggingTests
    {
        private const string SecretMarker = "SUPER-SECRET-KEY-MATERIAL";

        [Fact]
        public void ConvertSecretsPem_UnparseableSecret_DoesNotLogContents()
        {
            var logger = new RecordingLogger();
            var secret = new AWSSecret
            {
                Name = "broken-cert",
                SecretString = $"-----BEGIN PRIVATE KEY-----\n{SecretMarker}\n-----END PRIVATE KEY-----",
                Tags = new List<Tag>()
            };

            var (items, warnings) = InvokeConvertPem(logger, secret);

            items.Should().BeEmpty();
            warnings.Should().ContainSingle().Which.Should().Contain("broken-cert");
            warnings.Single().Should().NotContain(SecretMarker);

            logger.Messages.Should().Contain(m => m.Contains("broken-cert"),
                "the warning should still identify which secret failed");
            logger.Messages.Should().NotContain(m => m.Contains(SecretMarker),
                "secret contents must never be logged");
        }

        [Fact]
        public void RedactSecretStoreProperties_RedactsSecretFields()
        {
            var json = "{\"UseIAM\":\"true\",\"IAMUserAccessKey\":\"AKIA-" + SecretMarker + "\"," +
                       "\"IAMUserAccessSecret\":\"" + SecretMarker + "\"," +
                       "\"OAuthClientId\":{\"value\":\"" + SecretMarker + "\"}," +
                       "\"oauthclientsecret\":\"" + SecretMarker + "\"," +
                       "\"ExternalId\":\"ext-123\"}";

            var redacted = InvokeRedact(json);

            redacted.Should().NotContain(SecretMarker);
            redacted.Should().Contain("\"UseIAM\":\"true\"", "non-secret fields are kept for troubleshooting");
            redacted.Should().Contain("\"ExternalId\":\"ext-123\"");
        }

        [Theory]
        [InlineData("not valid json " + SecretMarker)]
        [InlineData("{\"IAMUserAccessSecret\":\"" + SecretMarker + "\"")] // truncated JSON
        public void RedactSecretStoreProperties_UnparseableInput_ReturnsNothingFromInput(string input)
        {
            InvokeRedact(input).Should().NotContain(SecretMarker);
        }

        // ── helpers ────────────────────────────────────────────────────────

        private static string InvokeRedact(string json)
        {
            var m = typeof(JobBase<Inventory>).GetMethod(
                "RedactSecretStoreProperties", BindingFlags.NonPublic | BindingFlags.Static);
            m.Should().NotBeNull("RedactSecretStoreProperties must be reachable via reflection");
            return (string)m!.Invoke(null, new object[] { json })!;
        }

        private static (List<CurrentInventoryItem>, List<string>) InvokeConvertPem(ILogger logger, params AWSSecret[] secrets)
        {
            var resolverMock = new Mock<IPAMSecretResolver>();
            resolverMock.Setup(r => r.Resolve(It.IsAny<string>())).Returns<string>(s => s);

            var inv = new TestableInventory(resolverMock.Object)
            {
                PublicJobParameters = new AwsSecretsManagerJobParameters { StoreType = "AWSSMPEM" }
            };

            var loggerProp = typeof(JobBase<Inventory>).GetProperty(
                "_logger", BindingFlags.NonPublic | BindingFlags.Instance);
            loggerProp.Should().NotBeNull("the job logger must be injectable via reflection");
            loggerProp!.SetValue(inv, logger);

            var m = typeof(Inventory).GetMethod(
                "ConvertSecretsPem", BindingFlags.NonPublic | BindingFlags.Instance);
            m.Should().NotBeNull("ConvertSecretsPem must be reachable via reflection");

            var tuple = (System.Runtime.CompilerServices.ITuple)m!.Invoke(inv, new object[] { secrets.ToList() })!;
            return ((List<CurrentInventoryItem>)tuple[0]!, (List<string>)tuple[1]!);
        }

        private sealed class RecordingLogger : ILogger
        {
            public List<string> Messages { get; } = new List<string>();

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                Messages.Add(formatter(state, exception));
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new NullScope();
                public void Dispose() { }
            }
        }
    }
}
