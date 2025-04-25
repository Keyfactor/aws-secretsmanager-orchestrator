using Amazon;
using Amazon.Runtime;
using Amazon.SecretsManager;
using Amazon.SecurityToken.Model;
using Amazon.SecurityToken;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class AwsClientFactory
{
    public class AwsAuthOptions
    {
        public string OAuthTokenUrl { get; set; }
        public string GrantType { get; set; } = "client_credentials";
        public string Scope { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string RoleName { get; set; }
        public string AccountId { get; set; }
        public string RoleSessionName { get; set; } = "oauth-session";
        public RegionEndpoint Region { get; set; } = RegionEndpoint.USEast1;

        //public AwsAuthOptions()
    }

    // Cache to hold the current token and its expiration time
    private static (string token, DateTime expiration) _cachedToken = (null, DateTime.MinValue);

    public static async Task<AmazonSecretsManagerClient> CreateSecretsManagerClientAsync(AwsAuthOptions options)
    {
        if (options == null)
            return new AmazonSecretsManagerClient();

        // Get OAuth token (with refresh logic)
        string token = await GetOAuthTokenAsync(options);

        // Construct RoleArn
        if (string.IsNullOrEmpty(options.RoleName) || string.IsNullOrEmpty(options.AccountId))
            throw new ArgumentException("RoleName and AccountId must be provided for OAuth-based authentication.");

        string roleArn = $"arn:aws:iam::{options.AccountId}:role/{options.RoleName}";

        // Create an AmazonSecurityTokenServiceClient for AssumeRole operation
        using var stsClient = new AmazonSecurityTokenServiceClient(options.Region);

        var assumeRoleRequest = new AssumeRoleWithWebIdentityRequest
        {
            RoleArn = roleArn,
            RoleSessionName = options.RoleSessionName,
            WebIdentityToken = token
        };

        var assumeRoleResponse = await stsClient.AssumeRoleWithWebIdentityAsync(assumeRoleRequest);

        var credentials = assumeRoleResponse.Credentials;
        var awsCredentials = new SessionAWSCredentials(credentials.AccessKeyId, credentials.SecretAccessKey, credentials.SessionToken);

        return new AmazonSecretsManagerClient(awsCredentials, options.Region);
    }

    private static async Task<string> GetOAuthTokenAsync(AwsAuthOptions options)
    {
        // If the cached token is still valid, return it
        if (_cachedToken.token != null && _cachedToken.expiration > DateTime.UtcNow)
            return _cachedToken.token;

        // Otherwise, fetch a new token
        return await RequestOAuthTokenAndCacheAsync(options);
    }

    private static async Task<string> RequestOAuthTokenAndCacheAsync(AwsAuthOptions options)
    {
        string token = await RequestOAuthToken(options);

        // Simulate token expiration (you can modify this based on the actual OAuth response)
        // For example, if the token expires in 3600 seconds (1 hour), set expiration time accordingly.
        var expirationTime = DateTime.UtcNow.AddSeconds(3600); // Assume 1 hour validity

        // Cache the token and expiration time
        _cachedToken = (token, expirationTime);

        return token;
    }

    private static async Task<string> RequestOAuthToken(AwsAuthOptions options)
    {
        using var httpClient = new HttpClient();

        var requestData = new StringBuilder();
        requestData.Append($"grant_type={options.GrantType}");
        if (!string.IsNullOrEmpty(options.Scope))
            requestData.Append($"&scope={Uri.EscapeDataString(options.Scope)}");

        var request = new HttpRequestMessage(HttpMethod.Post, options.OAuthTokenUrl)
        {
            Content = new StringContent(requestData.ToString(), Encoding.UTF8, "application/x-www-form-urlencoded")
        };

        var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(responseContent);

        if (!jsonDoc.RootElement.TryGetProperty("access_token", out var tokenElement))
            throw new Exception("OAuth token response did not contain an access_token");

        return tokenElement.GetString();
    }
}
