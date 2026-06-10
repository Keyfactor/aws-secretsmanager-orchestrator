
//  Copyright 2026 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager
{
    static class Constants
    {
        public const string STORE_TYPE_PEM = "AWSSMPEM";
        public const string STORE_TYPE_PFX = "AWSSMPFX";
        public const string STORE_TYPE_JKS = "AWSSMJKS";
    }

    public static class StorePropertyNames
    {
        // Boolean store-type custom field (AWSSMPEM) that switches the secret value to a
        // JSON document with separate "certificate" and "private_key" PEM properties.
        public const string SEPARATE_PRIVATE_KEY = "SeparatePrivateKey";
    }
    public static class KeyfactorJobType
    {
        public const string INVENTORY = "Inventory";
        public const string MANAGEMENT = "Management";
    }

    public static class AWSFilterParameter
    {
        public const string NAME = "name";
        public const string TAG_KEY = "tag-key";
        public const string TAG_VALUE = "tag-value";
    }

    public static class EntryParameterKeys
    {
        public const string TAGS = "CertificateTags";
        public const string REPLICAREGIONS = "ReplicaRegions";
    }

    public static class TagNames
    {
        public const string CERT_SECRET_PASSWORD_NAME = "PasswordSecret";
        public const string PASSWORD_SECRET_CERT_NAME = "PasswordFor";
    }

    /// <summary>
    /// Placeholder tokens that may be used inside CertificateTags values; each is replaced
    /// with the corresponding value evaluated from the certificate before the tag is written.
    /// </summary>
    public static class CertificateTagTokens
    {
        public const string SERIAL_NUMBER = "%SERIAL_NUMBER%";
        public const string NOT_BEFORE = "%NOT_BEFORE%";
        public const string NOT_AFTER = "%NOT_AFTER%";

        public static bool ContainsAnyToken(System.Collections.Generic.IEnumerable<string> values)
        {
            if (values == null) return false;
            foreach (var v in values)
            {
                if (string.IsNullOrEmpty(v)) continue;
                if (v.Contains(SERIAL_NUMBER) || v.Contains(NOT_BEFORE) || v.Contains(NOT_AFTER))
                    return true;
            }
            return false;
        }
    }
}

