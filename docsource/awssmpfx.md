## Overview 

The AWSSMPFX certificate store type allows managing certificates stored in AWS Secrets Manager in the PFX format via Keyfactor Command.
Since AWS Secrets Manager is designed to store arbitrary secrets of any type, it is necessary that we implement a convention for identifying and writing these certificates as 
AWS Secrets Manager secrets.

## Requirements

1. [Certificate Format](#certificate-format)
1. [Certificate Store Filtering](#defining-certificate-store-inclusion-criteria)
1. [Issuing Certificates](#issuing-certificates)

---

### Certificate Format

AWS Secrets Manager is essentially a key-value store that allows storing of arbitrary string or binary data in a secure way.  Considering that there is no inherent requirement regarding
formatting of certificates within the platform; this poses a challenge when determining which secrets may be a valid certificate that should be managed by this extension.


### Defining Certificate Store Inclusion Criteria

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

#### Secret Naming

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

### Issuing Certificates

#### Whenever a certificate is issued via Keyfactor Command, it will have the following properties:
- The certificate will be stored in a secret that shares the name of the certificate alias.
- It will be saved as a Secret Binary, containing the PFX certificate store.
- A second secret named \<alias\>-pw containing the PFX password as a Secret String.
- A tag will be added to the first secret called "PasswordSecret" and will contain the name of the secret containing the password.
- If a "prefix" is defined in the store path, it will be prepended to the names of both secrets.
- If a "TagName" and optional "TagValue" are provided in the store path, those tags will be added to the certificate secret.

#### Example
Enrolling a certificate with the alias "mycert" into a AWSSMPFX certificate store that has a store path value of "us-east-2 [prefix="devteam"]" will result in the following..
1. A secret named "devteam\mycert" is created
	- This secret contains the password protected PFX certificate store in the Secret Binary property.
	- It is associated with a new tag: {"PasswordSecret" : "devteam\mycert-pw"}
1. A secret named "devteam\mycert-pw" is created
    - This secreet contains the pfx password in the Secret String property.
	- It is associated with a new tag: {"PasswordFor" : "devteam\mycert"}

---


