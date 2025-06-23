
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager
{
    internal static class Helpers
    {
        internal static string ResolvePAMField(IPAMSecretResolver resolver, ILogger logger, string name, string key)
        {
            if (resolver == null) return key;
            else
            {
                return resolver.Resolve(key);
            }
        }

        public static bool IsValidJson(this string jsonString)
        {
            try
            {
                var unescapedJSON = System.Text.RegularExpressions.Regex.Unescape(jsonString);
                var tmpObj = JsonValue.Parse(unescapedJSON);
            }
            catch (FormatException fex)
            {
                //Invalid json format
                return false;
            }
            catch (Exception ex) //some other exception
            {
                return false;
            }
            return true;
        }

        public static string GetPemStringFromPfx(string base64Pfx, string pfxPassword)
        {
            byte[] pfxBytes = Convert.FromBase64String(base64Pfx); // 1. Decode base64

            try
            {
                using (var memoryStream = new MemoryStream(pfxBytes))
                {
                    var builder = new Pkcs12StoreBuilder();

                    var store = builder.Build();
                    store.Load(memoryStream, pfxPassword.ToCharArray());

                    var alias = store.Aliases.First().ToString();
                    var certEntry = store.GetCertificate(alias);
                    var keyEntry = store.GetKey(alias);
                    var privateKey = keyEntry.Key;


                    // 4. Use PemWriter to write the certificate and private key to PEM format
                    using (var stringWriter = new StringWriter())
                    {
                        PemWriter pemWriter = new PemWriter(stringWriter);

                        pemWriter.WriteObject(certEntry);
                        pemWriter.WriteObject(privateKey);
                        pemWriter.Writer.Flush();

                        // 5. Get the PEM string
                        return stringWriter.ToString();
                    }
                }

            }
            catch (Exception ex)
            {
                // Handle exceptions appropriately
                Console.WriteLine($"Error converting PFX to PEM: {ex.Message}");
                return null;
            }
        }

        public static byte[] ConvertPfxToPasswordlessPkcs12(string base64Pfx, string pfxPassword)
        {
            // Decode the Base64-encoded PFX data
            byte[] pfxBytes = Convert.FromBase64String(base64Pfx);
            using (var inputStream = new MemoryStream(pfxBytes))
            {
                var builder = new Pkcs12StoreBuilder();
                builder.SetUseDerEncoding(true);
                var store = builder.Build();
                store.Load(inputStream, pfxPassword.ToCharArray());

                string alias = null;
                foreach (string a in store.Aliases)
                {
                    if (store.IsKeyEntry(a))
                    {
                        alias = a;
                        break;
                    }
                }

                using (var outputStream = new MemoryStream())
                {
                    var newStore = builder.Build();

                    if (alias != null)
                    {
                        // Extract private key and certificate chain if available
                        var keyEntry = store.GetKey(alias);
                        var chain = store.GetCertificateChain(alias);
                        newStore.SetKeyEntry("converted-key", keyEntry, chain);
                    }
                    else
                    {
                        // If no private key, include just the certificate chain
                        foreach (string certAlias in store.Aliases)
                        {
                            if (store.IsCertificateEntry(certAlias))
                            {
                                var cert = store.GetCertificate(certAlias);
                                newStore.SetCertificateEntry(certAlias, cert);
                            }
                        }
                    }

                    // Save the new PKCS#12 store without a password
                    newStore.Save(outputStream, null, new Org.BouncyCastle.Security.SecureRandom());
                    return outputStream.ToArray();
                }
            }
        }
    }
}
