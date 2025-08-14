<h1 align="center" style="border-bottom: none">
    AWS Secrets Manager Universal Orchestrator Extension
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

For each format there is a corresponding certificate store type ([AWSSMPEM](#AWSSMPEM), [AWSSMPFX](#AWSSMPFX), [AWSSMJKS](#AWSSMJKS)).

The AWS Secrets Manager Universal Orchestrator extension implements 3 Certificate Store Types. Depending on your use case, you may elect to use one, or all of these Certificate Store Types. Descriptions of each are provided below.

- [AwsSecretsManager PEM](#AWSSMPEM)

- [AwsSecretsManager PFX](#AWSSMPFX)

- [AwsSecretsManager JKS](#AWSSMJKS)


## Compatibility

This integration is compatible with Keyfactor Universal Orchestrator version 10.1 and later.

## Support

The AWS Secrets Manager Universal Orchestrator extension is community open source and there is **no SLA**. Keyfactor will address issues as resources become available.

> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute bug fixes or additional enhancements, use the **[Pull requests](../../pulls)** tab.

## Requirements & Prerequisites

Before installing the AWS Secrets Manager Universal Orchestrator extension, we recommend that you install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.

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


## Certificate Store Types

To use the AWS Secrets Manager Universal Orchestrator extension, you **must** create the Certificate Store Types required for your use-case. This only needs to happen _once_ per Keyfactor Command instance.

The AWS Secrets Manager Universal Orchestrator extension implements 3 Certificate Store Types. Depending on your use case, you may elect to use one, or all of these Certificate Store Types.

### AWSSMPEM

<details><summary>Click to expand details</summary>


The AWSSMPEM certificate store type provided by this integration is the one to use for managing certificates stored in AWS Secrets Manager in the PEM format.
Certificates managed by this certificate store are expected to have the PEM formatted certificate stored as a SecretString in AWS Secrets Manager.


#### AwsSecretsManager PEM Requirements


1. [Certificate Format](#certificate-format)
1. [Configuring the Certificate Store](#configuring-the-certificate-store)
1. [Example](#example)
1. [Troubleshooting](#troubleshooting)

---

##### Certificate Format

For this certificate store type, the certificates are expected to be stored as a [PEM-formatted](https://en.wikipedia.org/wiki/Privacy-Enhanced_Mail) string, in the 
"SecretString" field of the AWS Secrets Manager Secret.

When enrolling a certificate from Keyfactor Command into the Certificate Store with this type (AWSSMPEM), it will be stored as a PEM
formatted string, including the private key, with no seperate password.

---

##### Configuring the Certificate Store



A common strategy used for organizing secrets with AWS Secrets Manager is to include a path structure in the secret name; for example 'dev/integrations/awssmtestcert' could imply the heirarchy of <org unit>/<team>/<cert>.
The benefit of this approach is that it provides a way to organize and filter for specific sub-sets of secrets when querying the API.

If you utilize a similar strategy for naming your certificates stored as a secret in AWS Secrets Manager, this integration allows you to specify the name prefix (or path) for all certificates that should be managed by an instance of a certificate store in Keyfactor Command.

We also provide the ability to use tags associated with the secret for the same purpose by providing a tag name and value that all certificate secrets managed by an instance of a certificate store will have.

> :warning: When performing a certificate enrollment into a certificate store, it will incorporate the same convention that is defined in the certificate store.

- **Example 1**:  if enrolling a certificate with alias "testcert", and adding it via Keyfactor Command to a certificate store with the "prefix" defined as "users/personal", then the full secret name will be: "users/personal/testcert"
- **Example 2**:  enrolling the same certificate into a certificate store with no prefix defined, but a tagName of "managedBy" and value of "Keyfactor", the secret name would be simply "testcert", and we associate the tag {"managedBy": "Keyfactor"} with the newly created secret.

In summary: supplying a name prefix or tag name and value as part of a certificate store definition in Keyfactor Command has the following implications:
 - Inventory Jobs will only return certificates where the name begins with the prefix, and/or the tagName exists on the secret and contains the provided tagValue.
 - Enrollment into these stores will apply the same convention to newly added certificate secrets; appending the prefix to the name and/or associating the tag name and value.

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

   <details><summary>Click to expand AWSSMPEM kfutil details</summary>

   ##### Using online definition from GitHub:
   This will reach out to GitHub and pull the latest store-type definition
   ```shell
   # AwsSecretsManager PEM

   kfutil store-types create AWSSMPEM
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
Below are instructions on how to create the AWSSMPEM store type manually in
the Keyfactor Command Portal
   <details><summary>Click to expand manual AWSSMPEM details</summary>

   Create a store type called `AWSSMPEM` with the attributes in the tables below:

   ##### Basic Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Name | AwsSecretsManager PEM | Display name for the store type (may be customized) |
   | Short Name | AWSSMPEM | Short display name for the store type |
   | Capability | AWSSMPEM | Store type name orchestrator will register with. Check the box to allow entry of value |
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

   ![AWSSMPEM Basic Tab](docsource/images/AWSSMPEM-basic-store-type-dialog.png)

   ##### Advanced Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Supports Custom Alias | Required | Determines if an individual entry within a store can have a custom Alias. |
   | Private Key Handling | Optional | This determines if Keyfactor can send the private key associated with a certificate to the store. Required because IIS certificates without private keys would be invalid. |
   | PFX Password Style | Default | 'Default' - PFX password is randomly generated, 'Custom' - PFX password may be specified when the enrollment job is created (Requires the Allow Custom Password application setting to be enabled.) |

   The Advanced tab should look like this:

   ![AWSSMPEM Advanced Tab](docsource/images/AWSSMPEM-advanced-store-type-dialog.png)

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

   ![AWSSMPEM Custom Fields Tab](docsource/images/AWSSMPEM-custom-fields-store-type-dialog.png)

   ##### Entry Parameters Tab

   | Name | Display Name | Description | Type | Default Value | Entry has a private key | Adding an entry | Removing an entry | Reenrolling an entry |
   | ---- | ------------ | ---- | ------------- | ----------------------- | ---------------- | ----------------- | ------------------- | ----------- |
   | CertificateTags | Certificate Tags | If desired, tags can be applied to the certificate entries in AWS Secrets Manager.  Provide them as a JSON string of key-value pairs ie: '{'tag-name': 'tag-content', 'other-tag-name': 'other-tag-content'}' | string |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |
   | ReplicaRegions | Replica Regions | To replicate secrets to other regions, you can provide them here as a JSON array in the format: [{ 'KmsKeyId': '<optionally specify the encryption key ID', 'Region': '<region name>'}, {...}] | string |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |

   The Entry Parameters tab should look like this:

   ![AWSSMPEM Entry Parameters Tab](docsource/images/AWSSMPEM-entry-parameters-store-type-dialog.png)

   </details>
</details>

### AWSSMPFX

<details><summary>Click to expand details</summary>


The AWSSMPFX certificate store type allows managing certificates stored in AWS Secrets Manager in the PFX format via Keyfactor Command.
Since AWS Secrets Manager is designed to store arbitrary secrets of any type, it is necessary that we implement a convention for identifying and writing these certificates as 
AWS Secrets Manager secrets.




#### AwsSecretsManager PFX Requirements

1. [Certificate Format](#certificate-format)
1. [Certificate Store Filtering](#defining-certificate-store-inclusion-criteria)
1. [Issuing Certificates](#issuing-certificates)

---

#### Certificate Format

AWS Secrets Manager is essentially a key-value store that allows storing of arbitrary string or binary data in a secure way.  Considering that there is no inherent requirement regarding
formatting of certificates within the platform; this poses a challenge when determining which secrets may be a valid certificate that should be managed by this extension.

#### Defining Certificate Store Inclusion Criteria

When creating a certificate store in Keyfactor Command, we recommend including a filter to identify the subset of all of the secrets that contain certificates to be managed by the certificate store.

A filter can be defined in the "Store Path" field when creating a certificate store

You can use either tags or a prefix naming convention to define which secrets should be included when attempting to perform an inventory.

The format of the Store Path for AWSSMPFX certificate stores is `<AWS Region> <[tag or name filter criteria (optional)]>`

Here are a few examples of Store Path values with explanations:

| Store Path Value | Filter Description | Details |
| :--------------- | :----------------- | :------ |
| us-east-2        | no filter          | all secrets that the authenticating identity has access to will be considered |
| us-east-2 [TagName="PFXcert"]  | TagName = "PFXcert" | all secrets that include the tag named "PFXcert".  The tag value is not considered |
| us-east-2 [TagName="PFXcert" TagValue="Keyfactor"] | TagName = "PFXcert" AND TagValue="Keyfactor" | all secrets that contain the tag named "PFXcert" and the tag value "Keyfactor" |
| us-east-2 [TagValue="KFcerts"] | INVALID | TagName required when setting TagValue | 
| us-east-2 [Prefix="ssl/prod/"] | secrets named "ssl/prod/*" | all secrets with a name beginning with "ssl/prod/" (logical heirarchy) |
| us-east-2 [Prefix="ssl/" TagName="PFXcert" TagValue="prod"] | secrets named "ssl/**" that have the tag named "PFXcert" with a tag value of "prod" | combined name and tag filters |

> :warning: In addition to the provided Tag or name prefix; any certificates added outside of Keyfactor Command will have to be stored with the same convention to be included when performing an Inventory
> ie. two separate secrets; one for the certificate stored as a secret binary and one for the password stored as a secret string.

##### Secret Naming

A common strategy used for organizing secrets with AWS Secrets Manager is to include a path structure in the secret name; for example 'dev/integrations/awssmtestcert' could imply the heirarchy of <org unit>/<team>/<cert>.
The benefit of this approach is that it provides a way to organize and filter for specific sub-sets of secrets when querying the API.

If you utilize a similar strategy for naming your certificates stored as a secret in AWS Secrets Manager, this integration allows you to specify the name prefix (or path) for all certificates that should be managed by an instance of a certificate store in Keyfactor Command.

We also provide the ability to use tags associated with the secret for the same purpose by providing a tag name and value that all certificate secrets managed by an instance of a certificate store will have.

> :warning: When enrolling a certificate into a certificate store, it will apply the same convention that is defined in the store path of the certificate store.

- **Example 1**:  if enrolling a certificate with alias "testcert", and adding it via Keyfactor Command to a certificate store with the "Prefix" defined as "users/personal", then the full secret name will be: "users/personal/testcert"
- **Example 2**:  enrolling the same certificate into a certificate store with no prefix defined, but a TagName of "managedBy" and value of "Keyfactor", the secret name would be simply "testcert", and we associate the tag {"managedBy": "Keyfactor"} with the newly created secret.

In summary; supplying a name prefix or tag name and value as part of a certificate store definition in Keyfactor Command has the following implications:
 - Inventory Jobs will only return certificates where the name begins with the prefix, and/or the tagName exists on the secret and contains the provided tagValue.
 - Enrollment into these stores will apply the same convention to newly added certificate secrets; appending the prefix to the name and/or associating the tag name and value.

#### Issuing Certificates

##### Whenever a certificate is issued via Keyfactor Command, it will have the following properties:
- The certificate will be stored in a secret that shares the name of the certificate alias.
- It will be saved as a Secret Binary, containing the PFX certificate store.
- A second secret named \<alias\>-pw containing the PFX password as a Secret String.
- A tag will be added to the first secret called "PasswordSecret" and will contain the name of the secret containing the password.
- If a "prefix" is defined in the store path, it will be prepended to the names of both secrets.
- If a "TagName" and optional "TagValue" are provided in the store path, those tags will be added to the certificate secret.

##### Example
Enrolling a certificate with the alias "mycert" into a AWSSMPFX certificate store that has a store path value of "us-east-2 [prefix="devteam"]" will result in the following..
1. A secret named "devteam\mycert" is created
	- This secret contains the password protected PFX certificate store in the Secret Binary property.
	- It is associated with a new tag: {"PasswordSecret" : "devteam\mycert-pw"}
1. A secret named "devteam\mycert-pw" is created
    - This secreet contains the pfx password in the Secret String property.
	- It is associated with a new tag: {"PasswordFor" : "devteam\mycert"}

---



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
   <details><summary>Click to expand AWSSMPFX kfutil details</summary>

   ##### Using online definition from GitHub:
   This will reach out to GitHub and pull the latest store-type definition
   ```shell
   # AwsSecretsManager PFX
   kfutil store-types create AWSSMPFX
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
Below are instructions on how to create the AWSSMPFX store type manually in
the Keyfactor Command Portal
   <details><summary>Click to expand manual AWSSMPFX details</summary>

   Create a store type called `AWSSMPFX` with the attributes in the tables below:

   ##### Basic Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Name | AwsSecretsManager PFX | Display name for the store type (may be customized) |
   | Short Name | AWSSMPFX | Short display name for the store type |
   | Capability | AWSSMPFX | Store type name orchestrator will register with. Check the box to allow entry of value |
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

   ![AWSSMPFX Basic Tab](docsource/images/AWSSMPFX-basic-store-type-dialog.png)

   ##### Advanced Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Supports Custom Alias | Required | Determines if an individual entry within a store can have a custom Alias. |
   | Private Key Handling | Optional | This determines if Keyfactor can send the private key associated with a certificate to the store. Required because IIS certificates without private keys would be invalid. |
   | PFX Password Style | Default | 'Default' - PFX password is randomly generated, 'Custom' - PFX password may be specified when the enrollment job is created (Requires the Allow Custom Password application setting to be enabled.) |

   The Advanced tab should look like this:

   ![AWSSMPFX Advanced Tab](docsource/images/AWSSMPFX-advanced-store-type-dialog.png)

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

   ![AWSSMPFX Custom Fields Tab](docsource/images/AWSSMPFX-custom-fields-store-type-dialog.png)

   ##### Entry Parameters Tab

   | Name | Display Name | Description | Type | Default Value | Entry has a private key | Adding an entry | Removing an entry | Reenrolling an entry |
   | ---- | ------------ | ---- | ------------- | ----------------------- | ---------------- | ----------------- | ------------------- | ----------- |
   | CertificateTags | Certificate Tags | If desired, tags can be applied to the certificate entries in AWS Secrets Manager.  Provide them as a JSON string of key-value pairs ie: '{'tag-name': 'tag-content', 'other-tag-name': 'other-tag-content'}' | string |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |
   | ReplicaRegions | Replica Regions | To replicate secrets to other regions, you can provide them here as a JSON array in the format: [{ 'KmsKeyId': '<optionally specify the encryption key ID', 'Region': '<region name>'}, {...}] | string |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |

   The Entry Parameters tab should look like this:

   ![AWSSMPFX Entry Parameters Tab](docsource/images/AWSSMPFX-entry-parameters-store-type-dialog.png)

   </details>
</details>

### AWSSMJKS

<details><summary>Click to expand details</summary>


The AWSSMJKS certificate store type allows managing certificates stored in AWS Secrets Manager in the JKS (Java Keystore) format via Keyfactor Command.
Since AWS Secrets Manager is designed to store arbitrary secrets of any type, it is necessary that we implement a convention for identifying and writing these certificates as 
AWS Secrets Manager secrets.




#### AwsSecretsManager JKS Requirements

1. [Certificate Format](#certificate-format)
1. [Certificate Store Filtering](#defining-certificate-store-inclusion-criteria)
1. [Issuing Certificates](#issuing-certificates)

---

#### Certificate Format

AWS Secrets Manager is essentially a key-value store that allows storing of arbitrary string or binary data in a secure way.  Considering that there is no inherent requirement regarding
formatting of certificates within the platform; this poses a challenge when determining which secrets may be a valid certificate that should be managed by this extension.

#### Defining Certificate Store Inclusion Criteria

When creating a certificate store in Keyfactor Command, we recommend including a filter to identify the subset of all of the secrets that contain certificates to be managed by the certificate store.

A filter can be defined in the "Store Path" field when creating a certificate store

You can use either tags or a prefix naming convention to define which secrets should be included when attempting to perform an inventory.

The format of the Store Path for AWSSMJKS certificate stores is `<AWS Region> <[tag or name filter criteria (optional)]>`

Here are a few examples of Store Path values with explanations:

| Store Path Value | Filter Description | Details |
| :--------------- | :----------------- | :------ |
| us-east-2        | no filter          | all secrets that the authenticating identity has access to will be considered |
| us-east-2 [TagName="JKScert"]  | TagName = "JKScert" | all secrets that include the tag named "JKScert".  The tag value is not considered |
| us-east-2 [TagName="JKScert" TagValue="Keyfactor"] | TagName = "JKScert" AND TagValue="Keyfactor" | all secrets that contain the tag named "JKScert" and the tag value "Keyfactor" |
| us-east-2 [TagValue="KFcerts"] | INVALID | TagName required when setting TagValue | 
| us-east-2 [Prefix="ssl/prod/"] | secrets named "ssl/prod/*" | all secrets with a name beginning with "ssl/prod/" (logical heirarchy) |
| us-east-2 [Prefix="ssl/" TagName="JKScert" TagValue="prod"] | secrets named "ssl/**" that have the tag named "JKScert" with a tag value of "prod" | combined name and tag filters |

> :warning: In addition to the provided Tag or name prefix; any certificates added outside of Keyfactor Command will have to be stored with the same convention to be included when performing an Inventory
> ie. two separate secrets; one for the certificate stored as a secret binary and one for the password stored as a secret string.

##### Secret Naming

A common strategy used for organizing secrets with AWS Secrets Manager is to include a path structure in the secret name; for example 'dev/integrations/awssmtestcert' could imply the heirarchy of <org unit>/<team>/<cert>.
The benefit of this approach is that it provides a way to organize and filter for specific sub-sets of secrets when querying the API.

If you utilize a similar strategy for naming your certificates stored as a secret in AWS Secrets Manager, this integration allows you to specify the name prefix (or path) for all certificates that should be managed by an instance of a certificate store in Keyfactor Command.

We also provide the ability to use tags associated with the secret for the same purpose by providing a tag name and value that all certificate secrets managed by an instance of a certificate store will have.

> :warning: When enrolling a certificate into a certificate store, it will apply the same convention that is defined in the store path of the certificate store.

- **Example 1**:  if enrolling a certificate with alias "testcert", and adding it via Keyfactor Command to a certificate store with the "Prefix" defined as "users/personal", then the full secret name will be: "users/personal/testcert"
- **Example 2**:  enrolling the same certificate into a certificate store with no prefix defined, but a TagName of "managedBy" and value of "Keyfactor", the secret name would be simply "testcert", and we associate the tag {"managedBy": "Keyfactor"} with the newly created secret.

In summary; supplying a name prefix or tag name and value as part of a certificate store definition in Keyfactor Command has the following implications:
 - Inventory Jobs will only return certificates where the name begins with the prefix, and/or the tagName exists on the secret and contains the provided tagValue.
 - Enrollment into these stores will apply the same convention to newly added certificate secrets; appending the prefix to the name and/or associating the tag name and value.

#### Issuing Certificates

##### Whenever a certificate is issued via Keyfactor Command, it will have the following properties:
- The certificate will be stored in a secret that shares the name of the certificate alias.
- It will be saved as a Secret Binary, containing the JKS certificate store.
- A second secret named \<alias\>-pw containing the JKS password as a Secret String.
- A tag will be added to the first secret called "PasswordSecret" and will contain the name of the secret containing the password.
- If a "prefix" is defined in the store path, it will be prepended to the names of both secrets.
- If a "TagName" and optional "TagValue" are provided in the store path, those tags will be added to the certificate secret.

##### Example
Enrolling a certificate with the alias "mycert" into a AWSSMJKS certificate store that has a store path value of "us-east-2 [prefix="devteam"]" will result in the following..
1. A secret named "devteam\mycert" is created
	- This secret contains the password protected JKS certificate store in the Secret Binary property.
	- It is associated with a new tag: {"PasswordSecret" : "devteam\mycert-pw"}
1. A secret named "devteam\mycert-pw" is created
    - This secreet contains the JKS password in the Secret String property.
	- It is associated with a new tag: {"PasswordFor" : "devteam\mycert"}

---



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
   <details><summary>Click to expand AWSSMJKS kfutil details</summary>

   ##### Using online definition from GitHub:
   This will reach out to GitHub and pull the latest store-type definition
   ```shell
   # AwsSecretsManager JKS
   kfutil store-types create AWSSMJKS
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
Below are instructions on how to create the AWSSMJKS store type manually in
the Keyfactor Command Portal
   <details><summary>Click to expand manual AWSSMJKS details</summary>

   Create a store type called `AWSSMJKS` with the attributes in the tables below:

   ##### Basic Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Name | AwsSecretsManager JKS | Display name for the store type (may be customized) |
   | Short Name | AWSSMJKS | Short display name for the store type |
   | Capability | AWSSMJKS | Store type name orchestrator will register with. Check the box to allow entry of value |
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


   ![AWSSMJKS Basic Tab](docsource/images/AWSSMJKS-basic-store-type-dialog.png)
   ##### Advanced Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Supports Custom Alias | Required | Determines if an individual entry within a store can have a custom Alias. |
   | Private Key Handling | Optional | This determines if Keyfactor can send the private key associated with a certificate to the store. Required because IIS certificates without private keys would be invalid. |
   | PFX Password Style | Default | 'Default' - PFX password is randomly generated, 'Custom' - PFX password may be specified when the enrollment job is created (Requires the Allow Custom Password application setting to be enabled.) |

   The Advanced tab should look like this:

   ![AWSSMJKS Advanced Tab](docsource/images/AWSSMJKS-advanced-store-type-dialog.png)

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


   ![AWSSMJKS Custom Fields Tab](docsource/images/AWSSMJKS-custom-fields-store-type-dialog.png)


   ##### Entry Parameters Tab

   | Name | Display Name | Description | Type | Default Value | Entry has a private key | Adding an entry | Removing an entry | Reenrolling an entry |
   | ---- | ------------ | ---- | ------------- | ----------------------- | ---------------- | ----------------- | ------------------- | ----------- |
   | CertificateTags | Certificate Tags | If desired, tags can be applied to the certificate entries in AWS Secrets Manager.  Provide them as a JSON string of key-value pairs ie: '{'tag-name': 'tag-content', 'other-tag-name': 'other-tag-content'}' | string |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |
   | ReplicaRegions | Replica Regions | To replicate secrets to other regions, you can provide them here as a JSON array in the format: [{ 'KmsKeyId': '<optionally specify the encryption key ID', 'Region': '<region name>'}, {...}] | string |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |

   The Entry Parameters tab should look like this:

   ![AWSSMJKS Entry Parameters Tab](docsource/images/AWSSMJKS-entry-parameters-store-type-dialog.png)

   </details>
</details>


## Installation

1. **Download the latest AWS Secrets Manager Universal Orchestrator extension from GitHub.**

    Navigate to the [AWS Secrets Manager Universal Orchestrator extension GitHub version page](https://github.com/Keyfactor/aws-secretsmanager-orchestrator/releases/latest). Refer to the compatibility matrix below to determine whether the `net6.0` or `net8.0` asset should be downloaded. Then, click the corresponding asset to download the zip archive.

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


3. **Create a new directory for the AWS Secrets Manager Universal Orchestrator extension inside the extensions directory.**

    Create a new directory called `aws-secretsmanager-orchestrator`.
    > The directory name does not need to match any names used elsewhere; it just has to be unique within the extensions directory.

4. **Copy the contents of the downloaded and unzipped assemblies from __step 2__ to the `aws-secretsmanager-orchestrator` directory.**

5. **Restart the Universal Orchestrator service.**

    Refer to [Starting/Restarting the Universal Orchestrator service](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/StarttheService.htm).


6. **(optional) PAM Integration**


    The AWS Secrets Manager Universal Orchestrator extension is compatible with all supported Keyfactor PAM extensions to resolve PAM-eligible secrets. PAM extensions running on Universal Orchestrators enable secure retrieval of secrets from a connected PAM provider.

    To configure a PAM provider, [reference the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam) to select an extension and follow the associated instructions to install it on the Universal Orchestrator (remote).


> The above installation steps can be supplemented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions).



## Defining Certificate Stores


The AWS Secrets Manager Universal Orchestrator extension implements 3 Certificate Store Types, each of which implements different functionality. Refer to the individual instructions below for each Certificate Store Type that you deemed necessary for your use case from the installation section.

<details><summary>AwsSecretsManager PEM (AWSSMPEM)</summary>

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
   | Orchestrator | Select an approved orchestrator capable of managing `AWSSMPEM` certificates. Specifically, one with the `AWSSMPEM` capability. |
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

1. **Generate a CSV template for the AWSSMPEM certificate store**

    ```shell
    kfutil stores import generate-template --store-type-name AWSSMPEM --outpath AWSSMPEM.csv
    ```
2. **Populate the generated CSV file**

    Open the CSV file, and reference the table below to populate parameters for each **Attribute**.

   | Attribute | Description |
   | --------- | ----------- |
   | Category | Select "AwsSecretsManager PEM" or the customized certificate store name from the previous step. |
   | Container | Optional container to associate certificate store with. |
   | Client Machine |  |
   | Store Path | The store path contains the AWS region where the SecretsManager resides.  It can optionally accept values for tags OR path prefix for identifying secrets to be managed by the cert store instance.  example:'us-east-2 [prefix='dev/midwest']' or 'us-east1 [tagName='managedBy' tagValue='keyfactor']'  |
   | Orchestrator | Select an approved orchestrator capable of managing `AWSSMPEM` certificates. Specifically, one with the `AWSSMPEM` capability. |
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
    kfutil stores import csv --store-type-name AWSSMPEM --file AWSSMPEM.csv
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


</details>

<details><summary>AwsSecretsManager PFX (AWSSMPFX)</summary>


### Store Creation

#### Manually with the Command UI

<details><summary>Click to expand details</summary>

1. **Navigate to the _Certificate Stores_ page in Keyfactor Command.**

    Log into Keyfactor Command, toggle the _Locations_ dropdown, and click _Certificate Stores_.

2. **Add a Certificate Store.**

    Click the Add button to add a new Certificate Store. Use the table below to populate the **Attributes** in the **Add** form.

   | Attribute | Description                                             |
   | --------- |---------------------------------------------------------|
   | Category | Select "AwsSecretsManager PFX" or the customized certificate store name from the previous step. |
   | Container | Optional container to associate certificate store with. |
   | Client Machine |  |
   | Store Path | The store path contains the AWS region where the SecretsManager resides.  It can optionally accept values for tags OR path prefix for identifying secrets to be managed by the cert store instance.  example:'us-east-2 [prefix='dev/midwest']' or 'us-east1 [tagName='managedBy' tagValue='keyfactor']'  |
   | Orchestrator | Select an approved orchestrator capable of managing `AWSSMPFX` certificates. Specifically, one with the `AWSSMPFX` capability. |
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

1. **Generate a CSV template for the AWSSMPFX certificate store**

    ```shell
    kfutil stores import generate-template --store-type-name AWSSMPFX --outpath AWSSMPFX.csv
    ```
2. **Populate the generated CSV file**

    Open the CSV file, and reference the table below to populate parameters for each **Attribute**.

   | Attribute | Description |
   | --------- | ----------- |
   | Category | Select "AwsSecretsManager PFX" or the customized certificate store name from the previous step. |
   | Container | Optional container to associate certificate store with. |
   | Client Machine |  |
   | Store Path | The store path contains the AWS region where the SecretsManager resides.  It can optionally accept values for tags OR path prefix for identifying secrets to be managed by the cert store instance.  example:'us-east-2 [prefix='dev/midwest']' or 'us-east1 [tagName='managedBy' tagValue='keyfactor']'  |
   | Orchestrator | Select an approved orchestrator capable of managing `AWSSMPFX` certificates. Specifically, one with the `AWSSMPFX` capability. |
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
    kfutil stores import csv --store-type-name AWSSMPFX --file AWSSMPFX.csv
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


</details>

<details><summary>AwsSecretsManager JKS (AWSSMJKS)</summary>


### Store Creation

#### Manually with the Command UI

<details><summary>Click to expand details</summary>

1. **Navigate to the _Certificate Stores_ page in Keyfactor Command.**

    Log into Keyfactor Command, toggle the _Locations_ dropdown, and click _Certificate Stores_.

2. **Add a Certificate Store.**

    Click the Add button to add a new Certificate Store. Use the table below to populate the **Attributes** in the **Add** form.

   | Attribute | Description                                             |
   | --------- |---------------------------------------------------------|
   | Category | Select "AwsSecretsManager JKS" or the customized certificate store name from the previous step. |
   | Container | Optional container to associate certificate store with. |
   | Client Machine |  |
   | Store Path | The store path contains the AWS region where the SecretsManager resides.  It can optionally accept values for tags OR path prefix for identifying secrets to be managed by the cert store instance.  example:'us-east-2 [prefix='dev/midwest']' or 'us-east1 [tagName='managedBy' tagValue='keyfactor']'  |
   | Orchestrator | Select an approved orchestrator capable of managing `AWSSMJKS` certificates. Specifically, one with the `AWSSMJKS` capability. |
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

1. **Generate a CSV template for the AWSSMJKS certificate store**

    ```shell
    kfutil stores import generate-template --store-type-name AWSSMJKS --outpath AWSSMJKS.csv
    ```
2. **Populate the generated CSV file**

    Open the CSV file, and reference the table below to populate parameters for each **Attribute**.

   | Attribute | Description |
   | --------- | ----------- |
   | Category | Select "AwsSecretsManager JKS" or the customized certificate store name from the previous step. |
   | Container | Optional container to associate certificate store with. |
   | Client Machine |  |
   | Store Path | The store path contains the AWS region where the SecretsManager resides.  It can optionally accept values for tags OR path prefix for identifying secrets to be managed by the cert store instance.  example:'us-east-2 [prefix='dev/midwest']' or 'us-east1 [tagName='managedBy' tagValue='keyfactor']'  |
   | Orchestrator | Select an approved orchestrator capable of managing `AWSSMJKS` certificates. Specifically, one with the `AWSSMJKS` capability. |
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
    kfutil stores import csv --store-type-name AWSSMJKS --file AWSSMJKS.csv
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


</details>


## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Universal Orchestrator extensions](https://github.com/orgs/Keyfactor/repositories?q=orchestrator).