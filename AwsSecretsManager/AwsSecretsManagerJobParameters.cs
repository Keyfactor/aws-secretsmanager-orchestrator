using Amazon.SecretsManager.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager
{
    public class AwsSecretsManagerJobParameters : AwsAuthParams
    {
        public string StorePath { get; set; }
        public string ClientMachine { get; set; }
        
        public string JobType { get; set; }

        public CertProperties CertProperties { get; set; }

    }

    public class CertProperties
    {
        public List<ReplicaRegionType> ReplicaRegions { get; set; }
        public string SecretId { get; set; }
    }
}
