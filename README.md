<h1 align="center" style="border-bottom: none">
    Integration Template Universal Orchestrator Extension
</h1>

<p align="center">
  <!-- Badges -->
<img src="https://img.shields.io/badge/integration_status-prototype-3D1973?style=flat-square" alt="Integration Status: prototype" />
<a href="https://github.com/Keyfactor/aws-secretsmanager-orchestrator/releases"><img src="https://img.shields.io/github/v/release/Keyfactor/aws-secretsmanager-orchestrator?style=flat-square" alt="Release" /></a>
<img src="https://img.shields.io/github/issues/Keyfactor/aws-secretsmanager-orchestrator?style=flat-square" alt="Issues" />
<img src="https://img.shields.io/github/downloads/Keyfactor/aws-secretsmanager-orchestrator/total?style=flat-square&label=downloads&color=28B905" alt="GitHub Downloads (all assets, all releases)" />
</p>

<p align="center">
  <!-- TOC -->
  <a href="#support">
    <b>Support</b>
  </a>
  ·
  <a href="#installation">
    <b>Installation</b>
  </a>
  ·
  <a href="#license">
    <b>License</b>
  </a>
  ·
  <a href="https://github.com/orgs/Keyfactor/repositories?q=orchestrator">
    <b>Related Integrations</b>
  </a>
</p>

## Overview

