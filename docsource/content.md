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

## Requirements
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

### Certificate tag placeholders

When supplying the optional `CertificateTags` entry parameter during enrollment, tag values may include placeholder tokens that are replaced with values evaluated from the certificate before the tags are written to AWS Secrets Manager:

| Token | Replaced with |
| :---- | :------------ |
| `%SERIAL_NUMBER%` | The certificate serial number (hexadecimal). |
| `%NOT_BEFORE%` | The start of the validity period, as an ISO-8601 UTC timestamp (`yyyy-MM-ddTHH:mm:ssZ`). |
| `%NOT_AFTER%` | The end of the validity period, as an ISO-8601 UTC timestamp (`yyyy-MM-ddTHH:mm:ssZ`). |

For example, providing this `CertificateTags` value during enrollment:

```json
{"serial": "%SERIAL_NUMBER%", "expires": "%NOT_AFTER%"}
```

would write two tags on the certificate secret: `serial` containing the certificate's serial number, and `expires` containing its expiration date. Only the tag values are substituted; tag names are left as-is. This applies to all three store types.

If the `CertificateTags` value is not valid JSON, the enrollment job fails with an error identifying the `CertificateTags` parameter, rather than a generic parse error.