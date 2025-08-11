## Overview 

The AWSSMPEM certificate store type provided by this integration is the one to use for managing certificates stored in AWS Secrets Manager in the PEM format.
Certificates managed by this certificate store are expected to have the PEM formatted certificate stored as a SecretString in AWS Secrets Manager.

### Requirements

1. [Certificate Format](#certificate-format)
1. [Configuring the Certificate Store](#configuring-the-certificate-store)
1. [Example](#example)
1. [Troubleshooting](#troubleshooting)

---

#### Certificate Format

For this certificate store type, the certificates are expected to be stored as a [PEM-formatted](https://en.wikipedia.org/wiki/Privacy-Enhanced_Mail) string, in the 
"SecretString" field of the AWS Secrets Manager Secret.

When enrolling a certificate from Keyfactor Command into the Certificate Store with this type (AWSSMPEM), it will be stored as a PEM
formatted string, including the private key, with no seperate password.

---

#### Configuring the Certificate Store



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
