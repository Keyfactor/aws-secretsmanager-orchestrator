
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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            catch (Exception ex)
            {
                _logger.LogError("An error occurred while trying to get AWS Credentials");
                _logger.LogTrace(ex.Message);
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
            var secretNames = new List<string>();

            string nextToken = null;

            // get the secret names

            try
            {
                _logger.LogTrace("retreiving secret names...");

                do
                {
                    var request = new ListSecretsRequest
                    {
                        Filters = filters,
                        MaxResults = 100
                    };
                    var secretNamesResponse = await _secretsManagerClient.ListSecretsAsync(request);
                    nextToken = secretNamesResponse.NextToken;
                    secretNames.AddRange(secretNamesResponse.SecretList?.Select(s => s.Name));
                }
                while (nextToken != null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"an error occurred when attempting to retreive the list of secret names.");
                _logger.LogError($"{LogHandler.FlattenException(ex)}");
                throw;
            }

            _logger.LogTrace($"got {secretNames.Count} secret names using the applied filters.");

            try
            {
                _logger.LogTrace($"begin batch retreival of (up to 20) secret values..");
                do
                {
                    if (secretNames.Count < 1) continue;

                    var request = new BatchGetSecretValueRequest
                    {
                        SecretIdList = secretNames
                    };

                    var response = await _secretsManagerClient.BatchGetSecretValueAsync(request);
                    results.AddRange(response.SecretValues);
                    nextToken = response.NextToken;
                    _logger.LogTrace($"got {response.SecretValues.Count} secret value entries.");
                    _logger.LogTrace("retreiving next batch of up to 20 entries..");
                }
                while (nextToken != null);
                _logger.LogTrace("completed secret value retreival");
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
        public async Task<string> AddOrUpdateSecret(AwsSecretsManagerJobParameters jobParameters)
        {
            _logger.MethodEntry();
            _logger.LogTrace($"the certificate alias is '{jobParameters.CertProperties.Alias}'.  The resolved the secret name in AWS will be '{jobParameters.SecretName}'");

            var replace = false;

            // first we need to check to see if a secret with the same name exists..

            _logger.LogTrace($"checking for existing secret named '{jobParameters.SecretName}'");

            var exists = await SecretExists(jobParameters.SecretName);

            if (exists)
            {
                // there is an existing secret with the same name..

                _logger.LogTrace($"a secret with the name '{jobParameters.SecretName}' exists.");

                if (!jobParameters.CertProperties.Overwrite)
                {
                    // and we should not overwrite it
                    _logger.LogTrace($"... and the 'overwrite' flag is false, the certificate will not be stored.");
                    throw new ResourceExistsException($"a secret named {jobParameters.SecretName} already exists, and overwrite is false.  No action taken.");
                }
                else replace = true; // and we should overwrite it
            }

            if (replace)
            {
                _logger.LogTrace("the existing secret will be replaced");
                return await UpdateSecret(jobParameters);
            }
            else
            {
                // there is not a secret with the same name, we will add a new one.
                _logger.LogTrace($"no existing secret with the name '{jobParameters.SecretName}' exists.  We will create a new one.");
                return await AddSecret(jobParameters);
            }
        }

        public async Task<bool> SecretExists(string secretName)
        {
            _logger.MethodEntry();

            var req = new ListSecretsRequest();
            req.Filters = new List<Filter>();

            ListSecretsResponse resp;
            var nameFilter = new Filter()
            {
                Key = AWSFilterParameter.NAME, // searches prefix (not full match) by default
                Values = new List<string>() { secretName }
            };

            req.Filters.Add(nameFilter);

            _logger.LogTrace($"attempting to retreive secret named {secretName}");

            // submit the request
            try
            {
                _logger.LogTrace($"sending request to AWS..");
                resp = await _secretsManagerClient.ListSecretsAsync(req);
                _logger.LogTrace($"request was successful");               

                _logger.LogTrace($"returned {resp.SecretList.Count} secret(s) beginning with \"{secretName}\"");

                if (resp.HttpStatusCode == System.Net.HttpStatusCode.NotFound || resp.SecretList == null || resp.SecretList.Count < 1)
                {
                    _logger.LogTrace($"the secret named {secretName} was not found.");
                    return false;
                }

                // AWS will return any secrets _beginning_ with the string, so we now have to check for an exact match..
                if (!resp.SecretList.Any(s => s.Name == secretName)) return false;
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
            return true;
        }

        private async Task<string> AddSecret(AwsSecretsManagerJobParameters jobParameters)
        {
            _logger.MethodEntry();
            CreateSecretRequest req = null;
            CreateSecretRequest pwdReq = null;
            CreateSecretResponse resp;

            switch (jobParameters.StoreType)
            {
                case "AWSSMPEM":
                    req = GenerateAddSecretPemRequest(jobParameters);
                    break;
                case "AWSSMPFX":
                    (req, pwdReq) = GenerateAddSecretPfxRequest(jobParameters);
                    break;
                case "AWSSMJKS":
                    (req, pwdReq) = GenerateAddSecretJksRequest(jobParameters);
                    break;
                default:
                    throw new ArgumentException($"Invalid store type {jobParameters.StoreType}");
            }

            // include any provided tags
            // gather any provided tags
            // for Updates, these need to be applied in a subsequent request
            // if we are using a tag for identification per cert store definition, we will add it.

            _logger.LogTrace("applying tags to request if necessary..");

            var tags = req.Tags ?? new List<Tag>(); // preserve any tags set when generating the request

            if (jobParameters.StoreProperties.UseTags)
            {
                var idTag = new Tag { Key = jobParameters.StoreProperties.TagName };
                idTag.Value = jobParameters.StoreProperties.TagValue ?? string.Empty; // a tag value is not required; so may not exist
            
            tags.Add(idTag);
                _logger.LogTrace($"included tag for identification.  Key = \"{idTag.Key}\", Value = \"{idTag.Value ?? ""}\"");
            }

            var entryTags = jobParameters.CertProperties.Tags?.Select(t => {
                var tag = new Tag { Key = t.Key };
                tag.Value = t.Value ?? string.Empty;
                return tag;
                })?.ToList();

            tags.AddRange(entryTags);

            if (tags.Any())
            {
                _logger.LogTrace("Adding the following tag values:");
                tags.ForEach(t => _logger.LogTrace($"Tag: {t.Key}, Value: {t.Value}"));

                req.Tags = tags; // when adding a secret, tags can be included in the request.
            }

            // include any explicit encryption key ID
            if (!string.IsNullOrEmpty(jobParameters.CertProperties.KmsKeyId)) req.KmsKeyId = jobParameters.CertProperties.KmsKeyId;

            // include any additional replica regions
            if (jobParameters.CertProperties.ReplicaRegions != null && jobParameters.CertProperties.ReplicaRegions.Any())
            {
                req.AddReplicaRegions = jobParameters.CertProperties.ReplicaRegions;
            }

            try
            {
                _logger.LogTrace($"writing the secret{(pwdReq != null ? "s":"")} to AWS..");
                
                if (pwdReq != null)
                {
                    _logger.LogTrace($"we will first write the secret containing the password as {pwdReq.Name}");
                    resp = await _secretsManagerClient.CreateSecretAsync(pwdReq);
                    _logger.LogTrace($"successfully created secret containing the password\nARN: {resp.ARN}\nversion ID: {resp.VersionId}");
                }

                _logger.LogTrace($"sending request to create cert secret named {req.Name}");
                resp = await _secretsManagerClient.CreateSecretAsync(req);
                _logger.LogTrace($"successfully created secret containing the certificate\nARN: {resp.ARN}\nversion ID: {resp.VersionId}");                
            }
            catch (Exception ex)
            {
                _logger.LogError($"an error occurred when trying to add the secret.\n{ex.Message}");
                throw;
            }
            finally
            {
                _logger.MethodExit();
            }
            return resp?.ARN;
        }

        /// <summary>
        /// Generates a request to write a binary PFX certificate store and writes two secrets to AWS Secrets manager:
        /// one containing the cert store secret; stored as a secretBinary, and one containing the PFX password.
        /// A tag named "PasswordSecret" will be added to the cert store secret and will contain the name of the password secret.
        /// </summary>
        /// <param name="jobParameters"></param>
        /// <returns>Two CreateSecretRequests; one for the cert store, and one for the password.</returns>
        private (CreateSecretRequest, CreateSecretRequest) GenerateAddSecretPfxRequest(AwsSecretsManagerJobParameters jobParameters)
        {
            _logger.MethodEntry();
            var pwdSecretName = jobParameters.SecretName + "-pw";

            var createStoreReq = new CreateSecretRequest { Name = jobParameters.SecretName };

            var createPwdReq = new CreateSecretRequest
            {
                Name = pwdSecretName,
                SecretString = jobParameters.CertProperties.PrivateKeyPassword,
                Tags = new List<Tag> { new Tag { Key = "PasswordFor", Value = jobParameters.SecretName } }
            };

            // we create the cert store binarySecret value by converting the base64 encoded PFX
            var storeBytes = Convert.FromBase64String(jobParameters.CertProperties.Contents);
            var stream = new MemoryStream(storeBytes);
            createStoreReq.SecretBinary = stream;
            createStoreReq.Tags = new List<Tag> { new Tag { Key = TagNames.CERT_SECRET_PASSWORD_NAME, Value = createPwdReq.Name } }; // add the tag identifying the password secret

            _logger.MethodExit();
            return (createStoreReq, createPwdReq);
        }

        private (CreateSecretRequest, CreateSecretRequest) GenerateAddSecretJksRequest(AwsSecretsManagerJobParameters jobParameters)
        {
            throw new NotImplementedException();
        }

        private CreateSecretRequest GenerateAddSecretPemRequest(AwsSecretsManagerJobParameters jobParameters)
        {
            _logger.MethodEntry();

            var req = new CreateSecretRequest { Name = jobParameters.SecretName };
            string pemCert;

            // first, format the cert according to the store type (PEM)

            try
            {
                pemCert = CertUtilities.ConvertPfxToPem(jobParameters.CertProperties.Contents, jobParameters.CertProperties.PrivateKeyPassword);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Conversion failed: unable to convert certificate contents to PEM\n{ex.Message}");
                throw;
            }

            req.SecretString = pemCert;

            return req;
        }

        /// <summary>
        /// Update existing secret with a new version
        /// then apply tags and replica regions from entry parameters.
        /// </summary>
        /// <param name="certName"></param>
        /// <param name="certProps"></param>
        /// <returns></returns>
        private async Task<string> UpdateSecret(AwsSecretsManagerJobParameters jobParameters)
        {
            _logger.MethodEntry();

            //var updateCertReq = new UpdateSecretRequest() { SecretId = jobParameters.SecretName };

            UpdateSecretRequest updateCertReq = null;
            UpdateSecretRequest updatePwReq = null;

            switch (jobParameters.StoreType)
            {
                case "AWSSMPEM":
                    updateCertReq = GenerateUpdateSecretPemRequest(jobParameters);
                    break;
                case "AWSSMPFX":
                    (updateCertReq, updatePwReq) = GenerateUpdateSecretPfxRequest(jobParameters);
                    break;
                case "AWSSMJKS":
                    (updateCertReq, updatePwReq) = GenerateUpdateSecretJksRequest(jobParameters);
                    break;
                default:
                    throw new ArgumentException($"Invalid store type {jobParameters.StoreType}");
            }

            // include any explicit encryption key ID
            if (!string.IsNullOrEmpty(jobParameters.CertProperties.KmsKeyId)) updateCertReq.KmsKeyId = jobParameters.CertProperties.KmsKeyId;

            UpdateSecretResponse updateCertResp;

            // send request to update the secret value
            try
            {
                _logger.LogTrace($"sending request to AWS..");
                updateCertResp = await _secretsManagerClient.UpdateSecretAsync(updateCertReq);
                _logger.LogTrace($"successfully created secret containing the certificate\nARN: {updateCertResp.ARN}\nversion ID: {updateCertResp.VersionId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"an error occurred when trying to add the certificate.\n{ex.Message}");
                throw;
            }

            // gather any provided tags
            // for Updates, these need to be applied in a subsequent request
            // if we are using a tag for identification per cert store definition, we will add it.

            _logger.LogTrace("handling tags if necessary..");

            var tags = new List<Tag>();

            if (jobParameters.StoreProperties.UseTags)
            {
                var idTag = new Tag { Key = jobParameters.StoreProperties.TagName, Value = jobParameters.StoreProperties.TagValue };
                tags.Add(idTag);
                _logger.LogTrace($"included tag for identification.  Key = \"{idTag.Key}\", Value = \"{idTag.Value}\"");
            }

            var entryTags = jobParameters.CertProperties.Tags?.Select(t => new Tag { Key = t.Key, Value = t.Value })?.ToList();

            tags.AddRange(entryTags);

            // for JKS or PFX stores..

            if (updatePwReq != null) {
                _logger.LogTrace($"setting a tag with the password secret name..");
                tags.Add(new Tag { Key = TagNames.CERT_SECRET_PASSWORD_NAME, Value = updatePwReq.SecretId });
                
                var pwdTags = new List<Tag> { new Tag { Key = TagNames.PASSWORD_SECRET_CERT_NAME, Value = updateCertReq.SecretId } };                
                await UpdateSecretTagsAsync(updatePwReq.SecretId, pwdTags);
            }            

            if (tags.Any())
            {
                _logger.LogTrace($"tags are included, replacing existing tags with the {tags.Count} provided.");
                await UpdateSecretTagsAsync(jobParameters.SecretName, tags);
            }

            // include any additional replica regions
            // these also need to be replaced in a seperate step for updates
            try
            {
                if (jobParameters.CertProperties.ReplicaRegions.Any())
                {
                    _logger.LogTrace("replica regions were provided, replacing any existing replica regions..");
                    await ReplaceSecretReplicaRegionsAsync(jobParameters.SecretName, jobParameters.CertProperties.ReplicaRegions);
                }
            }
            catch (Exception ex)
            {
                _logger.LogTrace($"there was an error when attempting to replace the secret replica regions. {ex.Message}");
                throw;
            }
            finally
            {
                _logger.MethodExit();
            }
            return updateCertResp?.ARN;
        }

        private (UpdateSecretRequest, UpdateSecretRequest) GenerateUpdateSecretJksRequest(AwsSecretsManagerJobParameters jobParameters)
        {
            throw new NotImplementedException();
        }

        private (UpdateSecretRequest, UpdateSecretRequest) GenerateUpdateSecretPfxRequest(AwsSecretsManagerJobParameters jobParameters)
        {
           _logger.MethodEntry();
            var pwdSecretName = jobParameters.SecretName + "-pw";

            var updateStoreReq = new UpdateSecretRequest { SecretId = jobParameters.SecretName };

            var updatePwdRequest = new UpdateSecretRequest
            {
                SecretId = pwdSecretName,
                SecretString = jobParameters.CertProperties.PrivateKeyPassword                
            };

            // we create the cert store binarySecret value by converting the base64 encoded PFX
            var storeBytes = Convert.FromBase64String(jobParameters.CertProperties.Contents);
            var stream = new MemoryStream(storeBytes);
            updateStoreReq.SecretBinary = stream;
            
            _logger.MethodExit();
            return (updateStoreReq, updatePwdRequest);
        }

        private UpdateSecretRequest GenerateUpdateSecretPemRequest(AwsSecretsManagerJobParameters jobParameters)
        {
            _logger.MethodEntry();
            var req = new UpdateSecretRequest { SecretId = jobParameters.SecretName };
            string pemCert;
            try
            {
                pemCert = CertUtilities.ConvertPfxToPem(jobParameters.CertProperties.Contents, jobParameters.CertProperties.PrivateKeyPassword);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Conversion failed: unable to convert certificate contents to PEM\n{ex.Message}");
                throw;
            }

            req.SecretString = pemCert;

            return req;
        }

        /// <summary>
        /// This method will remove a certificate secret from AWS Secrets Manager
        /// If there is a corresponding PFX or JKS password, it should also be removed.
        /// </summary>
        /// <param name="secretName"></param>
        /// <returns></returns>
        public async Task<(string,string)> RemoveSecret(string secretName)
        {
            _logger.MethodEntry();

            var req = new DeleteSecretRequest { SecretId = secretName };
            DeleteSecretResponse resp;
            var certSecretArn = string.Empty;
            var pwdSecretArn = string.Empty;

            try
            {
                _logger.LogTrace($"secret id to remove: '{req.SecretId}'");

                _logger.LogTrace($"first checking tags for a password entry..");

                // First, get the current tags on the secret
                var describeRequest = new DescribeSecretRequest
                {
                    SecretId = secretName
                };

                var describeResponse = await _secretsManagerClient.DescribeSecretAsync(describeRequest);

                var currentTags = describeResponse.Tags ?? new List<Tag>();

                var passwordTag = currentTags.FirstOrDefault(ct => ct.Key.ToUpper() == TagNames.CERT_SECRET_PASSWORD_NAME.ToUpper());
                var passwordSecretName = string.Empty;

                if (passwordTag != null) {
                    passwordSecretName = passwordTag.Value;
                    _logger.LogTrace($"got the password secret name {passwordSecretName} from the tag {TagNames.CERT_SECRET_PASSWORD_NAME}");
                }
                
                _logger.LogTrace($"submitting request to delete secret..");

                resp = await _secretsManagerClient.DeleteSecretAsync(req);
                certSecretArn = resp.ARN;
                _logger.LogTrace($"successfully removed secret with ARN {certSecretArn}");

                if (!string.IsNullOrEmpty(passwordSecretName)) {
                    var delPwdReq = new DeleteSecretRequest { SecretId = passwordSecretName };
                    _logger.LogTrace($"now removing the secret named {passwordSecretName}, the password for the cert.");
                    var removePwdResp = await _secretsManagerClient.DeleteSecretAsync(delPwdReq);
                    
                    pwdSecretArn = removePwdResp.ARN;

                    _logger.LogTrace($"successfully removed the secret with ARN: {pwdSecretArn}");
                }
                return (certSecretArn, pwdSecretArn);

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
        /// Adds the tags to a secret.  If the tag key already exists, it is replaced.
        /// </summary>
        /// <param name="secretName">The name or ARN of the secret</param>
        /// <param name="newTags">Dictionary of tag names and values to replace existing tags</param>
        /// <returns>Task representing the async operation</returns>
        private async Task UpdateSecretTagsAsync(string secretName, List<Tag> newTags)
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

                // Remove any existing tags with the same name
                var newTagKeys = newTags.Select(t => t.Key).ToList();

                var toReplace = currentTags.Where(t => newTagKeys.Any(key => key == t.Key)).Select(t => t.Key).ToList();
                
                if (toReplace.Any())
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

        /// <summary>
        /// retreive any tags associated with the secret in AWS Secrets Manager
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public async Task<List<Tag>> GetSecretTags(string name)
        {
            _logger.MethodEntry();

            var req = new DescribeSecretRequest { SecretId = name };

            try
            {
                var res = await _secretsManagerClient.DescribeSecretAsync(req);
                return res.Tags;
            }
            catch (Exception ex)
            {
                _logger.LogError($"there was an error retreiving the details for secret \"{name}\"");
                _logger.LogError($"exception: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// This method will search for a secret that stores the password for either a PFX or JKS file.
        /// It will attmempt to find it using conventions in the following order:
        /// 1) if there is a tag on the cert secret called "PasswordSecret"; the Value of the tag will be the name of the secret containing the password.
        /// 2) If there is not, we will search for a secret with the same name and "-pw" suffix.
        /// if neither are successful, we log and return null.
        /// </summary>
        /// <param name="secret"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        internal async Task<string> GetPassword(SecretValueEntry secret)
        {
            _logger.MethodEntry();

            try
            {
                // first, retrieve the tags..
                var tags = await GetSecretTags(secret.Name);

                // now check for the tag that should contain the name of the secret containing the password..
                if (tags != null && tags.Any(t => t.Key.ToLower() == TagNames.CERT_SECRET_PASSWORD_NAME.ToLower()))
                {
                    // there was an entry containing the password secret name.. 
                    var passwordSecretName = tags.First(t => t.Key.ToLower() == TagNames.CERT_SECRET_PASSWORD_NAME.ToLower())?.Value;
                    if (!string.IsNullOrEmpty(passwordSecretName))
                    {
                        // retreive the plaintext secret value, which should be the store password
                        var req = new GetSecretValueRequest() { SecretId = passwordSecretName };
                        var pwdSecret = await _secretsManagerClient.GetSecretValueAsync(req);
                        return pwdSecret.SecretString;
                    }
                }
                else
                {
                    // couldn't find a tag, we'll check for a secret with the same name + "-pw" suffix..
                    _logger.LogTrace($"no tag named {TagNames.CERT_SECRET_PASSWORD_NAME} found; checking for secret with same name and -pw suffix..");
                    var passwordSecretName = secret.Name + "-pw";
                    var exists = await SecretExists(passwordSecretName);

                    if (exists)
                    {
                        // we found a secret with the same name and "-pw" suffix, we'll use that
                        var pwdSecret = await _secretsManagerClient.GetSecretValueAsync(new GetSecretValueRequest { SecretId = passwordSecretName });
                        return pwdSecret.SecretString;
                    }
                }
                // if we got here, there is no usable password
                _logger.LogError($"Unable to find a secret containing the cert store password for {secret.Name} so this store cannot be managed via Keyfactor Command.");
                return null;
            }
            catch (Exception ex)
            {

                _logger.LogError($"There was an error when attempting to retreive the password for {secret.Name}");
                _logger.LogError($"{ex.Message}");
                throw;
            }
        }
    }
}
