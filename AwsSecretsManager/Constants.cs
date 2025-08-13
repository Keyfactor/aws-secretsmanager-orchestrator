
//  Copyright 2025 Keyfactor
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
}

