using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Functions.Worker;

namespace WellsChat.Functions
{
    public class KeyVault
    {
        private readonly ILogger _logger;

        public KeyVault(ILogger<KeyVault> logger)
        {
            _logger = logger;
        }

        [Function("KeyVault")]
        public async Task<IActionResult> Run(
                       [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req)
        {
            var key = req.Query["key"];
            var secretClient = new SecretClient(new Uri(Environment.GetEnvironmentVariable("VaultUri")), new DefaultAzureCredential());
            var secret = await secretClient.GetSecretAsync(key);
            var secretValue = secret.Value.Value;
            return new OkObjectResult(secretValue);
        }
    }
}
