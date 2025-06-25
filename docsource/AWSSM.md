## Overview 

This AWS Secrets Manager Orchestrator extension adds the ability to manage certificates stored in AWS Secrets Manager as secrets via Keyfactor Command.
The [AWS Secrets Manager](https://aws.amazon.com/secrets-manager/) service provides a secure way to manage secrets of any type, across your enterprise infrastructure.
This integration will allow you to generate and store certificates, remove certificates and maintain an inventory of certificates stored in AWS Secrets Manager; providing a 
comprehensive way to manage certificates used within your enterprise.


### Requirements

1. [Certificate Format](#using-the-repository)
1. [Secret Naming ](#using-the-repository)
1. [Authenticating and Permissions](#updating-the-integration-manifest.json)

---

#### Certificate Format

AWS Secrets Manager is essentially a key-value store that allows storing of arbitrary string or binary data in a secure way.  Considering that there is no inherent requirement regarding
formatting of certificates within the platform; this poses a challenge when determining which secrets may be a valid certificate that should be managed by this extension.

#### In order to address this while providing maximum compatibility, this integration will store newly issued certificates in AWS Secrets Manager as a **PEM** certificate, including the private key, in **Text** format.

---

#### Secret Naming

A common strategy used for organizing secrets with AWS Secrets Manager is to include a path structure in the secret name; for example 'dev/integrations/awssmtestcert' could imply the heirarchy of <org unit>/<team>/<cert>.
The benefit of this approach is that it provides a way to organize and filter for specific sub-sets of secrets when querying the API.

If you utilize a similar strategy for naming your certificates stored as a secret in AWS Secrets Manager, this integration allows you to specify the name prefix (or path) for all certificates that should be managed by an instance of a certificate store in Keyfactor Command.

We also provide the ability to use tags associated with the secret for the same purpose by providing a tag name and value that all certificate secrets managed by an instance of a certificate store will have.

> :warning: When performing a certificate enrollment into a certificate store, it will incorporate the same convention that is defined in the certificate store.

- **Example 1**:  if enrolling a certificate with alias "testcert", and adding it via Keyfactor Command to a certificate store with the "prefix" defined as "users/personal", then the full secret name will be: "users/personal/testcert"
- **Example 2**:  enrolling the same certificate into a certificate store with no prefix defined, but a tagName of "managedBy" and value of "Keyfactor", the secret name would be simply "testcert", and we associate the tag {"managedBy": "Keyfactor"} with the newly created secret.

In summary; supplying a name prefix or tag name and value as part of a certificate store definition in Keyfactor Command has the following implications:
 - Inventory Jobs will only return certificates where the name begins with the prefix, and/or the tagName exists on the secret and contains the provided tagValue.
 - Enrollment into these stores will apply the same convention to newly added certificate secrets; appending the prefix to the name and/or associating the tag name and value.



---

#### Authenticating and Permissions


> [!NOTE]
> In order to support the variety of alternative strategies available for authenticating into AWS, this integration utilizes the [Keyfactor AWS Auth Library](https://github.com/Keyfactor/aws-auth-library) 
> Refer to the documentation [here](https://github.com/Keyfactor/aws-auth-library) for details and examples.

##### AWS Secret Manager Permissions

Here is a list of the IAM actions that the authenticating identity should have in order to perform all Jobs supported by this extension:


- `secretsmanager.ListSecrets`
- `secretsManager.GetSecretValue` 
- `secretsmanager:CreateSecret`
- `secretsmanager.DeleteSecret` 
- `secretsmanager.UpdateSecret`
- `secretsmanager.TagResource`  if using tags for filtering or to add tags via entry parameters.

For more information on these permission actions, refer to the [AWS Documentation](https://docs.aws.amazon.com/service-authorization/latest/reference/list_awssecretsmanager.html).

---