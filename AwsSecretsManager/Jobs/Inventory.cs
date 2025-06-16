using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.SecretsManager.Model;
using Keyfactor.PKI;
using Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Jobs;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography.X509Certificates;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager
{
    // The Inventory class implementes IAgentJobExtension and is meant to find all of the certificates in a given certificate store on a given server
    //  and return those certificates back to Keyfactor for storing in its database.  Private keys will NOT be passed back to Keyfactor Command 
    [Job(KeyfactorJobType.INVENTORY)]
    public class Inventory : JobBase<Inventory>, IInventoryJobExtension
    {
        public Inventory(IPAMSecretResolver resolver)
        {
            logger = LogHandler.GetClassLogger(GetType());
            _resolver = resolver;
        }


        //    // Configuration StoreProperties Passed from Command for an Inventory Job have the following structure:
        //    // { ServerUsername: "",
        //    //   ServerPassword: "",
        //    //   JobHistoryId: "",
        //    //   Capability: 
        //    //   CertificateStoreDetails: {
        //    //      ClientMachine: "aws region name",
        //    //      StorePath: "", // the value here will be either the Tag Value or Name prefix for identifying certificates to be managed by the cert store.
        //    //      StorePassword: "",
        //    //      StoreProperties: { // CertificateStoreDetails.StoreProperties are dynamic and contain the custom store properties we define in the store type
        //    //          UseTags: boolean, // if true, the StorePath will be the tag value, and the required field "TagName" should be populated.
        //    //          TagName: false // the name of the tag to use for identifying the certs to be managed
        //    //      }
        //    //   }
        //    // } 


        //Job Entry Point
        public JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory)
        {
            logger.MethodEntry();
            logger.LogTrace($"received new inventory job. Job ID = {config.JobId}");

            logger.LogTrace($"initializing inventory job..");

            base.Initialize(config);

            logger.LogDebug($"begin Inventory...");

            //List<AgentCertStoreInventoryItem> is the collection that the interface expects to return from this job.  It will contain a collection of certificates found in the store along with other information about those certificates

            List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();

            try
            {
                // first, compose filter from parameters

                var filters = new List<Filter>();

                if (JobParameters.StoreProperties.UseTags) // we generate a filter based on found tag name and value
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
                    logger.LogTrace("using path prefix for identification, setting the secret name filter value..");

                    var pathFilter = new Filter()
                    {
                        Key = AWSFilterParameter.NAME,
                        Values = new List<string>() { JobParameters.StoreProperties.StorePath }
                    };

                    filters.Add(pathFilter);
                }

                logger.LogTrace($"determined cert filter criteria to be: ");

                filters.ForEach(filter => {
                    logger.LogTrace($"filter key: {filter.Key}");
                    logger.LogTrace($"filter value: {filter.Values.First()}");                
                });

                // then get the secrets

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

                        logger.LogWarning("Unable to perform PEM to DER conversion on cert contents.");
                        logger.LogWarning("cert contents:");
                        logger.LogWarning($"\n{potentialCert.SecretString}");
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
                }

                var successMessage = $"Successfully processed {inventoryItems.Count} certificates. ";
                if (warningCount > 0) successMessage += $"\n{warningCount} certificate(s) could not be processed.\nReview the logs on the orchestrator for more details.";
                if (submitInventory.Invoke(inventoryItems)) return Success(successMessage);
                return Failure(new Exception("Inventory Job Failed.  Review the orchestrator logs for more details."), "Inventory");


                //Code logic to:
                // 1) Connect to the orchestrated server (config.CertificateStoreDetails.ClientMachine) containing the certificate store to be inventoried (config.CertificateStoreDetails.StorePath)
                // 2) Custom logic to retrieve certificates from certificate store.
                // 3) Add certificates (no private keys) to the collection below.  If multiple certs in a store comprise a chain, the Certificates array will house multiple certs per InventoryItem.  If multiple certs
                //     in a store comprise separate unrelated certs, there will be one InventoryItem object created per certificate.

                //**** Will need to uncomment the block below and code to the extension's specific needs.  This builds the collection of certificates and related information that will be passed back to the KF Orchestrator service and then Command.
                //inventoryItems.Add(new AgentCertStoreInventoryItem()
                //{
                //    ItemStatus = OrchestratorInventoryItemStatus.Unknown, //There are other statuses, but Command can determine how to handle new vs modified certificates
                //    Alias = {valueRepresentingChainIdentifier}
                //    PrivateKeyEntry = true|false //You will not pass the private key back, but you can identify if the main certificate of the chain contains a private key in the store
                //    UseChainLevel = true|false,  //true if Certificates will contain > 1 certificate, main cert => intermediate CA cert => root CA cert.  false if Certificates will contain an array of 1 certificate
                //    Certificates = //Array of single X509 certificates in Base64 string format (certificates if chain, single cert if not), something like:
                //    ****************************
                //          foreach(X509Certificate2 certificate in certificates)
                //              certList.Add(Convert.ToBase64String(certificate.Export(X509ContentType.Cert)));
                //              certList.ToArray();
                //    ****************************
                //});

            }
            catch (Exception ex)
            {
                //Status: 2=Success, 3=Warning, 4=Error
                return new JobResult() { Result = Keyfactor.Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = "Custom message you want to show to show up as the error message in Job History in KF Command" };
            }

            try
            {
                //Sends inventoried certificates back to KF Command
                submitInventory.Invoke(inventoryItems);
                //Status: 2=Success, 3=Warning, 4=Error
                return new JobResult() { Result = Keyfactor.Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
            }
            catch (Exception ex)
            {
                // NOTE: if the cause of the submitInventory.Invoke exception is a communication issue between the Orchestrator server and the Command server, the job status returned here
                //  may not be reflected in Keyfactor Command.
                return new JobResult() { Result = Keyfactor.Orchestrators.Common.Enums.OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = "Custom message you want to show to show up as the error message in Job History in KF Command" };
            }
        }
    }
}