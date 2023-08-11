using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Venteur.KnightPath;

public class KnightPathResponse
{
    public KnightPathResponse(KnightPathResult result)
    {
        OperationId = result.RowKey;
        Starting = result.Starting;
        Ending = result.Ending;
        ShortestPath = result.ShortestPath;
        NumberOfMoves =
            result.NumberOfMoves ??
            0; // Coalesce to avoid nulls
    }

    public string ShortestPath { get; set; }
    public int NumberOfMoves { get; set; }
    public string Starting { get; set; }
    public string Ending { get; set; }
    public string OperationId { get; set; }
}

public static class ReceiveKnightPath
{
    [FunctionName("ReceiveKnightPath")]
    public static async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "knightpath")]
        HttpRequest req,
        [Table("KnightPathResults", "KnightPath", "{Query.operationId}")] [StorageAccount("AzureWebJobsStorage")]
        KnightPathResult pathResult,
        ILogger log)
    {
        log.LogInformation("ReceiveKnightPath processing a request.");

        string operationId = req.Query["operationId"];

        if (string.IsNullOrEmpty(operationId))
            return new BadRequestObjectResult("Please pass a operationId on the query string");

        if (pathResult == null)
            return new NotFoundObjectResult($"Operation Id {operationId} was not found.");

        var response = new KnightPathResponse(pathResult);

        return new OkObjectResult(response);
    }
}