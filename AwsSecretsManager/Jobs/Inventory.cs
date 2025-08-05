
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
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
            _logger.MethodEntry();
            _logger.LogTrace($"received new inventory job. Job ID = {config.JobId}");
            _logger.LogTrace($"initializing inventory job..");

            base.Initialize(config);

            _logger.LogDebug($"begin Inventory...");

            List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();
            List<string> warnings = new List<string>();

            try
            {
                // first, we compose filter criteria from parameters

                var filters = new List<Filter>();

                if (JobParameters.StoreProperties.UseTags) // we will generate a filter based on provided tag name and value
                {
                    _logger.LogTrace("using tags for identification, setting the tag filter values..");
                    var tagNameFilter = new Filter()
                    {
                        Key = AWSFilterParameter.TAG_KEY,
                        Values = new List<string>() { JobParameters.StoreProperties.TagName }
                    };
                    filters.Add(tagNameFilter);

                    if (!string.IsNullOrEmpty(JobParameters.StoreProperties.TagValue))
                    {
                        var tagValueFilter = new Filter()
                        {
                            Key = AWSFilterParameter.TAG_VALUE,
                            Values = new List<string> { JobParameters.StoreProperties.TagValue }
                        };
                        filters.Add(tagValueFilter);
                    }
                }
                else
                {
                    if (JobParameters.StoreProperties.UsePrefix)
                    {
                        _logger.LogTrace("using path prefix for identification, setting the secret name filter value..");

                        var pathFilter = new Filter()
                        {
                            Key = AWSFilterParameter.NAME, // searches prefix (not full match) by default
                            Values = new List<string>() { JobParameters.StoreProperties.NamePrefix }
                        };

                        filters.Add(pathFilter);
                    }
                }

                _logger.LogTrace($"determined cert filter criteria to be: ");

                if (filters.Count == 0)
                {
                    _logger.LogTrace("no filter; all certificates in the region that are available to the authenticating identity");
                }

                filters.ForEach(filter =>
                {
                    _logger.LogTrace($"filter key: {filter.Key}");
                    _logger.LogTrace($"filter value: {filter.Values.First()}");
                });

                // now get the secrets

                var secrets = _secretsManagerClient.ListSecrets(filters).Result;

                // check for validity and parse-ability
                // then convert to base64 encoded cer
                // this process differs for each store type
                switch (JobParameters.StoreType)
                {
                    case "AWSSMPEM":
                        (inventoryItems, warnings) = ConvertSecretsPem(secrets);
                        break;
                    case "AWSSMPFX":
                        (inventoryItems, warnings) = ConvertSecretsPfx(secrets).Result;
                        break;
                    case "AWSSMJKS":
                        (inventoryItems, warnings) = ConvertSecretsJks(secrets);
                        break;
                    default:
                        throw new ArgumentException($"Invalid store type {JobParameters.StoreType}");
                }

                var warningCount = warnings.Count;


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

                _logger.LogTrace($"invoking callback with list of {inventoryItems.Count} certificates..");

                if (succeeded && submitInventory.Invoke(inventoryItems)) return result;

                return FailureJobResult("Inventory Job callback failed.  Review the orchestrator logs for more details.");
            }
            catch (Exception ex)
            {
                return FailureJobResult($"Error performing Inventory job: {ex.Message}\nReview the orchestrator logs for more details.");
            }
        }

        private (List<CurrentInventoryItem>, List<string>) ConvertSecretsJks(List<SecretValueEntry> secrets)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Takes a list of PFX secrets and converts them to currentInventoryItems
        /// </summary>
        /// <param name="secrets"></param>
        /// <returns>A list of CurrentInventoryItem</returns>
        private async Task<(List<CurrentInventoryItem>, List<string>)> ConvertSecretsPfx(List<SecretValueEntry> secrets)
        {
            _logger.MethodEntry();

            var inventory = new List<CurrentInventoryItem>();
            var warnings = new List<string>();

            foreach (var secret in secrets)
            {
                var certificateChain = new List<string>();
                byte[] pfxBytes = null;

                try
                {
                    var pfxPassword = await _secretsManagerClient.GetPassword(secret);

                    if (pfxPassword == null)
                    {
                        warnings.Add($"Unable to find a secret containing the cert store password for {secret.Name}");
                        continue;
                    }
                    // now that we have the password, use it to extract the public certificates
                    
                    if (secret.SecretBinary != null)
                    {
                        // Convert the MemoryStream (SecretBinary) to a byte array
                        using (MemoryStream memoryStream = secret.SecretBinary)
                        {
                            pfxBytes = memoryStream.ToArray();
                        }
                    }

                    // Load the PFX file
                    var pfx = new X509Certificate2(pfxBytes, pfxPassword, X509KeyStorageFlags.EphemeralKeySet);

                    // build the chain
                    var chain = new X509Chain();

                    // Configure chain building options
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                    chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

                    bool chainBuilt = chain.Build(pfx);

                    foreach (var chainElement in chain.ChainElements)
                    {
                        var cert = chainElement.Certificate;
                        var derBytes = cert.GetRawCertData(); // Gets DER-encoded certificate
                        var base64Der = Convert.ToBase64String(derBytes);
                        certificateChain.Add(base64Der);
                    }                   

                    var inventoryItem = new CurrentInventoryItem
                    {
                        Alias = secret.Name,
                        Certificates = certificateChain,
                        UseChainLevel = certificateChain.Count > 1,
                        PrivateKeyEntry = pfx.HasPrivateKey
                    };

                    // Clean up
                    pfx.Dispose();
                    chain.Dispose();

                    inventory.Add(inventoryItem);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"There was an error attempting to retreive the store password for {secret.Name}");
                    warnings.Add(ex.Message);
                    continue;
                }
            }
            _logger.MethodExit();
            return (inventory, warnings);
        }

        private (List<CurrentInventoryItem>, List<string>) ConvertSecretsPem(List<SecretValueEntry> secrets)
        {
            _logger.MethodEntry();
            var warnings = new List<string>();
            var inventoryItems = new List<CurrentInventoryItem>();

            foreach (var potentialCert in secrets)
            {
                _logger.LogTrace($"parsing secret named: {potentialCert.Name}");
                var hasPrivateKey = false;

                List<string> encodedCerts = new List<string>();

                try
                {
                    // for AWSSMPEM, they will be stored as a secret string in PEM format.
                    var secretString = potentialCert.SecretString;

                    //_logger.LogTrace($"secret string: {secretString}");

                    // try pemtoder
                    var certBytes = PKI.PEM.PemUtilities.PEMToDER(potentialCert.SecretString);
                    _logger.LogTrace("successfully tested conversion from PEM to DER");

                    // if it didn't throw.. convert to base64 cer format

                    (encodedCerts, hasPrivateKey) = CertUtilities.ConvertPemToFullChainBase64(potentialCert.SecretString);

                    _logger.LogTrace($"converted to Base64 cer format.  The chain is {(encodedCerts.Count > 1 ? "" : "not ")}included.");

                    _logger.LogTrace("successfully parsed certificate from AWS Secrets Manager");
                }
                catch (Exception ex)
                {
                    // it failed; log a warning and continue.
                    var msg =
$@"Unable to perform PEM to DER conversion on secret named {potentialCert.Name}.
cert contents:
{{potentialCert.SecretString}}
""Exception: {{ex.Message}}";

                    _logger.LogWarning("cert contents:");
                    _logger.LogWarning($"\n{potentialCert.SecretString}\n");
                    _logger.LogWarning($"Exception: {ex.Message}");
                    warnings.Add(msg);
                    continue;
                }

                inventoryItems.Add(new CurrentInventoryItem()
                {
                    Alias = potentialCert.Name,
                    Certificates = encodedCerts.ToArray(),
                    PrivateKeyEntry = hasPrivateKey,
                    UseChainLevel = encodedCerts.Count > 1,
                });

            } // end cert evaluation loop
            return (inventoryItems, warnings);
        }
    }
}