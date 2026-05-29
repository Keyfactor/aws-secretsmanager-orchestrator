// Copyright 2026 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Jobs;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Tests
{
    /// <summary>
    /// Test double for Management that:
    ///  - allows tests to inject a mock AwsSecretsManagerClient and pre-populated JobParameters
    ///  - overrides Initialize to a no-op so AWS auth wiring is skipped
    ///  - exposes JobParameters publicly so tests can set them up directly
    /// </summary>
    internal class TestableManagement : Management
    {
        public TestableManagement(IPAMSecretResolver resolver) : base(resolver) { }

        // Skip the real Initialize entirely. Tests configure JobParameters / _secretsManagerClient up front.
        public override void Initialize(ManagementJobConfiguration config) { }
        public override void Initialize(InventoryJobConfiguration config) { }

        public AwsSecretsManagerJobParameters PublicJobParameters
        {
            get => JobParameters;
            set => JobParameters = value;
        }
    }

    internal class TestableInventory : Inventory
    {
        public TestableInventory(IPAMSecretResolver resolver) : base(resolver) { }

        public override void Initialize(InventoryJobConfiguration config) { }
        public override void Initialize(ManagementJobConfiguration config) { }

        public AwsSecretsManagerJobParameters PublicJobParameters
        {
            get => JobParameters;
            set => JobParameters = value;
        }
    }
}
