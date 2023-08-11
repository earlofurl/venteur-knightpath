using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Venteur.KnightPath;

public class KnightPathResult
{
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public int? NumberOfMoves { get; set; }
    public string ShortestPath { get; set; }
    public string Starting { get; set; }
    public string Ending { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}

public static class ProcessKnightPath
{
    [FunctionName("ProcessKnightPath")]
    public static async Task RunAsync(
        [QueueTrigger("knightpath-queue")] string myQueueItem,
        [Table("KnightPathResults")] [StorageAccount("AzureWebJobsStorage")]
        IAsyncCollector<KnightPathResult> table,
        ILogger log)
    {
        log.LogInformation("ProcessKnightPath processing a request: {MyQueueItem}", myQueueItem);

        var job = JsonConvert.DeserializeObject<KnightPathJob>(myQueueItem);
        var source = job.Source;
        var target = job.Target;
        int? result = null;

        try
        {
            // Calculate the shortest path for a chess knight from source to target
            var tempResult = CalculateShortestPath(source, target);
            if (tempResult != -1) // only assign valid results
                result = tempResult;
        }
        catch (Exception ex)
        {
            log.LogError("An error occurred: {Message}", ex.Message);
        }

        // Store the result in Azure Table Storage
        await table.AddAsync(new KnightPathResult
        {
            PartitionKey = "KnightPath",
            RowKey = job.Id,
            NumberOfMoves = result, // this will be null if there is no valid path
            ShortestPath = "",
            Starting = source,
            Ending = target,
            Timestamp = DateTimeOffset.UtcNow
        });

        log.LogInformation("ProcessKnightPath processed a request: {MyQueueItem}", myQueueItem);
    }

    private static int CalculateShortestPath(string source, string target)
    {
        var regex = new Regex("^[a-h][1-8]$");

        if (!regex.IsMatch(source) || !regex.IsMatch(target))
            throw new ArgumentException("Invalid source or target. They must be between a1 and h8 inclusive.");

        // Parse the source and target strings into x and y coordinates
        var sourceX = source[0] - 'a';
        var sourceY = int.Parse(source[1].ToString()) - 1;
        var targetX = target[0] - 'a';
        var targetY = int.Parse(target[1].ToString()) - 1;

        // Define the possible moves for a knight
        int[] row = { 2, 2, -2, -2, 1, 1, -1, -1 };
        int[] col = { -1, 1, 1, -1, 2, -2, 2, -2 };

        // Create a queue for BFS and enqueue the source cell
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((sourceX, sourceY));

        // Create a visited array to keep track of visited cells
        var visited = new bool[8, 8];
        visited[sourceX, sourceY] = true;

        // Create a distance array to keep track of the distance from the source to each cell
        var distance = new int[8, 8];
        distance[sourceX, sourceY] = 0;

        // Perform BFS
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();

            // If we have reached the target cell, return the distance
            if (x == targetX && y == targetY)
                return distance[x, y];

            // Enqueue all valid moves from the current cell
            for (var i = 0; i < row.Length; i++)
            {
                var newX = x + row[i];
                var newY = y + col[i];

                if (newX is < 0 or >= 8 || newY is < 0 or >= 8 || visited[newX, newY]) continue;
                visited[newX, newY] = true;
                distance[newX, newY] = distance[x, y] + 1;
                queue.Enqueue((newX, newY));
            }
        }

        // If we haven't reached the target cell after BFS is complete,
        // it means there is no valid path from the source to the target
        return -1;
    }
}