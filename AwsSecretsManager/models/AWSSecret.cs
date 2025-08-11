using Amazon.SecretsManager.Model;
using System.Collections.Generic;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.models
{
    public class AWSSecret
    {
        public string Name { get; set; }
        public string ARN { get; set; }
        public List<Tag> Tags { get; set; }
        public string SecretString { get; set; }
        public byte[] SecretBinary { get; set; }
        public string VersionId { get; set; }
    }
}
