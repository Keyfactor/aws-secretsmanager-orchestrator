
//  Copyright 2026 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.models
{
    /// <summary>
    /// Represents the JSON document used when an AWSSMPEM store has the SeparatePrivateKey
    /// option enabled.  The secret value is serialized as:
    ///   { "certificate": "&lt;PEM cert + chain, leaf first&gt;", "private_key": "&lt;PEM private key&gt;" }
    ///
    /// This is the single source of truth for the property names on both the write
    /// (Management Add/Update) and read (Inventory) paths.
    /// </summary>
    public class PemSecret
    {
        [JsonProperty("certificate")]
        public string Certificate { get; set; }

        [JsonProperty("private_key")]
        public string PrivateKey { get; set; }

        /// <summary>
        /// Reconstructs a single PEM block (certificate + chain followed by the private key)
        /// suitable for feeding the existing PEM parsing routines on the inventory path.
        /// </summary>
        public string ToCombinedPem()
        {
            if (string.IsNullOrEmpty(PrivateKey)) return Certificate ?? string.Empty;
            if (string.IsNullOrEmpty(Certificate)) return PrivateKey;
            return Certificate.TrimEnd() + "\n" + PrivateKey.TrimStart();
        }
    }
}
