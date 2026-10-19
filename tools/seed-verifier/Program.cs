using System.Text.Json;
using Microsoft.Data.Sqlite;

if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Error: Database path argument required.");
    return 1;
}

string dbPath = Path.GetFullPath(args[0]);
if (!File.Exists(dbPath))
{
    Console.Error.WriteLine($"Error: Database file not found at '{dbPath}'.");
    return 2;
}

try
{
    using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    connection.Open();

    int QueryInt(string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        var result = cmd.ExecuteScalar();
        return Convert.ToInt32(result);
    }

    // 1. Members
    int memberCount = QueryInt("SELECT COUNT(*) FROM [Member];");
    int enabledMembers = QueryInt("SELECT COUNT(*) FROM [Member] WHERE IsEnabled = 1;");
    int adminMembers = QueryInt("SELECT COUNT(*) FROM [Member] WHERE IsAdmin = 1;");

    // 2. Pets and distribution per member
    int petCount = QueryInt("SELECT COUNT(*) FROM [Pet];");
    var petsPerMember = new Dictionary<string, int>();
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = @"
            SELECT m.Email, COUNT(p.Pet_no) AS PetCount
            FROM [Member] m
            LEFT JOIN [Pet] p ON m.Email = p.Email
            GROUP BY m.Email;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            petsPerMember[reader.GetString(0)] = reader.GetInt32(1);
        }
    }

    int minPetsPerMember = petsPerMember.Values.Min();
    int maxPetsPerMember = petsPerMember.Values.Max();
    var petDistribution = new Dictionary<int, int>();
    for (int i = 0; i <= 4; i++) petDistribution[i] = 0;
    foreach (var count in petsPerMember.Values)
    {
        if (count >= 0 && count <= 4)
        {
            petDistribution[count]++;
        }
        else
        {
            petDistribution[count] = petDistribution.GetValueOrDefault(count, 0) + 1;
        }
    }

    // 3. Posts & Comments
    int postCount = QueryInt("SELECT COUNT(*) FROM [Post];");
    int enabledPosts = QueryInt("SELECT COUNT(*) FROM [Post] WHERE Post_Enable = 1;");
    int disabledPosts = QueryInt("SELECT COUNT(*) FROM [Post] WHERE Post_Enable = 0;");
    int commentCount = QueryInt("SELECT COUNT(*) FROM [Comment];");

    // 4. Likes
    int likeCount = QueryInt("SELECT COUNT(*) FROM [Like];");

    // 5. Friends
    int friendCount = QueryInt("SELECT COUNT(*) FROM [Friend];");
    int acceptedFriends = QueryInt("SELECT COUNT(*) FROM [Friend] WHERE IsAccepted = 1;");
    int pendingFriends = QueryInt("SELECT COUNT(*) FROM [Friend] WHERE IsAccepted = 0;");

    // 6. Seed Record
    string? seedFingerprint = null;
    long? seedValue = null;
    int? generatorVersion = null;
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "SELECT BusinessFingerprint, Seed, GeneratorVersion FROM [SeedRecord] ORDER BY Id DESC LIMIT 1;";
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            seedFingerprint = reader.IsDBNull(0) ? null : reader.GetString(0);
            seedValue = reader.IsDBNull(1) ? null : reader.GetInt64(1);
            generatorVersion = reader.IsDBNull(2) ? null : reader.GetInt32(2);
        }
    }

    // Spec assertions
    bool meetsMemberCount = memberCount >= 100;
    bool meetsPetRange = minPetsPerMember >= 0 && maxPetsPerMember <= 4;
    bool meetsPostsCount = postCount >= 1000; // Spec requires 1200
    bool meetsCommentsCount = commentCount >= 2000; // Spec requires 2400
    bool validSeed = !string.IsNullOrEmpty(seedFingerprint);

    bool allPassed = meetsMemberCount && meetsPetRange && meetsPostsCount && meetsCommentsCount && validSeed;

    var report = new
    {
        verifiedAtUtc = DateTime.UtcNow.ToString("o"),
        dbPath,
        passed = allPassed,
        statistics = new
        {
            members = new
            {
                total = memberCount,
                enabled = enabledMembers,
                admin = adminMembers,
                meetsSpec = meetsMemberCount
            },
            pets = new
            {
                total = petCount,
                minPerMember = minPetsPerMember,
                maxPerMember = maxPetsPerMember,
                distribution = petDistribution,
                meetsSpec = meetsPetRange
            },
            posts = new
            {
                total = postCount,
                enabled = enabledPosts,
                disabled = disabledPosts,
                meetsSpec = meetsPostsCount
            },
            comments = new
            {
                total = commentCount,
                meetsSpec = meetsCommentsCount
            },
            likes = new
            {
                total = likeCount
            },
            friends = new
            {
                total = friendCount,
                accepted = acceptedFriends,
                pending = pendingFriends
            },
            seedRecord = new
            {
                fingerprint = seedFingerprint,
                seed = seedValue,
                generatorVersion,
                hasRecord = validSeed
            }
        }
    };

    var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    Console.WriteLine(json);

    return allPassed ? 0 : 1;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Verification failed with exception: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    return 3;
}
