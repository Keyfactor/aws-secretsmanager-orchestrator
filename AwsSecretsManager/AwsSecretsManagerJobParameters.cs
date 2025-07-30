
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using Amazon.SecretsManager.Model;
using System;
using System.Collections.Generic;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager
{
    public class AwsSecretsManagerJobParameters
    {
        public string JobType { get; set; }
        public string StoreType { get; set; }
        public Guid JobId { get; set; }
        public long JobHistoryId { get; set; }
        public CertStoreProperties StoreProperties { get; set; }
        public CertProperties CertProperties { get; set; }
        public string SecretName
        {
            get
            {
                if (string.IsNullOrEmpty(StoreProperties.NamePrefix)) return CertProperties.Alias;
                return $"{StoreProperties.NamePrefix}/{CertProperties.Alias}";
            }
        }

        public AwsSecretsManagerJobParameters() { 
            StoreProperties = new CertStoreProperties();
            CertProperties = new CertProperties();
        }
    }

    public class CertStoreProperties
    {
        public string NamePrefix { get; set; } // root path that the managed certs should have.  set to "/" for all.
        public string AwsRole { get; set; } // the client machine value should be a fully qualified ARN that defines the role to assume
        public string AwsRegion { get; set; } // the client machine value should be the primary AWS region for the authenticating identity
        public string TagName { get; set; } // The name of the tag to use to identify certs that should be managed by this cert store
        public string TagValue { get; set; } // The value of the tag to use to identify certs that should be managed by this cert store; from StorePath
        public bool UseTags { get { return !string.IsNullOrEmpty(TagName) && !string.IsNullOrEmpty(TagValue); } }
        public bool UsePrefix { get { return !string.IsNullOrEmpty(NamePrefix); } }
    }

    public class CertProperties
    {
        public List<ReplicaRegionType> ReplicaRegions { get; set; } // additional regions to write the secret to
        public string KmsKeyId { get; set; } // encryption key; if empty, will use the default
        public Dictionary<string, string> Tags { get; set; } // tags to associate with the entry
        public bool Overwrite { get; set; } // whether this should overwrite an existing secret with the same name
        public string Thumbprint { get; set; }
        public string Contents { get; set; } // the Base64 encoded PFX file
        public string Alias { get; set; }  // the alias for the certificate
        public string Description { get; set; } // the description; can be passed via entry parameter
        public string PrivateKeyPassword { get; set; } // used to extract cert

        public CertProperties() { 
            ReplicaRegions = new List<ReplicaRegionType>();
            Tags = new Dictionary<string, string>();
        }
    }
}
