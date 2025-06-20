using Amazon.Runtime.Internal.Util;
using Amazon.SecretsManager.Model;
using Keyfactor.Extensions.Aws;
using Keyfactor.Extensions.Aws.Models;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static Org.BouncyCastle.Math.EC.ECCurve;


namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Jobs
{
    public class JobBase<T> : IOrchestratorJobExtension
    {
        public string ExtensionName => Constants.STORE_TYPE_NAME;
        internal protected Microsoft.Extensions.Logging.ILogger logger { get; set; }

        internal protected IPAMSecretResolver _resolver { get; set; }

        public virtual AwsSecretsManagerClient SecretsManagerClient { get; set; }
        internal virtual AwsAuthUtility authUtility { get; set; }

        internal protected virtual AwsSecretsManagerJobParameters JobParameters { get; set; }

        public JobBase(IPAMSecretResolver resolver)
        {
            logger = LogHandler.GetClassLogger(GetType());
            _resolver = resolver;
            authUtility = new AwsAuthUtility(resolver);
        }

        // set the configuration parameters
        public virtual void Initialize(InventoryJobConfiguration config)
        {
            logger.MethodEntry();

            logger.LogTrace($"reading serialized configuration passed from Command to create the AwsSecretsManagerJobParameters object..");

            JobParameters = new AwsSecretsManagerJobParameters();
            JobParameters.JobType = "Inventory";
            JobParameters.JobId = config.JobId;
            JobParameters.JobHistoryId = config.JobHistoryId;

            SetStoreProperties(config.CertificateStoreDetails);

            InitializeAwsClient(config.CertificateStoreDetails, JobParameters.StoreProperties.AwsRegion);

            logger.LogTrace("Inventory job initialization complete");

            logger.MethodExit();
        }

        public virtual void Initialize(ManagementJobConfiguration config)
        {
            logger.MethodEntry();

            logger.LogTrace($"reading serialized configuration passed from Command to create the AwsSecretsManagerJobParameters object..");

            JobParameters = new AwsSecretsManagerJobParameters();
            JobParameters.JobType = "Management";
            JobParameters.JobId = config.JobId;
            JobParameters.JobHistoryId = config.JobHistoryId;

            SetStoreProperties(config.CertificateStoreDetails);

            SetCertProperties(config.JobProperties, config.JobCertificate, config.Overwrite);

            InitializeAwsClient(config.CertificateStoreDetails, JobParameters.StoreProperties.AwsRegion);

            logger.LogTrace($"parameter initialization for Management > {config.OperationType.ToString()} job complete");

            logger.MethodExit();
        }

        private protected void SetStoreProperties(CertificateStore storeProps)
        {
            logger.MethodEntry();

            var roleName = storeProps.ClientMachine; // Fully qualified ARN of the role to assume; might be prefixed with [profile] to use the default profile loaded on the host            
            logger.LogTrace("parsing tags, prefix, and region from the store path..");
            (var storePath, var prefix, var tagName, var tagValue) = ParseStorePath(storeProps.StorePath);

            logger.LogTrace(
                @$"parsed the following from storepath: 
                prefix: {prefix ?? "(not provided)"}
                tagName: {tagName ?? "(not provided)"}
                tagValue: {tagValue ?? "(not provided)"}
                storePath: {storePath}");


            logger.LogTrace("determining identification strategy for this cert store (tags, prefix, or full region)..");

            if (!string.IsNullOrEmpty(tagName))
            {
                // using tags
                if (string.IsNullOrEmpty(tagValue))
                {
                    throw new MissingFieldException("tagName is defined, but tagValue is missing.  Both are needed to filter by tags.");
                }
                logger.LogTrace("using tag name and tag value (from store path)");
                JobParameters.StoreProperties.TagName = tagName;
                JobParameters.StoreProperties.TagValue = tagValue;
            }
            else
            {
                logger.LogTrace($"tag is undefined, checking for path value..");

                if (!string.IsNullOrEmpty(prefix) && prefix.Trim() != "/" && prefix.Trim() != "\\") ;
                {
                    logger.LogTrace($"using path prefix '{prefix}' in secret name");
                    JobParameters.StoreProperties.NamePrefix = prefix;
                }
            }
        }

        private void InitializeAwsClient(CertificateStore storeProps, string region)
        {
            logger.MethodEntry();
            AuthCustomFieldParameters customFields = JsonConvert.DeserializeObject<AuthCustomFieldParameters>(storeProps.Properties,
                    new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });

            AuthenticationParameters authParams = new AuthenticationParameters
            {
                RoleARN = storeProps.ClientMachine,
                Region = region,
                CustomFields = customFields
            };

            logger.LogTrace("initializing AWS client..");
            SecretsManagerClient.InitializeClient(authParams, authUtility);

            logger.MethodExit();
        }

        private void SetCertProperties(Dictionary<string, object> jobProperties, ManagementJobCertificate certProperties, bool overwrite = false)
        {
            logger.MethodEntry();

            // get cert tags entry parameter;  format = [{Region: "<region name>", KmsKeyId: "<key id>"}, ...]

            logger.LogTrace("getting certificate tag values, if any..");

            var tagsJSON = jobProperties["Tags"]?.ToString();
            var jObj = JObject.Parse(tagsJSON);
            var tagDict = new Dictionary<string, string>();

            foreach (var tag in jObj)
            {
                tagDict.Add(tag.Key, (string)tag.Value);
            }

            logger.LogTrace($"found {tagDict.Count} Tag(s)");

            JobParameters.CertProperties.Tags = tagDict;

            // get replica regions entry parameter;  format =  [{Region: "<region name>", KmsKeyId: "<key id>"}, ...]            

            var replicaRegionsJSON = jobProperties["ReplicaRegions"]?.ToString();
            logger.LogTrace($"getting replica region values, if any, from the JSON string '{replicaRegionsJSON}'");

            jObj = JObject.Parse(replicaRegionsJSON);
            JobParameters.CertProperties.ReplicaRegions = new List<ReplicaRegionType>();

            foreach (var replicaRegion in jObj)
            {
                JobParameters.CertProperties.ReplicaRegions.Add(new ReplicaRegionType() { Region = jObj["Region"]?.ToString().ToLower(), KmsKeyId = jObj["KmsKeyId"]?.ToString() });
            }

            logger.LogTrace($"found {JobParameters.CertProperties.ReplicaRegions.Count} Replica Regions(s)");

            logger.LogTrace($"loading certificate properties..");

            // get KmsKeyId (to use an encryption key other than the default)
            JobParameters.CertProperties.KmsKeyId = jobProperties["KmsKeyId"]?.ToString();

            JobParameters.CertProperties.Overwrite = overwrite;
            JobParameters.CertProperties.Alias = certProperties.Alias;
            JobParameters.CertProperties.PrivateKeyPassword = certProperties.PrivateKeyPassword;
            JobParameters.CertProperties.Thumbprint = certProperties.Thumbprint;
            JobParameters.CertProperties.Contents = certProperties.Contents;
            JobParameters.CertProperties.Description = jobProperties["Description"]?.ToString();

            logger.MethodExit();
        }

        private protected JobResult SuccessJobResult(string message = null)
        {
            return new JobResult()
            {
                Result = OrchestratorJobStatusJobResult.Success,
                JobHistoryId = JobParameters.JobHistoryId,
                FailureMessage = message
            };
        }

        private protected JobResult WarningJobResult(string message = null)
        {
            return new JobResult()
            {
                Result = OrchestratorJobStatusJobResult.Warning,
                JobHistoryId = JobParameters.JobHistoryId,
                FailureMessage = message
            };
        }

        private protected JobResult FailureJobResult(string message = null)
        {
            return new JobResult()
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                FailureMessage = message,
                JobHistoryId = JobParameters.JobHistoryId
            };
        }

        private string FlattenException(Exception ex)
        {
            string returnMessage = ex.Message;
            if (ex.InnerException != null)
            {
                returnMessage += (" - " + FlattenException(ex.InnerException));
            }
            return returnMessage;
        }

        private (string, string, string, string) ParseStorePath(string storePath)
        {

            var bracketStart = storePath.IndexOf('[');
            var bracketEnd = storePath.IndexOf(']');

            var bracketedText = storePath.Substring(bracketStart, bracketEnd - bracketStart);

            (var prefix, var tagName, var tagValue) = (string.Empty, string.Empty, string.Empty);

            if (bracketStart == -1)
            {
                return (storePath, prefix, tagName, tagValue);
            }

            // Regex pattern to capture the prefix or tag name/value pairs
            // It looks for content within square brackets [] and then tries to capture
            // either "prefix" or "tagName" and "tagValue"
            string pattern = @"\[(?:prefix=""(?<prefix>[^""]*)""|tagName=""(?<tagName>[^""]*)""\s+tagValue=""(?<tagValue>[^""]*)"")\]";

            // Create a Regex object with the IgnoreCase option
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);

            Match match = regex.Match(storePath);

            if (match.Success)
            {
                // Check if the "prefix" group was captured
                if (match.Groups["prefix"].Success)
                {
                    prefix = match.Groups["prefix"].Value;
                    
                    // remove any leading and trailing slashes
                    if (prefix.StartsWith("/") || prefix.StartsWith("\\"))
                    {
                        prefix = prefix.Substring(1);
                    }
                    if (prefix.EndsWith("/") || prefix.EndsWith("\\")) { prefix = prefix.Substring(0, prefix.Length - 1); }
                }
                // Check if the "tagName" and "tagValue" groups were captured
                else if (match.Groups["tagName"].Success && match.Groups["tagValue"].Success)
                {
                    tagName = match.Groups["tagName"].Value;
                    tagValue = match.Groups["tagValue"].Value;
                }
                storePath.Remove(bracketStart, bracketEnd);
            }
            else
            {
                // there were brackets, but none of the identifiers (tagName, tagValue or prefix)
                // log a warning, return full value
                logger.LogWarning($"store path ({storePath}) includes brackets but no 'tagName', 'tagValue' or 'prefix' qualifier.  Make sure the store path format is valid.");

            }
            return (storePath, prefix, tagName, tagValue);

        }
    }
}
