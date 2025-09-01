using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

class Program
{
    private static readonly HttpClient httpClient = new HttpClient();

    static async Task Main() // Task creation
    {
        List<string> urls = new List<string>() // List of urls
        {
            "https://google.com",
            "https://github.com",
            "https://ilovepdf.com"
        };

        var cts = new CancellationTokenSource(); // instance creation for cancellation token

        Console.WriteLine("Press ENTER anytime to cancel scraping...");
        Task cancellationListener = Task.Run(() =>     // queues the work to run on thread pool
        {
            Console.ReadLine();
            cts.Cancel();
        });


        var results = new ConcurrentBag<string>(); // list of result

        try
        {
            var tasks = new List<Task>(); // List of tasks

            foreach (var url in urls)
            {
                tasks.Add(Task.Run(async () => //adds each task to the list
                {
                    string content = await FetchWithRetryAsync(url, 3, cts.Token);

                    if (!string.IsNullOrEmpty(content))
                    {
                        string extractedData = ExtractWithRetry(content, 3);
                        results.Add($"{url} --> \n{extractedData}");
                    }
                }, cts.Token));
            }

            await Task.WhenAll(tasks); // Run all concurrently
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Scraping was cancelled.");
        }

        Console.WriteLine("\nResults:");
        foreach (var result in results)
        {
            Console.WriteLine(result);
        }
    }

    private static string ExtractWithRetry(string html, int maxRetries)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Example extractions
                var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText ?? "No title";
                var h1 = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText ?? "No h1";
                var firstParagraph = doc.DocumentNode.SelectSingleNode("//p")?.InnerText ?? "No <p>";
                var firstLink = doc.DocumentNode.SelectSingleNode("//a[@href]")?.GetAttributeValue("href", "No link");

                var allH3 = doc.DocumentNode
                    .SelectNodes("//div[contains(@class,'tools__item')]//h3");

                //foreach (var h3 in allH3)
                //{
                //    Console.WriteLine(h3.InnerText);
                //}

                return $"Title: {title}\nH1: {h1}\nParagraph: {firstParagraph}\nLink: {firstLink}\n\n";
            }
            catch when (attempt < maxRetries)
            {
                Console.WriteLine($"Extraction failed, retrying... ({attempt})");
            }
        }

        return "Extraction failed after retries.";
    }

    private static async Task<string> FetchWithRetryAsync(string url, int maxRetries, CancellationToken token)  // Task to fetch the data from url
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var response = await httpClient.GetAsync(url, token);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Request for {url} was cancelled.");
                throw; // rethrow so outer catch can handle
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                Console.WriteLine($"Error fetching {url}, retrying... ({attempt})");
                await Task.Delay(1000, token); // 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to fetch {url}: {ex.Message}");
                break;
            }
        }
        return string.Empty;
    }

}


