using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;

class Program
{
    // Path for CardImages folder on Desktop
    private static readonly string DesktopFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    private static readonly string ImageFolder = Path.Combine(DesktopFolder, "CardImages");

    static async Task Main(string[] args)
    {
        // Ensure the CardImages folder exists on the Desktop
        Directory.CreateDirectory(ImageFolder);

        Console.WriteLine("Starting download of card images...");

        // Create a cancellation token to support canceling the download
        var cts = new CancellationTokenSource();
        CancellationToken token = cts.Token;

        await DownloadCardImages(cts, token);

        Console.WriteLine("Download complete. Press any key to exit.");
        Console.ReadKey();
    }

    static async Task DownloadCardImages(CancellationTokenSource cts, CancellationToken token)
    {
        string apiUrl = "https://db.ygoprodeck.com/api/v7/cardinfo.php";

        using (HttpClient client = new HttpClient())
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl, token);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Failed to fetch card data from API.");
                    return;
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();
                JArray cardDataList = JArray.Parse(JObject.Parse(jsonResponse)["data"].ToString());

                Console.WriteLine($"Fetched {cardDataList.Count} cards.");

                int batchSize = 20;  // Limiting to 20 cards per batch
                int processedCards = 0;

                while (processedCards < cardDataList.Count)
                {
                    for (int i = 0; i < batchSize && processedCards < cardDataList.Count; i++)
                    {
                        if (token.IsCancellationRequested)
                        {
                            Console.WriteLine("Cancellation requested. Exiting...");
                            return;
                        }

                        string cardId = cardDataList[processedCards]["id"].ToString();
                        string imageUrl = cardDataList[processedCards]["card_images"][0]["image_url"].ToString();

                        string imageFilePath = Path.Combine(ImageFolder, $"{cardId}.jpg");

                        // Check if the image file already exists
                        if (File.Exists(imageFilePath))
                        {
                            // Delete the existing image before downloading the new one
                            File.Delete(imageFilePath);
                        }

                        byte[] imageBytes = await client.GetByteArrayAsync(imageUrl);
                        await File.WriteAllBytesAsync(imageFilePath, imageBytes);
                        Console.WriteLine($"Downloaded image for Card ID: {cardId}");

                        processedCards++;
                    }

                    // Wait for 1 second to respect API rate limit (20 calls per second)
                    Console.WriteLine("Waiting 1 second to respect API rate limit...");
                    await Task.Delay(1000);  // Delay 1 second between batches of 20 cards
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
