using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Keyfactor.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager
{
    public class AwsSecretsManagerClient
    {
        private ILogger logger;
        private IAmazonSecretsManager _secretManagerClient { get; set; }

        public AwsSecretsManagerClient() {
            logger = LogHandler.GetClassLogger(GetType());
        }

        public void InitializeClient() { 
            _secretManagerClient = new AmazonSecretsManagerClient();
            // additional configuration goes here
            _secretManagerClient
        }

        /// <summary>
        /// Lists all secrets that contain the provided Tag Key value.  
        /// We retreive these in batches of maximum 20 size and loop until we've gotten them all.
        /// </summary>
        public async Task<List<SecretValueEntry>> ListSecrets(string tagKeyFilter) {

            var results = new List<SecretValueEntry>();
                        
            // define filters (tags)
            var f = new Filter
            {
                Key = "tag-key",
                Values = new List<string> { tagKeyFilter }
            };

            string nextToken = null;

            do {
                var request = new BatchGetSecretValueRequest
                {
                    Filters = new List<Filter> { f },
                    MaxResults = 20,
                };
                if (nextToken != null) {
                    request.NextToken = nextToken;
                }

                var response = await _secretManagerClient.BatchGetSecretValueAsync(request);

                results.AddRange(response.SecretValues);
                nextToken = response.NextToken;
            }
            while (!string.IsNullOrEmpty(nextToken));

            return results;
        }
        
        public async Task GetSecret() { }

        public async Task AddSecret() { }

        public async Task RemoveSecret() { }


    }
}
