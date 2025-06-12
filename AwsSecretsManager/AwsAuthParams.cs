using Amazon;
using Newtonsoft.Json;
using System.ComponentModel;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager
{
    public abstract class AwsAuthParams
    {
        [JsonProperty("UseEC2AssumeRole")]
        [DefaultValue(false)]
        public bool UseEC2AssumeRole { get; set; }

        [JsonProperty("UseOAuth")]
        [DefaultValue(false)]
        public bool UseOAuth { get; set; }

        [JsonProperty("UseIAM")]
        [DefaultValue(false)]
        public bool UseIAM { get; set; }

        [JsonProperty("EC2AssumeRole")]
        public string EC2AssumeRole { get; set; }

        [JsonProperty("OAuthAssumeRole")]
        public string OAuthAssumeRole { get; set; }

        [JsonProperty("OAuthScope")]
        public string OAuthScope { get; set; }

        [JsonProperty("OAuthGrantType")]
        public string OAuthGrantType { get; set; }

        [JsonProperty("OAuthUrl")]
        public string OAuthUrl { get; set; }

        [JsonProperty("IAMAssumeRole")]
        public string IAMAssumeRole { get; set; }

        [JsonProperty("ExternalId")]
        public string ExternalId { get; set; }

        // retreived from ServerUsername in JobConfiguration
        public string AccessKey { get; set; } // PAM resolved secret

        // retreived from ServerPassword in JobConfiguration
        public string AccessSecret { get; set; }  // PAM resolved secret

        // retreived from ClientMachine in JobConfiguration
        public string AccountId { get; set; }

        [JsonProperty("OauthPath")]
        [DefaultValue("/oauth2/default/v1/token")]
        public string OAuthPath { get; set; }

        [JsonProperty("AwsRegions")]
        [DefaultValue(false)]
        public string AwsRegions { get; set; }

        [JsonProperty("AwsRole")]
        [DefaultValue(false)]
        public string AwsRole { get; set; }

        public string RoleSessionName { get; set; } = "oauth-session";
        public RegionEndpoint Region { get; set; } = RegionEndpoint.USEast1;

    }
}
