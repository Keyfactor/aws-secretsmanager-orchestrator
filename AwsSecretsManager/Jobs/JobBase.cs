using Amazon.SecretsManager.Model;
using Keyfactor.AnyAgent.AwsCertificateManager;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text.Json.Nodes;


namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Jobs
{
    public class JobBase<T> : IOrchestratorJobExtension
    {
        public string ExtensionName => Constants.STORE_TYPE_NAME;
        internal protected ILogger logger { get; set; }

        internal protected IPAMSecretResolver _resolver { get; set; }

        public virtual AwsSecretsManagerClient SecretsManagerClient { get; set; }
        internal protected virtual AwsSecretsManagerJobParameters JobParameters { get; set; }
        internal AuthUtilities AuthUtilities { get; set; }

        // set the configuration parameters
        public virtual void Initialize(InventoryJobConfiguration config)
        {
            logger.MethodEntry();

            logger.LogTrace($"reading serialized configuration passed from Command to create the AwsSecretsManagerJobParameters object..");
            JobParameters = new AwsSecretsManagerJobParameters();
            JobParameters.JobType = "Inventory";
            JobParameters.JobId = config.JobId;
            JobParameters.StoreProperties.AwsRegion = config.CertificateStoreDetails.ClientMachine;
            JobParameters.StoreProperties.StorePath = config.CertificateStoreDetails.StorePath;

            logger.LogTrace("resolving PAM fields..");
            JobParameters.StoreProperties.AuthAccessKeyId = _resolver.Resolve(config.ServerUsername);
            JobParameters.StoreProperties.AuthSecret = _resolver.Resolve(config.ServerPassword);

            logger.LogTrace("parameter initialization for Inventory job complete");

            logger.MethodExit();
        }

        public virtual void Initialize(ManagementJobConfiguration config)
        {
            logger.MethodEntry();

            logger.LogTrace($"reading serialized configuration passed from Command to create the AwsSecretsManagerJobParameters object..");
            JobParameters = new AwsSecretsManagerJobParameters();
            JobParameters.JobType = "Management";
            JobParameters.JobId = config.JobId;
            JobParameters.StoreProperties.AwsRegion = config.CertificateStoreDetails.ClientMachine;
            JobParameters.StoreProperties.StorePath = config.CertificateStoreDetails.StorePath;

            logger.LogTrace("resolving PAM fields..");
            JobParameters.StoreProperties.AuthAccessKeyId = _resolver.Resolve(config.ServerUsername);
            JobParameters.StoreProperties.AuthSecret = _resolver.Resolve(config.ServerPassword);

            logger.LogTrace("getting entry parameters..");

            // get and parse Tag entry parameters

            logger.LogTrace("getting certificate tag values, if any..");

            var tagsJSON = config.JobProperties["Tags"]?.ToString();

            var jObj = JObject.Parse(tagsJSON);

            var tagDict = new Dictionary<string, string>();

            foreach (var tag in jObj)
            {
                tagDict.Add(tag.Key, (string)tag.Value);
            }

            logger.LogTrace($"found {tagDict.Count} Tag(s)");

            JobParameters.CertProperties.Tags = tagDict;

            // get and parse list of replica regions

            logger.LogTrace("getting replica region values, if any..");

            var replicaRegionsJSON = config.JobProperties["ReplicaRegions"]?.ToString();

            jObj = JObject.Parse(replicaRegionsJSON);

            JobParameters.CertProperties.ReplicaRegions = new List<ReplicaRegionType>();

            foreach (var replicaRegion in jObj)
            {
                JobParameters.CertProperties.ReplicaRegions.Add(new ReplicaRegionType() { Region = jObj["Region"]?.ToString().ToLower(), KmsKeyId = jObj["KmsKeyId"]?.ToString() });
            }
            logger.LogTrace($"found {JobParameters.CertProperties.ReplicaRegions.Count} Replica Regions(s)");

            // get KmsKeyId (to use an encryption key other than the default)
            JobParameters.CertProperties.KmsKeyId = config.JobProperties["KmsKeyId"]?.ToString();

            JobParameters.CertProperties.Overwrite = config.Overwrite;
            JobParameters.CertProperties.Alias = config.JobCertificate.Alias;
            JobParameters.CertProperties.PrivateKeyPassword = config.JobCertificate.PrivateKeyPassword;
            JobParameters.CertProperties.Thumbprint = config.JobCertificate.Thumbprint;
            JobParameters.CertProperties.Contents = config.JobCertificate.Contents;

            logger.LogTrace("parameter initialization for Management job complete");

            logger.MethodExit();
        }
    }
}
