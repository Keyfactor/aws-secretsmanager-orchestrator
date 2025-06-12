using System;

using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;

using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Jobs
{
    public class Management : JobBase<Management>, IManagementJobExtension
    {
        public JobResult ProcessJob(ManagementJobConfiguration config)
        {
            //          Sample Management > Create job configuration:
            //
            //          {
            //              "LastInventory": [],
            //              "CertificateStoreDetails": {
            //                  "ClientMachine": "localmachine",
            //                  "StorePath": "c:\\tempSOS\\mystore.json",
            //                  "StorePassword": null,
            //                  "Properties": "{\"StoreNameString\":\"my sample store\",\"ForTestingOnlyBool\":\"true\",\"CollectionNameMultipleChoice\":\"internal\",\"PrivateDetailsSecret\":\"my secret\",\"ServerUsername\":\"joe\",\"ServerPassword\":\"v\",\"ServerUseSsl\":\"true\"}",
            //                  "Type": 105
            //              },
            //              "OperationType": 4,
            //              "Overwrite": false,
            //              "JobCertificate": {
            //                  "Thumbprint": null,
            //                  "Contents": null,
            //                  "Alias": null,
            //                  "PrivateKeyPassword": null
            //              },
            //              "JobCancelled": false,
            //              "ServerError": null,
            //              "JobHistoryId": 15,
            //              "RequestStatus": 1,
            //              "ServerUsername": "joe",
            //              "ServerPassword": "v",
            //              "UseSSL": true,
            //              "JobProperties": { },
            //              "JobTypeId": "00000000-0000-0000-0000-000000000000",
            //              "JobId": "ac24fcd0-af79-49f7-bad8-8b2acc2ddbb6",
            //              "Capability": "CertStores.SOS.Management"
            //          }

            //  Sample Management > Add job configuration
            //
            //            {
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

            //NLog Logging to c:\CMS\Logs\CMS_Agent_Log.txt
            ILogger logger = LogHandler.GetClassLogger(GetType());
            logger.LogDebug($"Begin Management...");

            try
            {
                //Management jobs, unlike Discovery, Inventory, and Reenrollment jobs can have 3 different purposes:
                switch (config.OperationType)
                {
                    case CertStoreOperationType.Add:
                        //OperationType == Add - Add a certificate to the certificate store passed in the config object
                        //Code logic to:
                        // 1) Connect to the orchestrated server (config.CertificateStoreDetails.ClientMachine) containing the certificate store
                        // 2) Custom logic to add certificate to certificate store (config.CertificateStoreDetails.StorePath) possibly using alias as an identifier if applicable (config.JobCertificate.Alias).  Use alias and overwrite flag (config.Overwrite)
                        //     to determine if job should overwrite an existing certificate in the store, for example a renewal.
                        break;
                    case CertStoreOperationType.Remove:
                        //OperationType == Remove - Delete a certificate from the certificate store passed in the config object
                        //Code logic to:
                        // 1) Connect to the orchestrated server (config.CertificateStoreDetails.ClientMachine) containing the certificate store
                        // 2) Custom logic to remove the certificate in a certificate store (config.CertificateStoreDetails.StorePath), possibly using alias (config.JobCertificate.Alias) or certificate thumbprint to identify the certificate (implementation dependent)
                        break;
                    default:
                        //Invalid OperationType.  Return error.  Should never happen though
                        return new JobResult() { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = $"Site {config.CertificateStoreDetails.StorePath} on server {config.CertificateStoreDetails.ClientMachine}: Unsupported operation: {config.OperationType.ToString()}" };
                }
            }
            catch (Exception ex)
            {
                //Status: 2=Success, 3=Warning, 4=Error
                return new JobResult() { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = "Custom message you want to show to show up as the error message in Job History in KF Command" };
            }

            //Status: 2=Success, 3=Warning, 4=Error
            return new JobResult() { Result = OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
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