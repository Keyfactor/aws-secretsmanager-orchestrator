using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;


namespace Keyfactor.Extensions.Orchestrators.AwsSecretsManager.Jobs
{
    public class JobBase<T> : IOrchestratorJobExtension
    {
        public string ExtensionName => Constants.STORE_TYPE_NAME;
        internal protected ILogger logger { get; set; }

        public virtual AwsSecretsManagerClient SecretsManagerClient { get; set; }
        internal protected virtual AwsSecretsManagerJobParameters JobParameters { get; set; }
        internal protected IPAMSecretResolver PamSecretResolver { get; set; }


        public void Initialize(dynamic config)
        {
            JobParameters = new AwsSecretsManagerJobParameters();

            try
            {


            }
            finally { }
        }
    }
}
