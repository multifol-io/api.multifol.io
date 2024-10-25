using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System.Reflection.Emit;

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
            var userID = Environment.GetEnvironmentVariable("MYSQL_USER");
            var dbName = Environment.GetEnvironmentVariable("MYSQL_DATABASE");
            var pw = Environment.GetEnvironmentVariable("MYSQL_PASSWORD");
            var server = Environment.GetEnvironmentVariable("MYSQL_SERVER");

            var builder = new MySqlConnectionStringBuilder
            {
                Server = server + ".mysql.database.azure.com",
                Database = dbName,
                UserID = userID,
                Password = pw,
                SslMode = MySqlSslMode.Required,
            };

            // TODO: support multiple terms
            string term = req.Query["term"];
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            term = term ?? data?.term;

            if (string.IsNullOrEmpty(term))
            {
                return new OkObjectResult("Ok, 0, results returned");
            }

            try
            {
                await using var connection = new MySqlConnection(builder.ConnectionString);
                await connection.OpenAsync();

                string sql =
                            "select *, 1 as rowOrder from organizations where " +
                            "MATCH (Organization) AGAINST (@term IN BOOLEAN MODE)" +
                            "UNION " +
                            "select *, 2 as rowOrder from organizations where " +
                            "MATCH(Organization) AGAINST(concat(@term, '* -', @term) IN BOOLEAN MODE) " +
                            "ORDER by rowOrder, Organization";

                int rowCount = 0;

                using (var command = new MySqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText = sql;
                    command.Parameters.AddWithValue("term", term);

                    _logger.LogInformation($"Execute reader searching for {term}");
                    using var reader = await command.ExecuteReaderAsync();
                    bool fieldCountShown = false;
                    while (await reader.ReadAsync())
                    {
                        rowCount++;
                        if (!fieldCountShown)
                        {
                            _logger.LogInformation($"fields: {reader.FieldCount.ToString()}");
                            fieldCountShown = true;
                        }

                        var EIN = reader.GetString(0);
                        var organization = reader.GetString(1);
                        var city = reader.GetString(2);
                        var state = reader.GetString(3);
                        var businessCode = reader.GetString(4);
                        _logger.LogInformation($"{EIN}, {organization}, {city}, {state}, {businessCode}");
                    }
                }

                return new OkObjectResult($"Ok, {rowCount}, results returned");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message + '\n' + ex.StackTrace);
                return new BadRequestObjectResult("Returned BadRequest");
            }
        }
    }
}
