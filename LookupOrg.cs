using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace api.multifol.io
{
    public class LookupOrg
    {
        private readonly ILogger<LookupOrg> _logger;

        public LookupOrg(ILogger<LookupOrg> logger)
        {
            _logger = logger;
        }

        [Function("LookupOrg")]
        public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            string? keyVaultName = Environment.GetEnvironmentVariable("KEY_VAULT_NAME");
            var kvUri = "https://" + keyVaultName + ".vault.azure.net";
            _logger.LogInformation($"KeyVault uri: {kvUri}");

            var secretClient = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());

            _logger.LogInformation("Get secrets");
            var userID = await secretClient.GetSecretAsync("database");
            var dbName = await secretClient.GetSecretAsync("db-user");
            var pw = await secretClient.GetSecretAsync("db-pw");
            var server = await secretClient.GetSecretAsync("server");

            _logger.LogInformation("C# HTTP trigger function processed a request.");
            var builder = new MySqlConnectionStringBuilder
            {
                Server = server.Value.Value + ".mysql.database.azure.com",
                Database = dbName.Value.Value,
                UserID = userID.Value.Value,
                Password = pw.Value.Value,
                SslMode = MySqlSslMode.Required,
            };

            // TODO: support multiple terms
            var term = req.Query["term"];

            using (var conn = new MySqlConnection(builder.ConnectionString))
            {
                Console.WriteLine("Opening connection");
                await conn.OpenAsync();

                string sql =
                            "select *, 1 as rowOrder from organizations where " +
                            "MATCH (Organization) AGAINST ('@term' IN BOOLEAN MODE) " +
                            "UNION " +
                            "select *, 2 as rowOrder from organizations where " +
                            "MATCH(Organization) AGAINST(concat('@term* -@term') IN BOOLEAN MODE) " +
                            "ORDER by rowOrder, Organization";

                using (var command = new MySqlCommand(sql, conn))
                {
                    command.Parameters.AddWithValue("@term", term);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var EIN = reader.GetString(0);
                            var organization = reader.GetString(1);
                            var city = reader.GetString(2);
                            var state = reader.GetString(3);
                            var businessCode = reader.GetString(4);
                            _logger.LogInformation($"{EIN}, {organization}, {city}, {state}, {businessCode}");
                        }
                    }
                }

                return new OkObjectResult("Welcome to Azure Functions!");
            }
        }
    }
}
