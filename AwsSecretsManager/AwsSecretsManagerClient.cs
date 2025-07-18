
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Keyfactor.Extensions.Aws;
using Keyfactor.Extensions.Aws.Models;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Ocsp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager
{
    public class AwsSecretsManagerClient
    {
        private ILogger _logger;
        private IAmazonSecretsManager _secretsManagerClient { get; set; }

        public AwsSecretsManagerClient()
        {
            _logger = LogHandler.GetClassLogger(GetType());
        }

        public void InitializeClient(AuthenticationParameters authParams, AwsAuthUtility authUtility)
        {
            _logger.MethodEntry();

            _logger.LogTrace($"getting the region endpoint from the string '{authParams.Region}'");

            var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(authParams.Region); // this will return it's best guess no matter what.. never null

            _logger.LogTrace($"resolved region endpoint {regionEndpoint.SystemName}");

            _logger.LogTrace("Resolving AWS Credentials object.");
            AwsExtensionCredential providedCredentials;
            try
            {
                providedCredentials = authUtility.GetCredentials(authParams);
            }
            catch (Exception)
            {
                _logger.LogError("An error occurred while trying to get AWS Credentials");
                throw;
            }
            _logger.LogTrace("creating an instance of the AmazonSecretsManagerClient");
            _secretsManagerClient = new AmazonSecretsManagerClient(providedCredentials.GetAwsCredentialObject(), providedCredentials.Region);

            _logger.MethodExit();
        }

        /// <summary>
        /// Lists all secrets that contain the provided Tag Key value.  
        /// We retreive these in batches of maximum 20 size and loop until we've gotten them all.
        /// </summary>
        public async Task<List<SecretValueEntry>> ListSecrets(List<Filter> filters)
        {
            _logger.MethodEntry();

            var results = new List<SecretValueEntry>();

            string nextToken = null;

            // TODO: retreive list of filtered secret names first, then need to retrieve the values in another operation.

            _logger.LogTrace($"begin batch retreival of secrets..");
            try
            {
                do
                {
                    var request = new BatchGetSecretValueRequest
                    {
                        Filters = filters,
                        MaxResults = 20,
                    };
                    if (nextToken != null)
                    {
                        request.NextToken = nextToken;
                    }

                    var response = await _secretsManagerClient.BatchGetSecretValueAsync(request);

                    results.AddRange(response.SecretValues);
                    nextToken = response.NextToken;
                    _logger.LogTrace($"got {response.SecretValues.Count} entries.");
                    _logger.LogTrace("retreiving next batch of up to 20 entries..");
                }
                while (!string.IsNullOrEmpty(nextToken));

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError($"There was an error when attempting to retreive the list of secrets: {LogHandler.FlattenException(ex)}");
                throw;
            }
            finally
            {
                _logger.MethodExit();
            }
        }

        /// <summary>
        /// Add secret first checks for the existence of a secret with the same name
        ///     if it exists, it checks the value of the overwrite flag
        ///         if it is true, 
        ///             we update the secret value, 
        ///             update the tags
        ///             update the replica regions.
        ///         if false, we stop.
        /// If the secret does not exist, we add it as new including the replica regions and tags.
        /// 
        /// </summary>
        /// <param name="secretName"></param>
        /// <param name="certProps"></param>
        /// <returns></returns>
        public async Task<string> AddOrUpdateSecret(string secretName, CertProperties certProps)
        {
            _logger.MethodEntry();
            _logger.LogTrace($"the certificate alias is '{certProps.Alias}'.  The resolved the secret name in AWS will be '{secretName}'");

            CreateSecretResponse resp;
            AmazonSecretsManagerRequest req;

            var replace = false;

            // first we need to check to see if a secret with the same name exists..

            _logger.LogTrace($"checking for existing secret named '{secretName}'");

            var existing = GetSecret(secretName);

            if (existing != null)
            {
                // there is an existing secret with the same name..

                _logger.LogTrace($"a secret with the name '{secretName}' exists.");

                if (!certProps.Overwrite)
                {
                    // and we should not overwrite it
                    _logger.LogTrace($"... and the 'overwrite' flag is false, the certificate will not be stored.");
                    return null;
                }
                else replace = true; // and we should overwrite it
            }

            if (replace)
            {
                _logger.LogTrace("the existing secret will be replaced");
                return await UpdateSecret(secretName, certProps);
            }
            else
            {
                // there is not a secret with the same name, we will add a new one.
                _logger.LogTrace($"no existing secret with the name '{secretName}' exists.  We will create a new one.");
                return await AddSecret(secretName, certProps);
            }
        }

        public async Task<GetSecretValueResponse> GetSecret(string secretName)
        {
            _logger.MethodEntry();

            var req = new GetSecretValueRequest();
            GetSecretValueResponse resp;
            req.SecretId = secretName;


            _logger.LogTrace($"attempting to retreive secret named {secretName}");

            // submit the request
            try
            {
                _logger.LogTrace($"sending request to AWS..");
                resp = await _secretsManagerClient.GetSecretValueAsync(req);
                _logger.LogTrace($"request was successful");
                if (resp.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogTrace($"the secret named {secretName} was not found.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"an error occurred when trying to retreive the secret named {secretName}.\n{ex.Message}");
                throw;
            }
            finally
            {
                _logger.MethodExit();
            }

            // return the ARN
            return resp;
        }

        private async Task<string> AddSecret(string secretName, CertProperties certProps)
        {
            _logger.MethodEntry();
            var req = new CreateSecretRequest() { Name = secretName };
            CreateSecretResponse resp;
            string pemCert;

            // get the PEM formatted string content from the base64pfx
            try
            {
                pemCert = CertUtilities.ConvertPfxToPem(certProps.Contents, certProps.PrivateKeyPassword);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Conversion failed: unable to convert certificate contents to PEM\n{ex.Message}");
                throw;
            }

            req.SecretString = pemCert;

            // include any provided tags
            var tags = certProps.Tags?.Select(t => new Tag { Key = t.Key, Value = t.Value })?.ToList();
            if (tags.Any()) req.Tags = tags;

            // include any explicit encryption key ID
            if (!string.IsNullOrEmpty(certProps.KmsKeyId)) req.KmsKeyId = certProps.KmsKeyId;

            // include any additional replica regions
            if (certProps.ReplicaRegions != null && certProps.ReplicaRegions.Any())
            {
                req.AddReplicaRegions = certProps.ReplicaRegions;
            }
            try
            {
                _logger.LogTrace($"sending request to AWS..");
                resp = await _secretsManagerClient.CreateSecretAsync(req);
                _logger.LogTrace($"successfully created secret containing the certificate\nARN: {resp.ARN}\nversion ID: {resp.VersionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"an error occurred when trying to add the certificate.\n{ex.Message}");
                throw;
            }
            finally
            {
                _logger.MethodExit();
            }
            return resp?.ARN;
        }

        /// <summary>
        /// Update existing secret with a new version
        /// then apply tags and replica regions from entry parameters.
        /// </summary>
        /// <param name="certName"></param>
        /// <param name="certProps"></param>
        /// <returns></returns>
        private async Task<string> UpdateSecret(string secretName, CertProperties certProps)
        {
            _logger.MethodEntry();

            var req = new UpdateSecretRequest() { SecretId = secretName };

            // include any explicit encryption key ID
            if (!string.IsNullOrEmpty(certProps.KmsKeyId)) req.KmsKeyId = certProps.KmsKeyId;

            string pemCert;
            UpdateSecretResponse resp;

            // get the PEM formatted string content from the base64pfx
            try
            {
                pemCert = CertUtilities.ConvertPfxToPem(certProps.Contents, certProps.PrivateKeyPassword);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Conversion failed: unable to convert certificate contents to PEM\n{ex.Message}");
                throw;
            }

            req.SecretString = pemCert;

            // include any provided tags
            var tags = certProps.Tags?.Select(t => new Tag { Key = t.Key, Value = t.Value })?.ToList();
            if (tags.Any())
            {
                await ReplaceSecretTagsAsync(secretName, tags);
            }

            // include any additional replica regions
            try
            {
                if (certProps.ReplicaRegions.Any())
                {
                    _logger.LogTrace("replica regions were provided, replacing any existing replica regions..");
                    await ReplaceSecretReplicaRegionsAsync(secretName, certProps.ReplicaRegions);
                }
            }
            catch (Exception ex) 
            {
                _logger.LogTrace($"there was an error when attempting to replace the secret replica regions. {ex.Message}");
                throw;
            }
            try
            {
                _logger.LogTrace($"sending request to AWS..");
                resp = await _secretsManagerClient.UpdateSecretAsync(req);
                _logger.LogTrace($"successfully created secret containing the certificate\nARN: {resp.ARN}\nversion ID: {resp.VersionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"an error occurred when trying to add the certificate.\n{ex.Message}");
                throw;
            }
            finally
            {
                _logger.MethodExit();
            }
            return resp?.ARN;
        }

        public async Task RemoveSecret(string secretName)
        {
            _logger.MethodEntry();

            var req = new DeleteSecretRequest();
            DeleteSecretResponse resp;

            try
            {
                req.SecretId = secretName;

                _logger.LogTrace($"secret id to remove: '{req.SecretId}'");

                _logger.LogTrace($"submitting request to delete secret..");

                resp = await _secretsManagerClient.DeleteSecretAsync(req);

                _logger.LogTrace($"successfully removed secret with ARN {resp.ARN}");

            }
            catch (Exception ex)
            {
                _logger.LogError($"an error occurred when attempting to delete the secret\n{ex.Message}");
                throw;
            }
            finally
            {
                _logger.MethodExit();
            }
        }

        /// <summary>
        /// Replaces all tags on an existing AWS Secrets Manager secret with the provided tags
        /// </summary>
        /// <param name="secretName">The name or ARN of the secret</param>
        /// <param name="newTags">Dictionary of tag names and values to replace existing tags</param>
        /// <returns>Task representing the async operation</returns>
        private async Task ReplaceSecretTagsAsync(string secretName, List<Tag> newTags)
        {
            if (string.IsNullOrEmpty(secretName))
                throw new ArgumentException("Secret name cannot be null or empty", nameof(secretName));

            if (newTags == null)
                throw new ArgumentNullException(nameof(newTags));
                        
            try
            {
                // First, get the current tags on the secret
                var describeRequest = new DescribeSecretRequest
                {
                    SecretId = secretName
                };

                var describeResponse = await _secretsManagerClient.DescribeSecretAsync(describeRequest);
                var currentTags = describeResponse.Tags ?? new List<Tag>();

                // Remove all existing tags if any exist
                if (currentTags.Any())
                {
                    var untagRequest = new UntagResourceRequest
                    {
                        SecretId = secretName,
                        TagKeys = currentTags.Select(tag => tag.Key).ToList()
                    };

                    await _secretsManagerClient.UntagResourceAsync(untagRequest);
                }

                // Add the new tags if any are provided
                if (newTags.Any())
                {
                    var tagRequest = new TagResourceRequest
                    {
                        SecretId = secretName,
                        Tags = newTags.Select(kvp => new Tag
                        {
                            Key = kvp.Key,
                            Value = kvp.Value
                        }).ToList()
                    };

                    await _secretsManagerClient.TagResourceAsync(tagRequest);
                }
            }
            catch (ResourceNotFoundException ex)
            {
                throw new InvalidOperationException($"Secret '{secretName}' not found", ex);
            }
            catch (InvalidParameterException ex)
            {
                throw new ArgumentException($"Invalid parameter provided: {ex.Message}", ex);
            }
            catch (InvalidRequestException ex)
            {
                throw new InvalidOperationException($"Invalid request: {ex.Message}", ex);
            }
            catch (AmazonSecretsManagerException ex)
            {
                throw new InvalidOperationException($"AWS Secrets Manager error: {ex.Message}", ex);
            }
        }


        /// <summary>
        /// Updates replica regions for an existing AWS Secrets Manager secret with detailed configuration
        /// </summary>
        /// <param name="secretName">The name or ARN of the secret</param>
        /// <param name="replicaConfigs">List of replica region configurations</param>
        /// <returns>Task representing the async operation</returns>
        public async Task ReplaceSecretReplicaRegionsAsync(string secretName, List<ReplicaRegionType> replicaConfigs)
        {
            if (string.IsNullOrEmpty(secretName))
                throw new ArgumentException("Secret name cannot be null or empty", nameof(secretName));

            if (replicaConfigs == null)
                throw new ArgumentNullException(nameof(replicaConfigs));

            try
            {
                // First, get the current replica configuration
                var describeRequest = new DescribeSecretRequest
                {
                    SecretId = secretName
                };

                var describeResponse = await _secretsManagerClient.DescribeSecretAsync(describeRequest);
                var currentReplicas = describeResponse.ReplicationStatus ?? new List<ReplicationStatusType>();

                // Determine which replicas to add and which to remove
                var currentRegions = currentReplicas.Select(r => r.Region).ToHashSet();
                var targetRegions = replicaConfigs.Select(r => r.Region).ToHashSet();

                var regionsToAdd = targetRegions.Except(currentRegions).ToList();
                var regionsToRemove = currentRegions.Except(targetRegions).ToList();

                // Remove replicas that are no longer needed
                if (regionsToRemove.Any())
                {
                    var removeRequest = new RemoveRegionsFromReplicationRequest
                    {
                        SecretId = secretName,
                        RemoveReplicaRegions = regionsToRemove
                    };

                    await _secretsManagerClient.RemoveRegionsFromReplicationAsync(removeRequest);
                }

                // Add new replicas
                if (regionsToAdd.Any())
                {
                    var addReplicaConfigs = replicaConfigs
                        .Where(config => regionsToAdd.Contains(config.Region))
                        .ToList();

                    var replicateRequest = new ReplicateSecretToRegionsRequest
                    {
                        SecretId = secretName,
                        AddReplicaRegions = addReplicaConfigs
                    };

                    await _secretsManagerClient.ReplicateSecretToRegionsAsync(replicateRequest);
                }
            }
            catch (ResourceNotFoundException ex)
            {
                throw new InvalidOperationException($"Secret '{secretName}' not found", ex);
            }
            catch (InvalidParameterException ex)
            {
                throw new ArgumentException($"Invalid parameter provided: {ex.Message}", ex);
            }
            catch (InvalidRequestException ex)
            {
                throw new InvalidOperationException($"Invalid request: {ex.Message}", ex);
            }
            catch (AmazonSecretsManagerException ex)
            {
                throw new InvalidOperationException($"AWS Secrets Manager error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Updates replica regions with KMS key specifications
        /// </summary>
        /// <param name="secretName">The name or ARN of the secret</param>
        /// <param name="replicaRegionKmsKeys">Dictionary of region to KMS key ARN mappings</param>
        /// <returns>Task representing the async operation</returns>
        private async Task UpdateSecretReplicasWithKmsAsync(string secretName, Dictionary<string, string> replicaRegionKmsKeys)
        {
            if (replicaRegionKmsKeys == null)
                throw new ArgumentNullException(nameof(replicaRegionKmsKeys));

            var replicaConfigs = replicaRegionKmsKeys.Select(kvp => new ReplicaRegionType
            {
                Region = kvp.Key,
                KmsKeyId = kvp.Value
            }).ToList();

            await ReplaceSecretReplicaRegionsAsync(secretName, replicaConfigs);
        }

        /// <summary>
        /// Removes all replicas from a secret (keeps only the primary region)
        /// </summary>
        /// <param name="secretName">The name or ARN of the secret</param>
        /// <returns>Task representing the async operation</returns>
        private async Task RemoveAllReplicasAsync(string secretName)
        {
            if (string.IsNullOrEmpty(secretName))
                throw new ArgumentException("Secret name cannot be null or empty", nameof(secretName));

            try
            {
                var describeRequest = new DescribeSecretRequest
                {
                    SecretId = secretName
                };

                var describeResponse = await _secretsManagerClient.DescribeSecretAsync(describeRequest);
                var currentReplicas = describeResponse.ReplicationStatus ?? new List<ReplicationStatusType>();

                if (currentReplicas.Any())
                {
                    var regionsToRemove = currentReplicas.Select(r => r.Region).ToList();

                    var removeRequest = new RemoveRegionsFromReplicationRequest
                    {
                        SecretId = secretName,
                        RemoveReplicaRegions = regionsToRemove
                    };

                    await _secretsManagerClient.RemoveRegionsFromReplicationAsync(removeRequest);
                }
            }
            catch (AmazonSecretsManagerException ex)
            {
                throw new InvalidOperationException($"Failed to remove replicas: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the current replication status of a secret
        /// </summary>
        /// <param name="secretName">The name or ARN of the secret</param>
        /// <returns>List of current replica regions and their status</returns>
        public async Task<List<ReplicationStatusType>> GetReplicationStatusAsync(string secretName)
        {
            if (string.IsNullOrEmpty(secretName))
                throw new ArgumentException("Secret name cannot be null or empty", nameof(secretName));

            try
            {
                var describeRequest = new DescribeSecretRequest
                {
                    SecretId = secretName
                };

                var describeResponse = await _secretsManagerClient.DescribeSecretAsync(describeRequest);
                return describeResponse.ReplicationStatus ?? new List<ReplicationStatusType>();
            }
            catch (AmazonSecretsManagerException ex)
            {
                throw new InvalidOperationException($"Failed to get replication status: {ex.Message}", ex);
            }
        }
    }
}
