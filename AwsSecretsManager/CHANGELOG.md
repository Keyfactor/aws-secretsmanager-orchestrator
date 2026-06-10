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