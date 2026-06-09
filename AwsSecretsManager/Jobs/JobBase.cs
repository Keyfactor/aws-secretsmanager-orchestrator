
//  Copyright 2026 Keyfactor
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
using System.Linq;
using System.Reflection;
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
            LogPluginVersion();
            _logger.LogTrace($"reading serialized configuration passed from Command to create the AwsSecretsManagerJobParameters object..");
            JobParameters = new AwsSecretsManagerJobParameters();
            JobParameters.StoreType = config.Capability.Split('.')[1] ?? null;
            _logger.LogTrace($"storeType: {JobParameters.StoreType}");
            JobParameters.JobType = "Inventory";
            JobParameters.JobId = config.JobId;
            JobParameters.JobHistoryId = config.JobHistoryId;

            SetStoreProperties(config.CertificateStoreDetails);
            _logger.LogTrace("successfully set the store properties");

            InitializeAwsClient(config.CertificateStoreDetails);
            _logger.LogTrace("Inventory job initialization complete");

            _logger.MethodExit();
        }

        protected void LogPluginVersion()
        {
            var targetAssembly = Assembly.GetExecutingAssembly();
            var assemblyName = targetAssembly?.GetName();
            var version = assemblyName?.Version;
            _logger.LogTrace("Keyfactor Orchestrator Extension for AWS Secrets Manager");
            _logger.LogTrace($"{assemblyName?.Name ?? "unknown"} v{version}");
        }

        public virtual void Initialize(ManagementJobConfiguration config)
        {
            _logger.MethodEntry();
            LogPluginVersion();

            _logger.LogTrace($"reading serialized configuration passed from Command to create the AwsSecretsManagerJobParameters object..");

            JobParameters = new AwsSecretsManagerJobParameters();
            JobParameters.StoreType = config.Capability.Split('.')[1] ?? null;
            _logger.LogTrace($"storeType: {JobParameters.StoreType}");
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
                JobParameters.StoreProperties.TagName = tagName;

                if (!string.IsNullOrEmpty(tagValue))
                {
                    JobParameters.StoreProperties.TagValue = tagValue;
                }
                _logger.LogTrace($"using tag name \"{tagName}\" and tag value \"{tagValue}\" from store path");
            }
            else
            {
                _logger.LogTrace($"tag is undefined, checking for path value..");
            }

            if (!string.IsNullOrEmpty(prefix) && prefix?.Trim() != "/" && prefix?.Trim() != "\\")
            {
                _logger.LogTrace($"using path prefix '{prefix}' in secret name");
                JobParameters.StoreProperties.NamePrefix = prefix;
            }

            // read the SeparatePrivateKey custom field (AWSSMPEM JSON split format)
            JobParameters.StoreProperties.SeparatePrivateKey =
                ReadBoolStoreProperty(storeProps.Properties, StorePropertyNames.SEPARATE_PRIVATE_KEY);
            _logger.LogTrace($"SeparatePrivateKey = {JobParameters.StoreProperties.SeparatePrivateKey}");
        }

        /// <summary>
        /// Reads a boolean store-type custom field from the serialized Properties JSON.
        /// Tolerant of Command serializing the value as a real bool, a "true"/"false" string,
        /// or an object wrapping the value (e.g. { "value": "true" }).  Defaults to false.
        /// </summary>
        private bool ReadBoolStoreProperty(string propertiesJson, string name)
        {
            if (string.IsNullOrWhiteSpace(propertiesJson)) return false;

            try
            {
                var jObj = JObject.Parse(propertiesJson);
                if (!jObj.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var token) || token == null)
                    return false;

                if (token.Type == JTokenType.Object)
                    token = token["value"];

                if (token == null || token.Type == JTokenType.Null)
                    return false;

                if (token.Type == JTokenType.Boolean)
                    return token.Value<bool>();

                return bool.TryParse(token.ToString(), out var parsed) && parsed;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"unable to parse store property '{name}'; defaulting to false. {ex.Message}");
                return false;
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

            _logger.LogTrace($"set AWS auth params to: \nRoleArn = {authParams.RoleARN}\nRegion={authParams.Region}\nCustomFields={authParams.CustomFields}");

            _secretsManagerClient.InitializeClient(authParams, _authUtility);

            _logger.MethodExit();
        }

        private void SetCertProperties(Dictionary<string, object> jobProperties, ManagementJobCertificate certProperties, bool overwrite = false)
        {
            _logger.MethodEntry();

            // get cert tags entry parameter;  format = [{Region: "<region name>", KmsKeyId: "<key id>"}, ...]

            _logger.LogTrace("getting certificate tag values, if any..");
            var keys = jobProperties.Keys.ToList();
            var vals = jobProperties.Values.ToList();

            _logger.LogTrace($"raw jobproperties keys: {string.Join(',', keys)}");
            _logger.LogTrace($"values: {string.Join(',', vals)}");

            if (jobProperties.ContainsKey(EntryParameterKeys.TAGS) && !string.IsNullOrEmpty(jobProperties[EntryParameterKeys.TAGS] as string))
            {
                var tagsJSON = jobProperties[EntryParameterKeys.TAGS]?.ToString();
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

            if (jobProperties.ContainsKey(EntryParameterKeys.REPLICAREGIONS) && !string.IsNullOrEmpty(jobProperties[EntryParameterKeys.REPLICAREGIONS] as string))
            {
                var replicaRegionsJSON = jobProperties[EntryParameterKeys.REPLICAREGIONS]?.ToString();
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
            _logger.MethodEntry();

            _logger.LogTrace($"parsing storepath \"{storePath}\"");
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

            _logger.LogTrace($"start bracket index: {startBracketIndex}, end bracket index: {endBracketIndex}");

            var bracketContent = storePath.Substring(startBracketIndex + 1, endBracketIndex - startBracketIndex - 1);
            var attributes = new Dictionary<string, string>();
            _logger.LogTrace($"bracket content: {bracketContent}");

            // Split by quotes and extract key-value pairs
            var parts = bracketContent.Split('"');
            for (int i = 0; i < parts.Length - 1; i += 2)
            {
                var keyPart = parts[i].Trim();
                if (keyPart.EndsWith("="))
                {
                    var key = keyPart.Substring(0, keyPart.Length - 1);
                    var value = parts[i + 1];
                    attributes[key.ToUpper()] = value;
                }
            }

            cleanPath = storePath.Replace(bracketContent, string.Empty)
                .Replace("[", string.Empty)
                .Replace("]", string.Empty)
                .Trim();

            _logger.LogTrace($"cleaned store path (AWS region): {cleanPath}");
            // get region from storepath


            // Validate combinations
            if (attributes.ContainsKey("PREFIX"))
                prefix = attributes["PREFIX"];

            if (attributes.ContainsKey("TAGNAME"))
                tagName = attributes["TAGNAME"];

            if (attributes.ContainsKey("TAGVALUE"))
                tagValue = attributes["TAGVALUE"];

            return (cleanPath, prefix, tagName, tagValue);
        }

    }
}
