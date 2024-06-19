using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace api.multifol.io
{
    public class CopyData
    {
        private readonly ILogger<CopyData> _logger;

        public CopyData(ILogger<CopyData> logger)
        {
            _logger = logger;
        }

        private static Dictionary<string, string> DataStorage = new();

        [Function("CopyData")]
        public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequest req)
        {
            _logger.LogInformation("CopyData processed a request.");

            string copyCode = req.Query["copyCode"];
            string profileData = req.Query["profileData"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            copyCode = copyCode ?? data?.copyCode;
            profileData = profileData ?? data?.profileData;

            if (profileData != null)
            {
                bool foundUniqueCopyCode = false;
                string newCopyCode = null;
                int maxCount = 10;
                while (!foundUniqueCopyCode && maxCount > 0)
                {
                    maxCount--;
                    long security = (long)(Random.Shared.NextDouble() * 999999999.0);
                    newCopyCode = security.ToString();

                    if (!DataStorage.ContainsKey(newCopyCode)) {
                        foundUniqueCopyCode = true;
                        DataStorage.Add(newCopyCode, profileData);
                        return new OkObjectResult(newCopyCode);
                    }
                }

                return new BadRequestObjectResult("no security string possible");
            }
            else if (copyCode != null)
            {
                if (DataStorage.ContainsKey(copyCode))
                {
                    var result = DataStorage[copyCode];
                    DataStorage.Remove(copyCode);
                    return new OkObjectResult(result);
                }
                else
                {
                    return new BadRequestObjectResult(null);
                }
            }

            return new BadRequestObjectResult(null);
        }
    }
}
