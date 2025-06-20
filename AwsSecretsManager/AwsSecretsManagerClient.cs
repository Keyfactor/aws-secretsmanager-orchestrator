using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Keyfactor.Extensions.Aws;
using Keyfactor.Extensions.Aws.Models;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager
{
    public class AwsSecretsManagerClient
    {
        private Microsoft.Extensions.Logging.ILogger logger;
        private IAmazonSecretsManager _secretManagerClient { get; set; }

        public AwsSecretsManagerClient()
        {
            logger = LogHandler.GetClassLogger(GetType());
        }

        public void InitializeClient(AuthenticationParameters authParams, AwsAuthUtility authUtility)
        {
            logger.MethodEntry();

            logger.LogTrace($"getting the region endpoint from the string '{authParams.Region}'");

            var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(authParams.Region); // this will return it's best guess no matter what.. never null

            logger.LogTrace($"resolved region endpoint {regionEndpoint.SystemName}");

            logger.LogTrace("Resolving AWS Credentials object.");
            AwsExtensionCredential providedCredentials;
            try
            {
                providedCredentials = authUtility.GetCredentials(authParams);
            }
            catch (Exception)
            {
                logger.LogError("An error occurred while trying to get AWS Credentials");
                throw;
            }

            _secretManagerClient = new AmazonSecretsManagerClient(providedCredentials.GetAwsCredentialObject());

            logger.MethodExit();
        }

        /// <summary>
        /// Lists all secrets that contain the provided Tag Key value.  
        /// We retreive these in batches of maximum 20 size and loop until we've gotten them all.
        /// </summary>
        public async Task<List<SecretValueEntry>> ListSecrets(List<Filter> filters)
        {
            logger.MethodEntry();

            var results = new List<SecretValueEntry>();

            string nextToken = null;

            logger.LogTrace($"begin batch retreival of secrets..");
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

                    var response = await _secretManagerClient.BatchGetSecretValueAsync(request);

                    results.AddRange(response.SecretValues);
                    nextToken = response.NextToken;
                    logger.LogTrace($"got {response.SecretValues.Count} entries.");
                    logger.LogTrace("retreiving next batch of up to 20 entries..");
                }
                while (!string.IsNullOrEmpty(nextToken));

                return results;
            }
            catch (Exception ex)
            {
                logger.LogError($"There was an error when attempting to retreive the list of secrets: {LogHandler.FlattenException(ex)}");
                throw;
            }
            finally
            {
                logger.MethodExit();
            }
        }

        public async Task<string> AddSecret(CertStoreProperties storeProps, CertProperties certProps)
        {
            logger.MethodEntry();

            var req = new CreateSecretRequest();

            // determine name, including any configured prefix

            var prefix = storeProps.NamePrefix;

            req.Name = string.IsNullOrEmpty(storeProps.NamePrefix) ? storeProps.NamePrefix + "/" + certProps.Alias : certProps.Alias;

            logger.LogTrace($"the alias is {certProps.Alias}, resolved the secret name to be {req.Name}");

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

            CreateSecretResponse resp;

            try
            {
                logger.LogTrace($"sending request to AWS..");
                resp = await _secretManagerClient.CreateSecretAsync(req);
                logger.LogTrace($"successfully submitted create secret request.\nARN: {resp.ARN}\nversion ID: {resp.VersionId}");
            }
            catch (Exception ex)
            {
                logger.LogError($"an error occurred when trying to add the certificate.\n{ex.Message}");
                throw;
            }
            finally
            {
                logger.MethodExit();
            }

            return resp?.ARN;
        }

        public async Task RemoveSecret()
        {
            throw new NotImplementedException();
        }
    }
}
