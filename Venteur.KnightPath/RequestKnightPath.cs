using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Venteur.KnightPath;

public static class RequestKnightPath
{
    [FunctionName("RequestKnightPath")]
    public static async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "knightpath")]
        HttpRequest req, [Queue("knightpath-queue")] [StorageAccount("AzureWebJobsStorage")] ICollector<string> msg,
        ILogger log)
    {
        log.LogInformation("RequestKnightPath processed a request");

        string source = req.Query["source"];
        string target = req.Query["target"];

        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        dynamic data = JsonConvert.DeserializeObject(requestBody);
        source = source ?? data?.source;
        target = target ?? data?.target;

        if (source == null || target == null)
            return new BadRequestObjectResult(
                "Please pass a source and target on the query string or in the request body");

        msg.Add($"{source},{target}");
        return new OkObjectResult($"Source: {source}, Target: {target}");
    }
}