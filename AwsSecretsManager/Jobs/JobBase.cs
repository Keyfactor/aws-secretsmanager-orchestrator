
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

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
using ILogger = Microsoft.Extensions.Logging.ILogger;


namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Jobs
{
    public class JobBase<T> : IOrchestratorJobExtension
    {
        public string ExtensionName => "AWSSM";
        internal ILogger _logger { get; set; }
        internal IPAMSecretResolver _resolver { get; set; }
        public virtual AwsSecretsManagerClient _secretsManagerClient { get; set; }
        internal AwsAuthUtility _authUtility { get; set; }
        internal protected virtual AwsSecretsManagerJobParameters JobParameters { get; set; }

        public JobBase(IPAMSecretResolver resolver)
        {
            _logger = LogHandler.GetClassLogger(GetType());
            _resolver = resolver;
            _authUtility = new AwsAuthUtility(resolver);
            _secretsManagerClient = new AwsSecretsManagerClient();
        }

        // set the configuration parameters
        public virtual void Initialize(InventoryJobConfiguration config)
        {
            _logger.MethodEntry();
            _logger.LogTrace($"reading serialized configuration passed from Command to create the AwsSecretsManagerJobParameters object..");            
            JobParameters = new AwsSecretsManagerJobParameters();
            JobParameters.StoreType = config.Capability.Split('.')[1] ?? null;
            JobParameters.JobType = "Inventory";
            JobParameters.JobId = config.JobId;
            JobParameters.JobHistoryId = config.JobHistoryId;

            SetStoreProperties(config.CertificateStoreDetails);
            _logger.LogTrace("successfully set the store properties");

            InitializeAwsClient(config.CertificateStoreDetails);

            _logger.LogTrace("Inventory job initialization complete");
            _logger.LogTrace($"storetype is {JobParameters.StoreType}");

            _logger.MethodExit();
        }

        public virtual void Initialize(ManagementJobConfiguration config)
        {
            _logger.MethodEntry();

            _logger.LogTrace($"reading serialized configuration passed from Command to create the AwsSecretsManagerJobParameters object..");

            JobParameters = new AwsSecretsManagerJobParameters();
            JobParameters.JobType = "Management";
            JobParameters.JobId = config.JobId;
            JobParameters.JobHistoryId = config.JobHistoryId;

            SetStoreProperties(config.CertificateStoreDetails);

            SetCertProperties(config.JobProperties, config.JobCertificate, config.Overwrite);

            InitializeAwsClient(config.CertificateStoreDetails);

            _logger.LogTrace($"parameter initialization for Management > {config.OperationType.ToString()} job complete");

            _logger.MethodExit();
        }

        private protected void SetStoreProperties(CertificateStore storeProps)
        {
            _logger.MethodEntry();

            var roleName = storeProps.ClientMachine; // Fully qualified ARN of the role to assume; might be prefixed with [profile]; handled by auth library
            _logger.LogTrace("parsing tags, prefix, and region from the store path..");
            (var awsRegion, var prefix, var tagName, var tagValue) = ParseStorePath(storeProps.StorePath);

            _logger.LogTrace(
                @$"parsed the following from storepath: 
                prefix: {prefix ?? "(not provided)"}
                tagName: {tagName ?? "(not provided)"}
                tagValue: {tagValue ?? "(not provided)"}
                awsRegion: {awsRegion}");

            JobParameters.StoreProperties.AwsRegion = awsRegion;

            _logger.LogTrace("determining identification strategy for this cert store (tags, prefix, or full region)..");

            if (!string.IsNullOrEmpty(tagName))
            {
                // using tags
                if (string.IsNullOrEmpty(tagValue))
                {
                    throw new MissingFieldException("tagName is defined, but tagValue is missing.  Both are needed to filter by tags.");
                }
                _logger.LogTrace("using tag name and tag value (from store path)");
                JobParameters.StoreProperties.TagName = tagName;
                JobParameters.StoreProperties.TagValue = tagValue;
            }
            else
            {
                _logger.LogTrace($"tag is undefined, checking for path value..");

                if (!string.IsNullOrEmpty(prefix) && prefix?.Trim() != "/" && prefix?.Trim() != "\\")
                {
                    _logger.LogTrace($"using path prefix '{prefix}' in secret name");
                    JobParameters.StoreProperties.NamePrefix = prefix;
                }
            }
        }

        private void InitializeAwsClient(CertificateStore storeProps)
        {
            _logger.MethodEntry();
            _logger.LogTrace("deserializing store properties..");
            _logger.LogTrace($"raw value: {storeProps.Properties}");
            AuthCustomFieldParameters customFields;
            try
            {
                customFields = JsonConvert.DeserializeObject<AuthCustomFieldParameters>(storeProps.Properties,
                        new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate });
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred when attempting to deserialize the store properties:  {ex.Message}");
                throw;
            }
            _logger.LogTrace("successfully deserialized the store properties");

            AuthenticationParameters authParams = new AuthenticationParameters
            {
                RoleARN = storeProps.ClientMachine,
                Region = JobParameters.StoreProperties.AwsRegion,
                CustomFields = customFields
            };

            _secretsManagerClient.InitializeClient(authParams, _authUtility);

            _logger.MethodExit();
        }

        private void SetCertProperties(Dictionary<string, object> jobProperties, ManagementJobCertificate certProperties, bool overwrite = false)
        {
            _logger.MethodEntry();

            // get cert tags entry parameter;  format = [{Region: "<region name>", KmsKeyId: "<key id>"}, ...]

            _logger.LogTrace("getting certificate tag values, if any..");

            if (jobProperties.ContainsKey("Tags") && !string.IsNullOrEmpty(jobProperties["Tags"] as string))
            {
                var tagsJSON = jobProperties["Tags"]?.ToString();
                var jObj = JObject.Parse(tagsJSON);
                var tagDict = new Dictionary<string, string>();


                foreach (var tag in jObj)
                {
                    tagDict.Add(tag.Key, (string)tag.Value);
                }

                _logger.LogTrace($"found {tagDict.Count} Tag(s)");

                JobParameters.CertProperties.Tags = tagDict;
            }

            // get replica regions entry parameter;  format =  [{Region: "<region name>", KmsKeyId: "<key id>"}, ...]            

            if (jobProperties.ContainsKey("ReplicaRegions") && !string.IsNullOrEmpty(jobProperties["ReplicaRegions"] as string))
            {
                var replicaRegionsJSON = jobProperties["ReplicaRegions"]?.ToString();
                _logger.LogTrace($"getting replica region values, if any, from the JSON string '{replicaRegionsJSON}'");

                var jObj = JObject.Parse(replicaRegionsJSON);
                JobParameters.CertProperties.ReplicaRegions = new List<ReplicaRegionType>();

                foreach (var replicaRegion in jObj)
                {
                    JobParameters.CertProperties.ReplicaRegions.Add(new ReplicaRegionType() { Region = jObj["Region"]?.ToString().ToLower(), KmsKeyId = jObj["KmsKeyId"]?.ToString() });
                }

                _logger.LogTrace($"found {JobParameters.CertProperties.ReplicaRegions.Count} Replica Regions(s)");
            }
            _logger.LogTrace($"loading certificate properties..");

            // get KmsKeyId (to use an encryption key other than the default)
            JobParameters.CertProperties.KmsKeyId = jobProperties.ContainsKey("KmsKeyId") ? jobProperties["KmsKeyId"]?.ToString() : null;
            JobParameters.CertProperties.Overwrite = overwrite;
            JobParameters.CertProperties.Alias = certProperties.Alias;
            JobParameters.CertProperties.PrivateKeyPassword = certProperties.PrivateKeyPassword;
            JobParameters.CertProperties.Thumbprint = certProperties.Thumbprint;
            JobParameters.CertProperties.Contents = certProperties.Contents;
            JobParameters.CertProperties.Description = jobProperties.ContainsKey("Description") ? jobProperties["Description"].ToString() : null;

            _logger.MethodExit();
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

        //private (string, string, string, string) ParseStorePath(string awsRegion)
        //{
        //    _logger.LogTrace($"parsing values from storepath \"{awsRegion}\"");
        //    var bracketStart = awsRegion.IndexOf('[');
        //    var bracketEnd = awsRegion.IndexOf(']');
        //    (var prefix, var tagName, var tagValue) = (string.Empty, string.Empty, string.Empty);

        //    if (bracketStart == -1)
        //    {
        //        return (awsRegion, prefix, tagName, tagValue); // if no brackets, no need to perform the search and parse
        //    }

        //    // Regex pattern to capture the prefix or tag name/value pairs
        //    // It looks for bracketContent within square brackets [] and then tries to capture
        //    // either "prefix" or "tagName" and "tagValue"
        //    string pattern = @"\[(?:prefix=""(?<prefix>[^""]*)""|tagName=""(?<tagName>[^""]*)""\s+tagValue=""(?<tagValue>[^""]*)"")\]";

        //    // Create a Regex object with the IgnoreCase option
        //    Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);

        //    Match match = regex.Match(awsRegion);

        //    if (match.Success)
        //    {
        //        // Check if the "prefix" group was captured
        //        if (match.Groups["prefix"].Success)
        //        {
        //            prefix = match.Groups["prefix"].Value.Trim();

        //            // remove any leading and trailing slashes
        //            if (prefix.StartsWith("/") || prefix.StartsWith("\\"))
        //            {
        //                prefix = prefix.Substring(1);
        //            }
        //            if (prefix.EndsWith("/") || prefix.EndsWith("\\")) { prefix = prefix.Substring(0, prefix.Length - 1); }
        //        }
        //        // Check if the "tagName" and "tagValue" groups were captured
        //        else if (match.Groups["tagName"].Success && match.Groups["tagValue"].Success)
        //        {
        //            tagName = match.Groups["tagName"].Value;
        //            tagValue = match.Groups["tagValue"].Value;
        //        }
        //        awsRegion.Remove(bracketStart, bracketEnd);
        //    }
        //    else
        //    {
        //        // there were brackets, but none of the identifiers (tagName, tagValue or prefix)
        //        // log a warning, return full value
        //        _logger.LogWarning($"store path ({awsRegion}) includes brackets but no 'tagName', 'tagValue' or 'prefix' qualifier.  Make sure the store path format is valid.");

        //    }

        //    if (string.IsNullOrEmpty(tagName) && !string.IsNullOrEmpty(tagValue)) {
        //        var errorMsg = $"if tagValue is provided, tagName must also be provided.";
        //        _logger.LogError(errorMsg);
        //        throw new Exception(errorMsg);
        //    }

        //    return (awsRegion, prefix, tagName, tagValue);

        //}

        /// <summary>
        /// parses the optional prefix, tagName and tagValue parameters from the store path
        /// examples: 
        ///     us-east-2 [prefix="dev/testing/"]
        ///     us-east-1 [prefix="web/certs/" tagName="managedBy" tagValue="Keyfactor"]
        ///     us-west-1
        /// </summary>
        /// <param name="storePath"></param>
        /// <returns>(storepath, prefix, tagName, tagValue)</returns>
        private (string, string, string, string) ParseStorePath(string storePath)
        {

            string prefix = null;
            string tagName = null;
            string tagValue = null;
            string cleanPath = storePath;

            var startBracketIndex = storePath.IndexOf('[');
            var endBracketIndex = storePath.IndexOf(']');
            var bracketLength = endBracketIndex - startBracketIndex;

            if (bracketLength <= 0)
            {
                return (cleanPath, prefix, tagName, tagValue);
            }

            var bracketContent = storePath.Substring(startBracketIndex, endBracketIndex - 2);
            var attributes = new Dictionary<string, string>();

            // Split by quotes and extract key-value pairs
            var parts = bracketContent.Split('"');
            for (int i = 0; i < parts.Length - 1; i += 2)
            {
                var keyPart = parts[i].Trim();
                if (keyPart.EndsWith("="))
                {
                    var key = keyPart.Substring(0, keyPart.Length - 1);
                    var value = parts[i + 1];
                    attributes[key] = value;
                }
            }

            // Validate combinations
            if (attributes.ContainsKey("prefix"))
                prefix = attributes["prefix"];

            if (attributes.ContainsKey("tagName"))
                tagName = attributes["tagName"];

            if (attributes.ContainsKey("tagValue"))
                tagValue = attributes["tagValue"];

            return (storePath, prefix, tagName, tagValue);
        }

    }
}
