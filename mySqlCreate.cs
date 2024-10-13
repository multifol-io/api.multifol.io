using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace api.multifol.io
{
    public class mySqlCreate
    {
        private readonly ILogger<mySqlCreate> _logger;

        public mySqlCreate(ILogger<mySqlCreate> logger)
        {
            _logger = logger;
        }

        [Function("mySqlCreate")]
        public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            string? keyVaultName = Environment.GetEnvironmentVariable("KEY_VAULT_NAME");
            var kvUri = "https://" + keyVaultName + ".vault.azure.net";
            var secretClient = new SecretClient(new Uri(kvUri), new DefaultAzureCredential());

            var userID = await secretClient.GetSecretAsync("db-user");
            var pw = await secretClient.GetSecretAsync("db-pw");
            var server = await secretClient.GetSecretAsync("server");

            _logger.LogInformation("C# HTTP trigger function processed a request.");
            var builder = new MySqlConnectionStringBuilder
            {
                Server = server.Value.Value + ".mysql.database.azure.com",
                Database = "YOUR-DATABASE",
                UserID = userID.Value.Value,
                Password = pw.Value.Value,
                SslMode = MySqlSslMode.Required,
            };

            using (var conn = new MySqlConnection(builder.ConnectionString))
            {
                Console.WriteLine("Opening connection");
                await conn.OpenAsync();

                using (var command = conn.CreateCommand())
                {
                    command.CommandText = "DROP TABLE IF EXISTS organizations;";
                    await command.ExecuteNonQueryAsync();
                    Console.WriteLine("Finished dropping table (if existed)");

                    command.CommandText = "CREATE TABLE organizations (SPONS_DFE_EIN VARCHAR(9) PRIMARY KEY, SPONSOR_DFE_NAME VARCHAR(70);";
                    await command.ExecuteNonQueryAsync();
                    Console.WriteLine("Finished creating table");

                    command.CommandText = @"INSERT INTO organizations (SPONS_DFE_EIN, SPONSOR_DFE_NAME) VALUES (@ein1, @name1),
                        (@ein2, @name2), (@ein2, @name2);";
                    command.Parameters.AddWithValue("@ein1", "23-1234");
                    command.Parameters.AddWithValue("@name1", "Microsoft");
                    command.Parameters.AddWithValue("@ein2", "23-1235");
                    command.Parameters.AddWithValue("@name2", "Docusign");
                    command.Parameters.AddWithValue("@ein3", "23-1236");
                    command.Parameters.AddWithValue("@name3", "Ford");

                    int rowCount = await command.ExecuteNonQueryAsync();
                    Console.WriteLine(String.Format("Number of rows inserted={0}", rowCount));
                }

                // connection will be closed by the 'using' block
                Console.WriteLine("Closing connection");
                return new OkObjectResult("Welcome to Azure Functions!");
        }
    }
}
