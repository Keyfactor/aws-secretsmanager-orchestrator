
//  Copyright 2025 Keyfactor
//  Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
//  and limitations under the License.

using System;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using Microsoft.Extensions.Logging;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using System.Threading.Tasks;


namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Jobs
{
    [Job(KeyfactorJobType.MANAGEMENT)]
    public class Management : JobBase<Management>, IManagementJobExtension
    {
        public Management(IPAMSecretResolver resolver) : base(resolver) { }

        public JobResult ProcessJob(ManagementJobConfiguration config)
        {
            _logger.MethodEntry();
            var jobType = config.OperationType.ToString();

            _logger.LogTrace($"received new Management > {jobType} job. Job ID = {config.JobId}");
            _logger.LogTrace($"initializing Management > {jobType} job..");

            base.Initialize(config);

            _logger.LogDebug($"begin Management > {jobType}...");

            try
            {
                switch (config.OperationType)
                {
                    case CertStoreOperationType.Add:
                        return AddCertificate();
                    case CertStoreOperationType.Remove:
                        return RemoveCertificate().Result;
                    default:
                        // this should never occur
                        return FailureJobResult($"Unsupported operation: {config.OperationType.ToString()}");
                }
            }
            catch (Exception ex)
            {
                //Status: 2=SuccessJobResult, 3=WarningJobResult, 4=Error
                var msg = $"an error occurred.  Management > {config.OperationType.ToString()} job was not successful.\n{ex.Message}";
                _logger.LogError(msg);
                return FailureJobResult(msg);
            }
            finally
            {
                _logger.MethodExit();
            }
        }

        /// <summary>
        /// Checks for an existing secret with the same name, if exists and overwrite flag is true, replace the secret.
        /// if overwrite is false, skip.
        /// if no existing secret with the name exists, create a new one for the certificate.
        /// </summary>
        /// <returns>JobResult with status</returns>
        public JobResult AddCertificate()
        {
            _logger.MethodEntry();
            var arn = string.Empty;

            try
            {
                var certARN = _secretsManagerClient.AddOrUpdateSecret(JobParameters).Result;
                return SuccessJobResult($"Successfully enrolled certificate with alias '{JobParameters.CertProperties.Alias}'.\nARN: {certARN}");
            }
            catch (Exception ex)
            {
                var msg = $"an error occurred when attempting to add the certificate.\n{ex.Message}";
                _logger.LogError(msg);
                return FailureJobResult(msg);
            }
            finally
            {
                _logger.MethodExit();
            }
        }

        public async Task<JobResult> RemoveCertificate()
        {
            _logger.MethodEntry();

            try 
            {
                _logger.LogTrace($"sending request to remove secret named {JobParameters.SecretName}");
                await _secretsManagerClient.RemoveSecret(JobParameters.SecretName);
                return SuccessJobResult();
            }
            catch (Exception ex) 
            {
                var msg = $"there was an error when attempting to remove the secret: {ex.Message}";
                _logger.LogError(msg);
                return FailureJobResult(msg);
            }
            finally { _logger.MethodExit(); }
        }
    }
}