using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager
{
    public class AwsSecretsManagerClient
    {
        private ILogger logger;
        private IAmazonSecretsManager _secretManagerClient { get; set; }

        public AwsSecretsManagerClient()
        {
            logger = LogHandler.GetClassLogger(GetType());
        }

        public void InitializeClient(string accessKey, string secret, string region)
        {
            logger.MethodEntry();

            logger.LogTrace($"getting the region endpoint from the string '{region}'");

            var regionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region); // this will return it's best guess no matter what.. never null

            logger.LogTrace($"resolved region endpoint {regionEndpoint.SystemName}");

            _secretManagerClient = new AmazonSecretsManagerClient(accessKey, secret, regionEndpoint);

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

        public async Task AddSecret(string name, Dictionary<string, string> tags, string contents)
        {
            //var req = new CreateSecretRequest();
            //req.Tags = new List<Tag>() { new Tag() { k} }
            //_secretManagerClient.CreateSecretAsync(new CreateSecretRequest())
        }

        public async Task RemoveSecret() { }


    }
}
