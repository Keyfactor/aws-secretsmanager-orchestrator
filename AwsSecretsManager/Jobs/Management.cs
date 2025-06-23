
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using System;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using Microsoft.Extensions.Logging;
using Keyfactor.Orchestrators.Extensions.Interfaces;


namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Jobs
{
    [Job(KeyfactorJobType.MANAGEMENT)]
    public class Management : JobBase<Management>, IManagementJobExtension
    {
        public Management(IPAMSecretResolver resolver) : base(resolver) { }

        public JobResult ProcessJob(ManagementJobConfiguration config)
        {
            logger.MethodEntry();
            var jobType = config.OperationType.ToString();

            logger.LogTrace($"received new Management > {jobType} job. Job ID = {config.JobId}");
            logger.LogTrace($"initializing Management > {jobType} job..");

            base.Initialize(config);

            logger.LogDebug($"begin Management > {jobType}...");

            try
            {
                //Management jobs, unlike Discovery, Inventory, and Reenrollment jobs can have 3 different purposes:
                switch (config.OperationType)
                {
                    case CertStoreOperationType.Add:
                        return AddCertificate();
                    case CertStoreOperationType.Remove:
                        return RemoveCertificate();
                    default:
                        // in theory, this should never occur
                        return FailureJobResult($"Unsupported operation: {config.OperationType.ToString()}");
                }
            }
            catch (Exception ex)
            {
                //Status: 2=SuccessJobResult, 3=WarningJobResult, 4=Error
                var msg = $"an error occurred.  Management > {config.OperationType.ToString()} job was not successful.\n{ex.Message}";
                logger.LogError(msg);
                return FailureJobResult(msg);
            }
            finally
            {
                logger.MethodExit();
            }

        }

        public JobResult AddCertificate()
        {
            logger.MethodEntry();

            try
            {
                var certARN = SecretsManagerClient.AddSecret(JobParameters.StoreProperties, JobParameters.CertProperties).Result;
                return SuccessJobResult($"Successfully enrolled certificate with alias '{JobParameters.CertProperties.Alias}'.\nARN: {certARN}");
            }
            catch (Exception ex)
            {
                var msg = $"an error occurred when attempting to add the certificate.\n{ex.Message}";
                logger.LogError(msg);
                return FailureJobResult(msg);
            }
            finally
            {
                logger.MethodExit();
            }
        }

        public JobResult RemoveCertificate()
        {
            throw new NotImplementedException();
        }
    }
}

// {
//                "LastInventory": [],
//                "CertificateStoreDetails": {
//                    "ClientMachine": "localmachine",
//                    "StorePath": "c:\\tempSOS\\mystore.json",
//                    "StorePassword": null,
//                    "Properties": {
//                        "StoreNameString": "my sample store",
//                        "ForTestingOnlyBool": "true",
//                        "CollectionNameMultipleChoice": "internal",
//                        "PrivateDetailsSecret": "my secret",
//                        "ServerUsername": "joe",
//                        "ServerPassword": "v",
//                        "ServerUseSsl": "true"
//                    },
//                "Type": 105
//                },
//                "OperationType": 2,
//                "Overwrite": false,
//                "JobCertificate": {
//                    "Thumbprint": null,
//                    "Contents": "",
//                    "Alias": "testcert",
//                    "PrivateKeyPassword": "..."
//                },
//                "JobCancelled": false,
//                "ServerError": null,
//                "JobHistoryId": 28,
//                "RequestStatus": 1,
//                "ServerUsername": "joe",
//                "ServerPassword": "v",
//                "UseSSL": true,
//                "JobProperties": {
//                    "CommaSeparatedSansString": "testwritecert.keyfactor.lab,testcert.keyfactor.lab",
//                    "CertColorMultipleChoice": "red",
//                    "ForTestingOnlyBool": true,
//                    "PrivateCertDetailsSecret": "secretcert"
//                },
//                "JobTypeId": "00000000-0000-0000-0000-000000000000",
//                "JobId": "4234344b-a254-45b5-b233-d70aedd187ea",
//                "Capability": "CertStores.SOS.Management"
//          }