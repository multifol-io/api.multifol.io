using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace api.multifol.io
{
    public class GetQuotes
    {
        private readonly ILogger<GetQuotes> _logger;

        public GetQuotes(ILogger<GetQuotes> logger)
        {
            _logger = logger;
        }

        [Function("GetQuotes")]
        public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("GetQuotes processed a request.");

            string ticker = req.Query["ticker"];
            string apikey = req.Query["apikey"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            ticker = ticker ?? data?.ticker;
            apikey = apikey ?? data?.apikey;

            string url = $"https://eodhd.com/api/real-time/{ticker}?fmt=json&api_token={apikey}";
            var httpClient = new HttpClient();
            try
            {
                var quoteDataJson = await httpClient.GetStringAsync(url);
                return new OkObjectResult(quoteDataJson);
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex.GetType().Name + ": " + ex.Message);
            }
        }
    }
}
