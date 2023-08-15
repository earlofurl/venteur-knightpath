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

        var (numberOfMoves, shortestPath) = CalculateShortestPath(source, target);

        // Store the result in Azure Table Storage
        await table.AddAsync(new KnightPathResult
        {
            PartitionKey = "KnightPath",
            RowKey = job.Id,
            NumberOfMoves = numberOfMoves == -1 ? null : numberOfMoves,
            ShortestPath = shortestPath,
            Starting = source,
            Ending = target,
            Timestamp = DateTimeOffset.UtcNow
        });

        log.LogInformation("ProcessKnightPath processed a request: {MyQueueItem}", myQueueItem);
    }

    private static (int distance, string path) CalculateShortestPath(string source, string target)
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

        // Prepare the previous cells' coordinates for backtracking
        var previous = new (int x, int y)?[8, 8];

        // Perform BFS
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();

            // If we have reached the target cell, backtrace the path and return distance and path
            if (x == targetX && y == targetY)
            {
                var path = BacktracePath(previous, source, target);
                var dist = distance[x, y];
                return (dist, path);
            }

            // Enqueue all valid moves from the current cell
            for (var i = 0; i < row.Length; i++)
            {
                var newX = x + row[i];
                var newY = y + col[i];

                if (newX is < 0 or >= 8 || newY is < 0 or >= 8 || visited[newX, newY]) continue;
                previous[newX, newY] = (x, y);
                visited[newX, newY] = true;
                distance[newX, newY] = distance[x, y] + 1;
                queue.Enqueue((newX, newY));
            }
        }

        // If there is no valid path from the source to the target
        return (-1, string.Empty);
    }

    private static string BacktracePath((int x, int y)?[,] previous, string source, string target)
    {
        var path = new List<string> { target };
        var targetX = target[0] - 'a';
        var targetY = int.Parse(target[1].ToString()) - 1;
        var current = previous[targetX, targetY];

        while (current.HasValue)
        {
            var (x, y) = current.Value;
            path.Add($"{(char)(x + 'a')}{y + 1}");
            current = previous[x, y];
        }

        path.Reverse(); // initially path is from target to source, we need it to be from source to target
        return string.Join(":", path); // join cells by colon to get the final path string
    }
}