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
}