This AWS Secrets Manager Orchestrator extension adds the ability to manage certificates stored in AWS Secrets Manager as secrets via Keyfactor Command.
The [AWS Secrets Manager](https://aws.amazon.com/secrets-manager/) service provides a secure way to manage secrets of any type, across your enterprise infrastructure.
This integration will allow you to generate and store certificates, remove certificates and maintain an inventory of certificates stored in AWS Secrets Manager; providing a 
comprehensive way to manage certificates used within your enterprise.

It can read and write secrets containing certificates stored in the following formats:
- PEM
- PFX
- JKS

For each format there is a corresponding certificate store type ([AWSSMPEM](./awssmpem.md), [AWSSMPFX](./awssmpfx.md), [AWSSMJKS](./awssmpfx.md)).



## Compatibility

This integration is compatible with Keyfactor Universal Orchestrator version 10.1 and later.

## Support
The Integration Template Universal Orchestrator extension is community open source and there is **no SLA**. Keyfactor will address issues as resources become available.

> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute bug fixes or additional enhancements, use the **[Pull requests](../../pulls)** tab.

## Requirements & Prerequisites

Before installing the Integration Template Universal Orchestrator extension, we recommend that you install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.


In order to use this integration, you should have..
- An instance of Keyfactor Command v11.0+
- An instance of the Keyfactor Universal Orchestrator v10.4+
- AWS credentials with sufficient authority to manage secrets
 
### Authentication
This integration utilizes the Keyfactor AWS Authentication SDK, and includes the standard fields that enable authentication into AWS via a number of methods.
For more details on how to configure authentication, refer to the [aws-auth-library](https://github.com/Keyfactor/aws-auth-library) repository.

### Authorization / permissions
There are several strategies available to manage permissions to AWS Secrets Manager including authorizing only a subset of secrets.  
And whichever approach makes the most sense to your organization should be utilized.  The caveat to remember is that this integration can only read and write certificate secrets that the provided identity represented by 
the authentication credentials has access to.

Here is a list of the IAM actions that the authenticating identity should have in order to perform all Jobs supported by this extension:

- `secretsmanager.ListSecrets`
- `secretsManager.GetSecretValue` 
- `secretsmanager:CreateSecret`
- `secretsmanager.DeleteSecret` 
- `secretsmanager.UpdateSecret`
- `secretsmanager.TagResource`  _if using tags for filtering or to add tags via entry parameters._

For more information on these permission actions, refer to the [AWS Documentation](https://docs.aws.amazon.com/service-authorization/latest/reference/list_awssecretsmanager.html).

For more information on how best to secure your AWS Secrets Manager secrets, refer to [this document](https://docs.aws.amazon.com/secretsmanager/latest/userguide/auth-and-access.html).

### Secret naming and filtering

While secrets stored in AWS Secrets Manager are natively stored as a single, flat list; if you utilize a [heirarchical naming convention](https://docs.aws.amazon.com/prescriptive-guidance/latest/secure-sensitive-data-secrets-manager-terraform/naming-convention.html) for your certificate secrets,
In this integration, we've included the ability to restrict the secrets to be considered as potential certificates to be managed by the Certificate Store defined in Keyfactor Command by 
providing a name prefix that all secrets to be considered should have.
This allows for managing certificates within a specific path of that heirarchy.  

For example, if we have three secrets in AWS Secrets Manager

- "org-name/dev-env/cert-secret"
- "org-name/dev-env/testing/cert-secret"
- "org-name/ssl/sll-cert"

The certificate store in Keyfactor Command can be configured to include all of them by including <b>[prefix="org-name/"]</b> in the store path.

To include only include the first two: <b>[prefix="org-name/dev-env/"]</b> in the store path.

Additionally, the certificate store in Command can be configured to filter the secrets to be managed by the presence of a specific Tag Name and optional Tag value.

Additional details of how to configure these in Keyfactor Command can be found in the documentation for that store type ([AWSSMPEM](./awssmpem.md), [AWSSMPFX](./awssmpfx.md), [AWSSMJKS](./awssmpfx.md)).


## AWSSM Certificate Store Type

To use the Integration Template Universal Orchestrator extension, you **must** create the AWSSM Certificate Store Type. This only needs to happen _once_ per Keyfactor Command instance.



TODO Overview is a required section
TODO Global Store Type Section is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info





#### AwsSecretsManager PEM Requirements

TODO Requirements is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info



#### Supported Operations

| Operation    | Is Supported                                                                                                           |
|--------------|------------------------------------------------------------------------------------------------------------------------|
| Add          | ✅ Checked        |
| Remove       | ✅ Checked     |
| Discovery    | 🔲 Unchecked  |
| Reenrollment | 🔲 Unchecked |
| Create       | 🔲 Unchecked     |

#### Store Type Creation

##### Using kfutil:
`kfutil` is a custom CLI for the Keyfactor Command API and can be used to create certificate store types.
For more information on [kfutil](https://github.com/Keyfactor/kfutil) check out the [docs](https://github.com/Keyfactor/kfutil?tab=readme-ov-file#quickstart)
   <details><summary>Click to expand AWSSM kfutil details</summary>

   ##### Using online definition from GitHub:
   This will reach out to GitHub and pull the latest store-type definition
   ```shell
   # AwsSecretsManager PEM
   kfutil store-types create AWSSM
   ```

   ##### Offline creation using integration-manifest file:
   If required, it is possible to create store types from the [integration-manifest.json](./integration-manifest.json) included in this repo.
   You would first download the [integration-manifest.json](./integration-manifest.json) and then run the following command
   in your offline environment.
   ```shell
   kfutil store-types create --from-file integration-manifest.json
   ```
   </details>


#### Manual Creation
Below are instructions on how to create the AWSSM store type manually in
the Keyfactor Command Portal
   <details><summary>Click to expand manual AWSSM details</summary>

   Create a store type called `AWSSM` with the attributes in the tables below:

   ##### Basic Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Name | AwsSecretsManager PEM | Display name for the store type (may be customized) |
   | Short Name | AWSSM | Short display name for the store type |
   | Capability | AWSSM | Store type name orchestrator will register with. Check the box to allow entry of value |
   | Supports Add | ✅ Checked | Check the box. Indicates that the Store Type supports Management Add |
   | Supports Remove | ✅ Checked | Check the box. Indicates that the Store Type supports Management Remove |
   | Supports Discovery | 🔲 Unchecked |  Indicates that the Store Type supports Discovery |
   | Supports Reenrollment | 🔲 Unchecked |  Indicates that the Store Type supports Reenrollment |
   | Supports Create | 🔲 Unchecked |  Indicates that the Store Type supports store creation |
   | Needs Server | 🔲 Unchecked | Determines if a target server name is required when creating store |
   | Blueprint Allowed | 🔲 Unchecked | Determines if store type may be included in an Orchestrator blueprint |
   | Uses PowerShell | 🔲 Unchecked | Determines if underlying implementation is PowerShell |
   | Requires Store Password | 🔲 Unchecked | Enables users to optionally specify a store password when defining a Certificate Store. |
   | Supports Entry Password | 🔲 Unchecked | Determines if an individual entry within a store can have a password. |

   The Basic tab should look like this:

   ![AWSSM Basic Tab](docsource/images/AWSSM-basic-store-type-dialog.png)

   ##### Advanced Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Supports Custom Alias | Required | Determines if an individual entry within a store can have a custom Alias. |
   | Private Key Handling | Optional | This determines if Keyfactor can send the private key associated with a certificate to the store. Required because IIS certificates without private keys would be invalid. |
   | PFX Password Style | Default | 'Default' - PFX password is randomly generated, 'Custom' - PFX password may be specified when the enrollment job is created (Requires the Allow Custom Password application setting to be enabled.) |

   The Advanced tab should look like this:

   ![AWSSM Advanced Tab](docsource/images/AWSSM-advanced-store-type-dialog.png)

   > For Keyfactor **Command versions 24.4 and later**, a Certificate Format dropdown is available with PFX and PEM options. Ensure that **PFX** is selected, as this determines the format of new and renewed certificates sent to the Orchestrator during a Management job. Currently, all Keyfactor-supported Orchestrator extensions support only PFX.

   ##### Custom Fields Tab
   Custom fields operate at the certificate store level and are used to control how the orchestrator connects to the remote target server containing the certificate store to be managed. The following custom fields should be added to the store type:

   | Name | Display Name | Description | Type | Default Value/Options | Required |
   | ---- | ------------ | ---- | --------------------- | -------- | ----------- |
   | UseDefaultSdkAuth | Use Default SDK Auth | A switch to enable the store to use Default SDK credentials | Bool | false | ✅ Checked |
   | DefaultSdkAssumeRole | Assume new Role using Default SDK Auth | A switch to enable the store to assume a new Role when using Default SDK credentials | Bool | false | 🔲 Unchecked |
   | UseOAuth | Use OAuth 2.0 Provider | A switch to enable the store to use an OAuth provider workflow to authenticate with AWS | Bool | false | ✅ Checked |
   | OAuthScope | OAuth Scope | This is the OAuth Scope needed for Okta OAuth, defined in Okta | String |  | 🔲 Unchecked |
   | OAuthGrantType | OAuth Grant Type | In OAuth 2.0, the term 'grant type' refers to the way an application gets an access token. In Okta this is `client_credentials` | String | client_credentials | 🔲 Unchecked |
   | OAuthUrl | OAuth Url | The token endpoint for the OAuth 2.0 provider | String | https://***/oauth2/default/v1/token | 🔲 Unchecked |
   | OAuthClientId | OAuth Client ID | The Client ID for OAuth. | Secret |  | 🔲 Unchecked |
   | OAuthClientSecret | OAuth Client Secret | The Client Secret for OAuth. | Secret |  | 🔲 Unchecked |
   | UseIAM | Use IAM User Auth | A switch to enable the store to use IAM User auth to assume a role when authenticating with AWS | Bool | false | ✅ Checked |
   | IAMUserAccessKey | IAM User Access Key | The AWS Access Key for an IAM User | Secret |  | 🔲 Unchecked |
   | IAMUserAccessSecret | IAM User Access Secret | The AWS Access Secret for an IAM User. | Secret |  | 🔲 Unchecked |
   | ExternalId | sts:ExternalId | An optional parameter sts:ExternalId to pass with Assume Role calls | String |  | 🔲 Unchecked |

   The Custom Fields tab should look like this:

   ![AWSSM Custom Fields Tab](docsource/images/AWSSM-custom-fields-store-type-dialog.png)

   ##### Entry Parameters Tab

   | Name | Display Name | Description | Type | Default Value | Entry has a private key | Adding an entry | Removing an entry | Reenrolling an entry |
   | ---- | ------------ | ---- | ------------- | ----------------------- | ---------------- | ----------------- | ------------------- | ----------- |
   | CertificateTags | Certificate Tags | If desired, tags can be applied to the certificate entries in AWS Secrets Manager.  Provide them as a JSON string of key-value pairs ie: '{'tag-name': 'tag-content', 'other-tag-name': 'other-tag-content'}' | string |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |
   | ReplicaRegions | Replica Regions | To have the certificate replicated to other regions, you can provide them here as a JSON array in the format: [{ 'KmsKeyId': '<optionally specify the encryption key ID', 'Region': '<region name>'}, {...}] | string |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |

   The Entry Parameters tab should look like this:

   ![AWSSM Entry Parameters Tab](docsource/images/AWSSM-entry-parameters-store-type-dialog.png)

   </details>

## Installation

1. **Download the latest Integration Template Universal Orchestrator extension from GitHub.**

    Navigate to the [Integration Template Universal Orchestrator extension GitHub version page](https://github.com/Keyfactor/aws-secretsmanager-orchestrator/releases/latest). Refer to the compatibility matrix below to determine whether the `net6.0` or `net8.0` asset should be downloaded. Then, click the corresponding asset to download the zip archive.

   | Universal Orchestrator Version | Latest .NET version installed on the Universal Orchestrator server | `rollForward` condition in `Orchestrator.runtimeconfig.json` | `aws-secretsmanager-orchestrator` .NET version to download |
   | --------- | ----------- | ----------- | ----------- |
   | Older than `11.0.0` | | | `net6.0` |
   | Between `11.0.0` and `11.5.1` (inclusive) | `net6.0` | | `net6.0` |
   | Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `Disable` | `net6.0` |
   | Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `LatestMajor` | `net8.0` |
   | `11.6` _and_ newer | `net8.0` | | `net8.0` |

    Unzip the archive containing extension assemblies to a known location.

    > **Note** If you don't see an asset with a corresponding .NET version, you should always assume that it was compiled for `net6.0`.

2. **Locate the Universal Orchestrator extensions directory.**

    * **Default on Windows** - `C:\Program Files\Keyfactor\Keyfactor Orchestrator\extensions`
    * **Default on Linux** - `/opt/keyfactor/orchestrator/extensions`

3. **Create a new directory for the Integration Template Universal Orchestrator extension inside the extensions directory.**

    Create a new directory called `aws-secretsmanager-orchestrator`.
    > The directory name does not need to match any names used elsewhere; it just has to be unique within the extensions directory.

4. **Copy the contents of the downloaded and unzipped assemblies from __step 2__ to the `aws-secretsmanager-orchestrator` directory.**

5. **Restart the Universal Orchestrator service.**

    Refer to [Starting/Restarting the Universal Orchestrator service](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/StarttheService.htm).


6. **(optional) PAM Integration**

    The Integration Template Universal Orchestrator extension is compatible with all supported Keyfactor PAM extensions to resolve PAM-eligible secrets. PAM extensions running on Universal Orchestrators enable secure retrieval of secrets from a connected PAM provider.

    To configure a PAM provider, [reference the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam) to select an extension and follow the associated instructions to install it on the Universal Orchestrator (remote).


> The above installation steps can be supplemented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions).



## Defining Certificate Stores


TODO Global Store Type Section is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info

TODO Certificate Store Configuration is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info


### Store Creation

#### Manually with the Command UI

<details><summary>Click to expand details</summary>

1. **Navigate to the _Certificate Stores_ page in Keyfactor Command.**

    Log into Keyfactor Command, toggle the _Locations_ dropdown, and click _Certificate Stores_.

2. **Add a Certificate Store.**

    Click the Add button to add a new Certificate Store. Use the table below to populate the **Attributes** in the **Add** form.

   | Attribute | Description                                             |
   | --------- |---------------------------------------------------------|
   | Category | Select "AwsSecretsManager PEM" or the customized certificate store name from the previous step. |
   | Container | Optional container to associate certificate store with. |
   | Client Machine |  |
   | Store Path | The store path contains the AWS region where the SecretsManager resides.  It can optionally accept values for tags OR path prefix for identifying secrets to be managed by the cert store instance.  example:'us-east-2 [prefix='dev/midwest']' or 'us-east1 [tagName='managedBy' tagValue='keyfactor']'  |
   | Orchestrator | Select an approved orchestrator capable of managing `AWSSM` certificates. Specifically, one with the `AWSSM` capability. |
   | UseDefaultSdkAuth | A switch to enable the store to use Default SDK credentials |
   | DefaultSdkAssumeRole | A switch to enable the store to assume a new Role when using Default SDK credentials |
   | UseOAuth | A switch to enable the store to use an OAuth provider workflow to authenticate with AWS |
   | OAuthScope | This is the OAuth Scope needed for Okta OAuth, defined in Okta |
   | OAuthGrantType | In OAuth 2.0, the term 'grant type' refers to the way an application gets an access token. In Okta this is `client_credentials` |
   | OAuthUrl | The token endpoint for the OAuth 2.0 provider |
   | OAuthClientId | The Client ID for OAuth. |
   | OAuthClientSecret | The Client Secret for OAuth. |
   | UseIAM | A switch to enable the store to use IAM User auth to assume a role when authenticating with AWS |
   | IAMUserAccessKey | The AWS Access Key for an IAM User |
   | IAMUserAccessSecret | The AWS Access Secret for an IAM User. |
   | ExternalId | An optional parameter sts:ExternalId to pass with Assume Role calls |

</details>



#### Using kfutil CLI

<details><summary>Click to expand details</summary>

1. **Generate a CSV template for the AWSSM certificate store**

    ```shell
    kfutil stores import generate-template --store-type-name AWSSM --outpath AWSSM.csv
    ```
2. **Populate the generated CSV file**

    Open the CSV file, and reference the table below to populate parameters for each **Attribute**.

   | Attribute | Description |
   | --------- | ----------- |
   | Category | Select "AwsSecretsManager PEM" or the customized certificate store name from the previous step. |
   | Container | Optional container to associate certificate store with. |
   | Client Machine |  |
   | Store Path | The store path contains the AWS region where the SecretsManager resides.  It can optionally accept values for tags OR path prefix for identifying secrets to be managed by the cert store instance.  example:'us-east-2 [prefix='dev/midwest']' or 'us-east1 [tagName='managedBy' tagValue='keyfactor']'  |
   | Orchestrator | Select an approved orchestrator capable of managing `AWSSM` certificates. Specifically, one with the `AWSSM` capability. |
   | Properties.UseDefaultSdkAuth | A switch to enable the store to use Default SDK credentials |
   | Properties.DefaultSdkAssumeRole | A switch to enable the store to assume a new Role when using Default SDK credentials |
   | Properties.UseOAuth | A switch to enable the store to use an OAuth provider workflow to authenticate with AWS |
   | Properties.OAuthScope | This is the OAuth Scope needed for Okta OAuth, defined in Okta |
   | Properties.OAuthGrantType | In OAuth 2.0, the term 'grant type' refers to the way an application gets an access token. In Okta this is `client_credentials` |
   | Properties.OAuthUrl | The token endpoint for the OAuth 2.0 provider |
   | Properties.OAuthClientId | The Client ID for OAuth. |
   | Properties.OAuthClientSecret | The Client Secret for OAuth. |
   | Properties.UseIAM | A switch to enable the store to use IAM User auth to assume a role when authenticating with AWS |
   | Properties.IAMUserAccessKey | The AWS Access Key for an IAM User |
   | Properties.IAMUserAccessSecret | The AWS Access Secret for an IAM User. |
   | Properties.ExternalId | An optional parameter sts:ExternalId to pass with Assume Role calls |

3. **Import the CSV file to create the certificate stores**

    ```shell
    kfutil stores import csv --store-type-name AWSSM --file AWSSM.csv
    ```

</details>


#### PAM Provider Eligible Fields
<details><summary>Attributes eligible for retrieval by a PAM Provider on the Universal Orchestrator</summary>

If a PAM provider was installed _on the Universal Orchestrator_ in the [Installation](#Installation) section, the following parameters can be configured for retrieval _on the Universal Orchestrator_.

   | Attribute | Description |
   | --------- | ----------- |
   | OAuthClientId | The Client ID for OAuth. |
   | OAuthClientSecret | The Client Secret for OAuth. |
   | IAMUserAccessKey | The AWS Access Key for an IAM User |
   | IAMUserAccessSecret | The AWS Access Secret for an IAM User. |

Please refer to the **Universal Orchestrator (remote)** usage section ([PAM providers on the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam)) for your selected PAM provider for instructions on how to load attributes orchestrator-side.
> Any secret can be rendered by a PAM provider _installed on the Keyfactor Command server_. The above parameters are specific to attributes that can be fetched by an installed PAM provider running on the Universal Orchestrator server itself.

</details>


> The content in this section can be supplemented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/ReferenceGuide/Certificate%20Stores.htm?Highlight=certificate%20store).


## Discovering Certificate Stores with the Discovery Job

### AwsSecretsManager PEM Discovery Job
TODO Global Store Type Section is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info


TODO Discovery Job Configuration is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info




## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Universal Orchestrator extensions](https://github.com/orgs/Keyfactor/repositories?q=orchestrator).