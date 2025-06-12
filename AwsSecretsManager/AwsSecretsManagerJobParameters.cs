using Amazon.SecretsManager.Model;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager
{
    public class AwsSecretsManagerJobParameters
    {
        public string JobType { get; set; }
        public Guid JobId { get; set; }


        public CertStoreProperties StoreProperties { get; set; }
        public CertProperties CertProperties { get; set; }
    }

    public class CertStoreProperties
    {
        [JsonProperty("StorePath")]
        public string StorePath { get; set; } // root path that the managed certs should have.  set to "/" for all.

        [JsonProperty("ClientMachine")]
        public string AwsRegion { get; set; } // the client machine value should be a valid AWS region system name       

        [JsonProperty("ServerUsername")]
        public string AuthAccessKeyId { get; set; } // PAM resolvable access key ID for IAM authentication

        [JsonProperty("ServerPassword")]
        public string AuthSecret { get; set; } // PAM resolvable secret value for IAM authentication


    }
    //        "JobCertificate": {
            //                    "Thumbprint": null,
            //                    "Contents": "",
            //                    "Alias": "testcert",
            //                    "PrivateKeyPassword": "..."


    public class CertProperties
    {
        public List<ReplicaRegionType> ReplicaRegions { get; set; } // additional regions to write the secret to
        public string KmsKeyId { get; set; } // encryption key; if empty, will use the default
        public Dictionary<string, string> Tags { get; set; } // tags to associate with the entry
        public bool Overwrite { get; set; } // whether this should overwrite an existing secret with the same name

        public string Thumbprint { get; set; } 

        public string Contents { get; set; } // the Base64 encoded PFX file

        public string Alias { get; set; }  // the alias for the certificate

        public string PrivateKeyPassword { get; set; } // used to extract cert
    }
}
