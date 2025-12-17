using System;
using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PetPet.LoadTest;

public class Program
{
    private static readonly HttpClient _client = new HttpClient();
    private const string BaseUrl = "http://localhost:5000";
    
    // Test Configuration
    private const int ConcurrentUsers = 50; 
    private const int TotalSwipesPerUser = 20;

    public static async Task Main(string[] args)
    {
        Console.WriteLine($"🚀 Starting Stress Test: {ConcurrentUsers} Users x {TotalSwipesPerUser} Swipes");
        Console.WriteLine($"Target: {BaseUrl}");

        var stopwatch = Stopwatch.StartNew();
        var successCount = 0;
        var errorCount = 0;
        
        // 1. Create Virtual Users (Login & Get Cookie)
        var users = new ConcurrentBag<VirtualUser>();
        
        Console.WriteLine("🔑 Logging in virtual users...");
        
        // Parallel Login to get Cookies
        await Parallel.ForEachAsync(Enumerable.Range(0, ConcurrentUsers), new ParallelOptions { MaxDegreeOfParallelism = 10 }, async (i, ct) =>
        {
            try 
            {
                // We will use the 'Spy' user for everyone for simplicity, or we can use generated accounts.
                // Using 'spy@test.com' might cause concurrency issues on the SAME user record (DB Lock).
                // Let's create unique users if possible, or just use 'spy' to test Read heavy.
                // BUT 'RecordSwipe' locks DB if Same User swipes? No, Row Level Lock.
                // Re-using 'spy@test.com' simulates ONE user spamming.
                // To simulate MANY users, we should use the seeded users (alice, bob, chen...).
                
                // Let's pick a random seeded user to simulate 'real' traffic distribution
                var seeds = new[] { "alice@test.com", "bob@test.com", "chen@test.com", "lin@test.com", "lee@test.com" };
                var email = seeds[i % seeds.Length]; 
                
                var handler = new HttpClientHandler 
                { 
                    CookieContainer = new CookieContainer(),
                    AllowAutoRedirect = false // Stop redirect to see if we are getting 302
                };
                var client = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };
                
                // Perform Login
                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("Email", email),
                    new KeyValuePair<string, string>("Password", "password")
                });

                var loginRes = await client.PostAsync("/Member/Login", formContent);
                // Login might redirect (302) to Home. This is GOOD.
                if (loginRes.StatusCode == HttpStatusCode.Found || loginRes.IsSuccessStatusCode)
                {
                    users.Add(new VirtualUser { Client = client, Email = email, Handler = handler });
                    Console.Write(".");
                }
                else
                {
                    Console.WriteLine($"\n[Login Fail] {loginRes.StatusCode}");
                }
            }
            catch { Interlocked.Increment(ref errorCount); }
        });
        
        Console.WriteLine($"\n✅ {users.Count} Users Logged In. Starting Attack...");

        // 2. Execute Attack
        await Parallel.ForEachAsync(users, new ParallelOptions { MaxDegreeOfParallelism = ConcurrentUsers }, async (user, ct) =>
        {
            for (int k = 0; k < TotalSwipesPerUser; k++)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var res = await user.Client.GetAsync("/Match/GetCandidates");
                    sw.Stop();
                    
                    if (res.IsSuccessStatusCode)
                    {
                        Interlocked.Increment(ref successCount);
                    }
                    else
                    {
                        Interlocked.Increment(ref errorCount);
                        Console.Write("X");
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref errorCount);
                    Console.WriteLine(ex.Message);
                }
            }
        });

        stopwatch.Stop();
        Console.WriteLine("\n\n📊 Test Finished!");
        Console.WriteLine($"Time: {stopwatch.Elapsed.TotalSeconds:F2} sec");
        Console.WriteLine($"Requests: {successCount + errorCount}");
        Console.WriteLine($"RPS: {(successCount + errorCount) / stopwatch.Elapsed.TotalSeconds:F2}");
        Console.WriteLine($"Success: {successCount}");
        Console.WriteLine($"Errors: {errorCount}");
    }
}

public class VirtualUser
{
    public HttpClient Client { get; set; } = null!;
    public string Email { get; set; } = null!;
    public HttpClientHandler Handler { get; set; } = null!;
}
