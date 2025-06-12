using System;
using System.Collections.Generic;
using Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Jobs;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;

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
        //    //      ClientMachine: "",
        //    //      StorePath: "", // in this case, it will be "<aws region name>/<cert tag value>" where cert tag value is the value of the "KeyfactorCertificateStore" tag key.
        //    //      StorePassword: "",
        //    //      StoreProperties: { // CertificateStoreDetails.StoreProperties are dynamic and contain the custom store properties we define in the store type
        //    //          StoreNameString: "",
        //    //          ForTestingOnlyBool: false,
        //    //          CollectionNameMultipleChoice: "",
        //    //          PrivateDetailsSecret: ""
        //    //      }
        //    //   }
        //    // } 


        //Job Entry Point
        public JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory)
        {
            logger.MethodEntry();
            logger.LogTrace($"Received new inventory job. Job ID = {config.JobId}");

            logger.LogTrace($"Initializing inventory job..");

            base.Initialize(config);

            logger.LogDebug($"Begin Inventory...");

            //List<AgentCertStoreInventoryItem> is the collection that the interface expects to return from this job.  It will contain a collection of certificates found in the store along with other information about those certificates
            
            List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();

            try
            {
                var secrets = SecretsManagerClient.ListSecrets(JobParameters.StoreProperties.StorePath);

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