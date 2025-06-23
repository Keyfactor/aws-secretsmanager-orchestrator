
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.SecretsManager.Model;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Jobs
{
    // The Inventory class implementes IAgentJobExtension and is meant to find all of the certificates in a given certificate store on a given server
    //  and return those certificates back to Keyfactor for storing in its database.  Private keys will NOT be passed back to Keyfactor Command 
    [Job(KeyfactorJobType.INVENTORY)]
    public class Inventory : JobBase<Inventory>, IInventoryJobExtension
    {
        public Inventory(IPAMSecretResolver resolver) : base(resolver) { }

        //Job Entry Point
        public JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory)
        {
            logger.MethodEntry();
            logger.LogTrace($"received new inventory job. Job ID = {config.JobId}");
            logger.LogTrace($"initializing inventory job..");

            base.Initialize(config);

            logger.LogDebug($"begin Inventory...");

            List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();

            try
            {
                // first, we compose filter criteria from parameters

                var filters = new List<Filter>();

                if (JobParameters.StoreProperties.UseTags) // we will generate a filter based on provided tag name and value
                {
                    logger.LogTrace("using tags for identification, setting the tag filter values..");
                    var tagNameFilter = new Filter()
                    {
                        Key = AWSFilterParameter.TAG_KEY,
                        Values = new List<string>() { JobParameters.StoreProperties.TagName }
                    };

                    var tagValueFilter = new Filter()
                    {
                        Key = AWSFilterParameter.TAG_VALUE,
                        Values = new List<string> { JobParameters.StoreProperties.TagValue }
                    };

                    filters.Add(tagNameFilter);
                    filters.Add(tagValueFilter);
                }
                else
                {
                    if (JobParameters.StoreProperties.UsePrefix)
                    {
                        logger.LogTrace("using path prefix for identification, setting the secret name filter value..");

                        var pathFilter = new Filter()
                        {
                            Key = AWSFilterParameter.NAME, // searches prefix (not full match) by default
                            Values = new List<string>() { JobParameters.StoreProperties.NamePrefix }
                        };

                        filters.Add(pathFilter);
                    }
                }

                logger.LogTrace($"determined cert filter criteria to be: ");

                if (filters.Count == 0)
                {
                    logger.LogTrace("no filter; all certificates in the region that are available to the authenticating identity");
                }

                filters.ForEach(filter =>
                {
                    logger.LogTrace($"filter key: {filter.Key}");
                    logger.LogTrace($"filter value: {filter.Values.First()}");
                });

                // now get the secrets

                var secrets = SecretsManagerClient.ListSecrets(filters).Result;

                // check for validity and parse-ability

                var warningCount = 0;

                foreach (var potentialCert in secrets)
                {
                    logger.LogTrace($"parsing secret named: {potentialCert.Name}");

                    var includeChain = false;
                    try
                    {
                        // try pemtoder
                        var certBytes = PKI.PEM.PemUtilities.PEMToDER(potentialCert.SecretString);

                        // check for chain level (multiple certs)

                        var certCount = potentialCert.SecretString.Split("-----BEGIN CERTIFICATE-----").Length - 1;

                        includeChain = certCount > 1;

                        logger.LogTrace($"found {certCount} headers.  {(includeChain ? " multiple headers; chain implied" : " single certificate, no chain.")}");

                        logger.LogTrace("successfully parsed PEM string");
                    }
                    catch (Exception ex)
                    {
                        // it failed; log a warning and continue.

                        logger.LogWarning($"Unable to perform PEM to DER conversion on secret named {potentialCert.Name}.");
                        logger.LogWarning("cert contents:");
                        logger.LogWarning($"\n{potentialCert.SecretString}\n");
                        logger.LogWarning($"Exception: {ex.Message}");
                        warningCount++;
                        continue;
                    }

                    inventoryItems.Add(new CurrentInventoryItem()
                    {
                        Alias = potentialCert.Name, // trim prefix?
                        Certificates = new string[] { potentialCert.SecretString },
                        PrivateKeyEntry = false,
                        UseChainLevel = includeChain
                    });

                } // end cert evaluation loop

                var succeeded = true;
                var resultMessage = $"Successfully processed {inventoryItems.Count} certificates. ";
                JobResult result = SuccessJobResult(resultMessage);

                // if there was a mix of errors and successful retrieval..
                if (warningCount > 0 && inventoryItems.Count > 0)
                {
                    resultMessage += $"\n{warningCount} certificate(s) could not be processed.\nReview the logs on the orchestrator for more details.";
                    result = WarningJobResult(resultMessage);
                    succeeded = true;
                }

                // if there were only failed attempts we do not count as successful
                if (warningCount > 0 && inventoryItems.Count == 0)
                {
                    result = FailureJobResult($"{warningCount} certificate(s) could not be processed.\nReview the logs on the orchestrator for more details.");
                    succeeded = false;
                }

                if (succeeded && submitInventory.Invoke(inventoryItems)) return result;

                return FailureJobResult("Inventory Job callback failed.  Review the orchestrator logs for more details.");
            }
            catch (Exception ex)
            {
                return FailureJobResult($"Error performing Inventory job: {ex.Message}\nReview the orchestrator logs for more details.");
            }
        }
    }
}