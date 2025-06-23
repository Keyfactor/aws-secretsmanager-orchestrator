
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
        public const string STORE_TYPE_NAME = "AWSSM";
        public const string KEYFACTOR_CERT_TAG_KEY = "KeyfactorCertificateStore"; // this key will need to be present on any certs managed by this integration
    }
    public static class KeyfactorJobType
    {
        public const string CREATE = "Create";
        public const string DISCOVERY = "Discovery";
        public const string INVENTORY = "Inventory";
        public const string MANAGEMENT = "Management";
        public const string REENROLLMENT = "Enrollment";
    }

    public static class AWSFilterParameter { 
        public const string NAME = "name";
        public const string TAG_KEY = "tag-key";
        public const string TAG_VALUE = "tag-value";
    }
}
