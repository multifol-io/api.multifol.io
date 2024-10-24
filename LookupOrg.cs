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
            _logger.LogInformation("Get vars");
            var userID = Environment.GetEnvironmentVariable("MYSQL_USER");
            var dbName = Environment.GetEnvironmentVariable("MYSQL_DATABASE");
            var pw = Environment.GetEnvironmentVariable("MYSQL_PASSWORD");
            var server = Environment.GetEnvironmentVariable("MYSQL_SERVER");

            _logger.LogInformation("Create connection string");
            var builder = new MySqlConnectionStringBuilder
            {
                Server = server + ".mysql.database.azure.com",
                Database = dbName,
                UserID = userID,
                Password = pw,
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
