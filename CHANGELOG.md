## 1.2.0
* AWSSMPEM - New optional store type parameter `IncludeChain` (default false) to include the issuer chain in the single concatenated PEM secret (leaf, then chain, then private key). Ignored when `SeparatePrivateKey` is enabled, as the JSON format already includes the chain. Stores without the parameter behave as before.
* Added .NET 10 as a target framework.
* Security - AWSSMPEM inventory no longer writes the contents of a secret that cannot be parsed to the orchestrator log. Previously the full secret value, including the unencrypted private key, was logged at the Warning level. The warning now includes only the secret name and the parse error.
* Security - Secret-type store properties (OAuth client ID/secret, IAM user access key/secret) are now redacted when the store properties are written to the Trace log.
* Fixed tag handling when a certificate is renewed or overwritten: previously, if any provided tag key already existed on the secret, all existing tags were removed (including tags not managed by Keyfactor). Now only the tags with matching keys are replaced; other tags on the secret are left intact.
* Documentation - added the `secretsmanager:BatchGetSecretValue` and `secretsmanager:DescribeSecret` IAM actions to the list of required permissions.
* Documentation - added the conditionally required `secretsmanager:UntagResource`, `secretsmanager:ReplicateSecretToRegions`, and `secretsmanager:RemoveRegionsFromReplication` IAM actions, and corrected the formatting of the listed action names (`secretsmanager:<Action>`) so they can be used directly in an IAM policy.

## 1.1.0
* AWSSMPEM - New optional store type parameter to indicate that the secret containing the cert and private key should use the JSON format with seperate properties for certificate and private key.
* now support placeholders in CertificateTags for 3 certificate metadata fields:
	- %SERIAL_NUMBER% 
		- replaced with certificate serial number before sent to AWS SM
	- %NOT_BEFORE% 
		- replaced with certificate validity start date
	- %NOT_AFTER% 
		- replaced with certificate expiration date
* CertificateTags and ReplicaRegions entry parameters now fail fast with a clear, actionable error when given invalid JSON, instead of a generic parse failure.
* Fixed private key export for the PEM formats so it no longer fails on Windows for keys imported from a PFX ("The requested operation is not supported"); keys are now exported via BouncyCastle.

## 1.0.1
* Fix for issue where entry parameters were not visible in Command after inventory

## 1.0.0
* initial release