using System.Security.Cryptography;
using System.Text;
using PetPet.Domain.Entities;

namespace PetPet.Infrastructure.Demo;

public class DemoSeedData
{
    public List<City> Cities { get; set; } = [];
    public List<Variety> Varieties { get; set; } = [];
    public List<Member> Members { get; set; } = [];
    public List<Pet> Pets { get; set; } = [];
    public List<Post> Posts { get; set; } = [];
    public List<Comment> Comments { get; set; } = [];
    public List<Like> Likes { get; set; } = [];
    public List<Friend> Friends { get; set; } = [];
    public string BusinessFingerprint { get; set; } = string.Empty;
}

public static class DemoDataGenerator
{
    public static readonly (int Id, string Name, string Code)[] TaiwanCities =
    [
        (1, "臺北市", "TPE"),
        (2, "新北市", "NTP"),
        (3, "桃園市", "TYN"),
        (4, "臺中市", "TXG"),
        (5, "臺南市", "TNN"),
        (6, "高雄市", "KHH"),
        (7, "基隆市", "KEE"),
        (8, "新竹市", "HSZ"),
        (9, "嘉義市", "CYI"),
        (10, "新竹縣", "HSQ"),
        (11, "苗栗縣", "MIA"),
        (12, "彰化縣", "CHA"),
        (13, "南投縣", "NAN"),
        (14, "雲林縣", "YUN"),
        (15, "嘉義縣", "CYQ"),
        (16, "屏東縣", "PIF"),
        (17, "宜蘭縣", "ILA"),
        (18, "花蓮縣", "HUA"),
        (19, "臺東縣", "TTT"),
        (20, "澎湖縣", "PEN"),
        (21, "金門縣", "KIN"),
        (22, "連江縣", "LIE")
    ];

    public static Guid DeterministicGuid(long seed, string entityType, int index)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes($"{seed}:{entityType}:{index}");
        var hash = sha.ComputeHash(bytes);
        var guidBytes = new byte[16];
        Array.Copy(hash, 0, guidBytes, 0, 16);
        return new Guid(guidBytes);
    }

    public static DemoSeedData Generate(long seed = 20261013, DateTime? baseTime = null, int generatorVersion = 1)
    {
        var baseTimeUtc = baseTime ?? new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        if (baseTimeUtc > DateTime.UtcNow)
        {
            throw new ArgumentException("BaseTimeUtc cannot be in the future.");
        }

        var rng = new Random((int)(seed & 0x7FFFFFFF));
        var result = new DemoSeedData();

        // 1. Catalogs
        foreach (var c in TaiwanCities)
        {
            result.Cities.Add(new City { Id = c.Id, Name = c.Name, Code = c.Code });
        }
        result.Varieties.AddRange([
            new Variety { Id = 1, Name = "狗", KindCode = "Dog" },
            new Variety { Id = 2, Name = "貓", KindCode = "Cat" },
            new Variety { Id = 3, Name = "其他", KindCode = "Other" }
        ]);

        // 2. Members (120 Total)
        // Fixed accounts
        var admin = new Member
        {
            Email = "admin@example.test",
            NormalizedEmail = "ADMIN@EXAMPLE.TEST",
            PublicId = DeterministicGuid(seed, "Member", 0),
            Name = "系統管理員",
            Phone = "",
            CityId = 1,
            Birthday = baseTimeUtc.AddYears(-30),
            Gender = true,
            Photo = "/images/presets/avatar-boy.svg",
            IsEnabled = true,
            IsAdmin = true,
            IsMatchEnabled = false,
            Bio = "PetPet 平台管理員"
        };
        result.Members.Add(admin);

        var disabled = new Member
        {
            Email = "disabled@example.test",
            NormalizedEmail = "DISABLED@EXAMPLE.TEST",
            PublicId = DeterministicGuid(seed, "Member", 1),
            Name = "停權會員",
            Phone = "",
            CityId = 1,
            Birthday = baseTimeUtc.AddYears(-25),
            Gender = false,
            Photo = "/images/presets/avatar-girl.svg",
            IsEnabled = false,
            IsAdmin = false,
            IsMatchEnabled = false,
            Bio = "因違規已被停權的示範帳號"
        };
        result.Members.Add(disabled);

        var owner = new Member
        {
            Email = "demo-owner@example.test",
            NormalizedEmail = "DEMO-OWNER@EXAMPLE.TEST",
            PublicId = DeterministicGuid(seed, "Member", 2),
            Name = "展示飼主 (Owner)",
            Phone = "",
            CityId = 1, // Taipei
            Birthday = baseTimeUtc.AddYears(-26),
            Gender = false,
            Photo = "/images/presets/avatar-girl.svg",
            IsEnabled = true,
            IsAdmin = false,
            IsMatchEnabled = true,
            Bio = "喜愛寵物的展示飼主，家裡有4隻毛孩！"
        };
        result.Members.Add(owner);

        var nopet = new Member
        {
            Email = "demo-nopet@example.test",
            NormalizedEmail = "DEMO-NOPET@EXAMPLE.TEST",
            PublicId = DeterministicGuid(seed, "Member", 3),
            Name = "無寵物會員",
            Phone = "",
            CityId = 2, // New Taipei
            Birthday = baseTimeUtc.AddYears(-24),
            Gender = true,
            Photo = "/images/presets/avatar-boy.svg",
            IsEnabled = true,
            IsAdmin = false,
            IsMatchEnabled = false,
            Bio = "準備領養第一隻毛孩的愛心會員"
        };
        result.Members.Add(nopet);

        var friend = new Member
        {
            Email = "demo-friend@example.test",
            NormalizedEmail = "DEMO-FRIEND@EXAMPLE.TEST",
            PublicId = DeterministicGuid(seed, "Member", 4),
            Name = "展示好友 (Friend)",
            Phone = "",
            CityId = 1,
            Birthday = baseTimeUtc.AddYears(-28),
            Gender = true,
            Photo = "/images/presets/avatar-boy.svg",
            IsEnabled = true,
            IsAdmin = false,
            IsMatchEnabled = true,
            Bio = "展示飼主的好朋友，經常一起遛狗"
        };
        result.Members.Add(friend);

        // 115 Synthetic enabled members
        for (int i = 5; i < 120; i++)
        {
            var email = $"user{i - 4:D3}@example.test";
            var surname = DemoTemplates.Surnames[i % DemoTemplates.Surnames.Length];
            var given = DemoTemplates.GivenNames[(i * 7) % DemoTemplates.GivenNames.Length];
            var isMale = i % 2 == 0;
            var cityId = 1 + (i % 22);

            result.Members.Add(new Member
            {
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                PublicId = DeterministicGuid(seed, "Member", i),
                Name = $"{surname}{given}",
                Phone = "",
                CityId = cityId,
                Birthday = baseTimeUtc.AddYears(-20 - (i % 20)),
                Gender = isMale,
                Photo = isMale ? "/images/presets/avatar-boy.svg" : "/images/presets/avatar-girl.svg",
                IsEnabled = true,
                IsAdmin = false,
                IsMatchEnabled = (i % 3 != 0),
                Bio = $"你好！我是住在{TaiwanCities[cityId - 1].Name}的毛孩家長。"
            });
        }

        // 3. Pet Distribution (240 Pets total across 120 members)
        // Group 0: 24 members with 0 pets
        // Group 1: 24 members with 1 pet
        // Group 2: 24 members with 2 pets
        // Group 3: 24 members with 3 pets
        // Group 4: 24 members with 4 pets
        // Total = 24*0 + 24*1 + 24*2 + 24*3 + 24*4 = 240 pets.

        var group0 = new List<Member>(); // 0 pets (24 members)
        var group1 = new List<Member>(); // 1 pet  (24 members)
        var group2 = new List<Member>(); // 2 pets (24 members)
        var group3 = new List<Member>(); // 3 pets (24 members)
        var group4 = new List<Member>(); // 4 pets (24 members)

        // Assign fixed personas:
        // admin (index 0) -> group0
        // disabled (index 1) -> group1
        // owner (index 2) -> group4
        // nopet (index 3) -> group0
        // friend (index 4) -> group2
        group0.Add(admin);
        group0.Add(nopet);
        group1.Add(disabled);
        group2.Add(friend);
        group4.Add(owner);

        // Distribute remaining 115 members (indices 5 to 119)
        for (int i = 5; i < 120; i++)
        {
            var m = result.Members[i];
            if (group0.Count < 24) group0.Add(m);
            else if (group1.Count < 24) group1.Add(m);
            else if (group2.Count < 24) group2.Add(m);
            else if (group3.Count < 24) group3.Add(m);
            else if (group4.Count < 24) group4.Add(m);
        }

        int petIndex = 0;
        void AddPetsFor(Member m, int count)
        {
            for (int p = 0; p < count; p++)
            {
                petIndex++;
                bool isDog = (petIndex % 2 == 0);
                string kindCode = isDog ? "Dog" : "Cat";
                int varietyId = isDog ? 1 : 2;
                string petName = isDog
                    ? DemoTemplates.DogNames[(petIndex + p) % DemoTemplates.DogNames.Length]
                    : DemoTemplates.CatNames[(petIndex + p) % DemoTemplates.CatNames.Length];
                string photo = isDog ? "/images/presets/avatar-dog.svg" : "/images/presets/avatar-cat.svg";

                result.Pets.Add(new Pet
                {
                    Id = petIndex,
                    OwnerEmail = m.Email,
                    Name = petName,
                    KindCode = kindCode,
                    VarietyId = varietyId,
                    Gender = (p % 2 == 0),
                    BirthDate = baseTimeUtc.AddYears(-1 - (p % 6)).AddMonths(-p * 2),
                    Bio = $"{m.Name} 家裡可愛的{petName}，個性活潑討喜！",
                    IsPublic = true,
                    Photo = photo,
                    Version = DeterministicGuid(seed, "PetVersion", petIndex)
                });
            }
        }

        foreach (var m in group0) AddPetsFor(m, 0);
        foreach (var m in group1) AddPetsFor(m, 1);
        foreach (var m in group2) AddPetsFor(m, 2);
        foreach (var m in group3) AddPetsFor(m, 3);
        foreach (var m in group4) AddPetsFor(m, 4);

        // 4. Posts (1,200 Total: 20 hidden, 1,180 visible)
        // Eligible authors: Enabled, non-admin members (118 members)
        var eligibleAuthors = result.Members.Where(m => m.IsEnabled && !m.IsAdmin).ToList();

        int postId = 0;
        // First, ensure each of the 118 eligible members has at least 1 post
        for (int i = 0; i < eligibleAuthors.Count; i++)
        {
            postId++;
            var author = eligibleAuthors[i];
            var created = baseTimeUtc.AddDays(-89 + (i % 80)).AddHours(i % 24);
            var title = DemoTemplates.PostTitles[i % DemoTemplates.PostTitles.Length];
            var body = DemoTemplates.PostBodies[i % DemoTemplates.PostBodies.Length];
            var img = (i % 2 == 0) ? "/images/presets/avatar-dog.svg" : "/images/presets/avatar-cat.svg";

            result.Posts.Add(new Post
            {
                Id = postId,
                AuthorEmail = author.Email,
                Title = title,
                Content = body,
                ImageUrl = img,
                CreatedAt = created,
                IsEnabled = true,
                IsDeleted = false,
                Version = DeterministicGuid(seed, "PostVersion", postId)
            });
        }

        // Fill remaining posts up to 1,200
        while (postId < 1200)
        {
            postId++;
            var author = eligibleAuthors[(postId * 13) % eligibleAuthors.Count];
            var dayOffset = (postId * 7) % 89;
            var created = baseTimeUtc.AddDays(-89 + dayOffset).AddMinutes((postId * 17) % 1440);
            var title = DemoTemplates.PostTitles[(postId + 5) % DemoTemplates.PostTitles.Length];
            var body = DemoTemplates.PostBodies[(postId + 7) % DemoTemplates.PostBodies.Length];
            var img = (postId % 3 == 0) ? null : ((postId % 2 == 0) ? "/images/presets/avatar-dog.svg" : "/images/presets/avatar-cat.svg");

            result.Posts.Add(new Post
            {
                Id = postId,
                AuthorEmail = author.Email,
                Title = title,
                Content = body,
                ImageUrl = img,
                CreatedAt = created,
                IsEnabled = true,
                IsDeleted = false,
                Version = DeterministicGuid(seed, "PostVersion", postId)
            });
        }

        // Set exactly 20 posts as hidden (IsEnabled = false), e.g. posts 1181 to 1200
        for (int i = 1180; i < 1200; i++)
        {
            result.Posts[i].IsEnabled = false;
        }

        // Special boundary requirements:
        // Post 1 (owner's post): 20 likes
        // Post 2: 0 likes
        // Post 3: 0 comments
        // Post 4: >= 30 comments (we will give 35 comments)

        // 5. Comments (2,400 Total)
        int commentId = 0;

        // Post 4 gets 35 comments (boundary case >= 30)
        var post4 = result.Posts[3]; // Id = 4
        for (int c = 0; c < 35; c++)
        {
            commentId++;
            var commentAuthor = eligibleAuthors[(c * 3) % eligibleAuthors.Count];
            var commentTime = post4.CreatedAt.AddMinutes(5 + c * 10);
            if (commentTime > baseTimeUtc) commentTime = baseTimeUtc;

            result.Comments.Add(new Comment
            {
                Id = commentId,
                PostId = post4.Id,
                UserEmail = commentAuthor.Email,
                Content = DemoTemplates.CommentTemplates[c % DemoTemplates.CommentTemplates.Length],
                CreatedAt = commentTime,
                IsDeleted = false,
                Version = DeterministicGuid(seed, "CommentVersion", commentId)
            });
        }

        // Post 3 (Id = 3) gets 0 comments!
        // Remaining comments (2400 - 35 = 2365 comments) distributed across other posts (excluding Post 3)
        var commentablePosts = result.Posts.Where(p => p.Id != 3 && p.Id != 4).ToList();
        int targetRemaining = 2400 - 35;
        for (int c = 0; c < targetRemaining; c++)
        {
            commentId++;
            var targetPost = commentablePosts[c % commentablePosts.Count];
            var commentAuthor = eligibleAuthors[(c * 11) % eligibleAuthors.Count];
            var commentTime = targetPost.CreatedAt.AddMinutes(10 + (c % 120));
            if (commentTime > baseTimeUtc) commentTime = baseTimeUtc;

            result.Comments.Add(new Comment
            {
                Id = commentId,
                PostId = targetPost.Id,
                UserEmail = commentAuthor.Email,
                Content = DemoTemplates.CommentTemplates[(c + 3) % DemoTemplates.CommentTemplates.Length],
                CreatedAt = commentTime,
                IsDeleted = false,
                Version = DeterministicGuid(seed, "CommentVersion", commentId)
            });
        }

        // 6. Likes (Each post 0 to 20 likes, Post 1 has 20, Post 2 has 0)
        int likeId = 0;
        foreach (var post in result.Posts)
        {
            int likeCount;
            if (post.Id == 1)
            {
                likeCount = 20; // Boundary: 20 likes
            }
            else if (post.Id == 2)
            {
                likeCount = 0;  // Boundary: 0 likes
            }
            else
            {
                likeCount = (post.Id * 7) % 21; // 0 to 20 likes
            }

            for (int k = 0; k < likeCount; k++)
            {
                likeId++;
                var liker = eligibleAuthors[(post.Id * 5 + k * 3) % eligibleAuthors.Count];
                result.Likes.Add(new Like
                {
                    Id = likeId,
                    PostId = post.Id,
                    UserEmail = liker.Email,
                    CreatedAt = post.CreatedAt.AddMinutes(2 + k * 5)
                });
            }
        }

        // 7. Friends (Exactly 300 unique pairs: 180 accepted, 120 pending)
        // Must guarantee:
        // - demo-owner has incoming pending, outgoing pending to demo-friend, and accepted friends
        int friendId = 0;
        var pairSet = new HashSet<string>(StringComparer.Ordinal);

        void AddFriend(Member requester, Member addressee, bool isAccepted)
        {
            if (requester.Email == addressee.Email) return;

            string memberA = string.CompareOrdinal(requester.Email, addressee.Email) < 0 ? requester.Email : addressee.Email;
            string memberB = string.CompareOrdinal(requester.Email, addressee.Email) < 0 ? addressee.Email : requester.Email;
            string pairKey = $"{memberA}:{memberB}";

            if (pairSet.Contains(pairKey)) return;
            pairSet.Add(pairKey);

            friendId++;
            var created = baseTimeUtc.AddDays(-30 + (friendId % 25));
            result.Friends.Add(new Friend
            {
                Id = friendId,
                RequesterEmail = requester.Email,
                AddresseeEmail = addressee.Email,
                MemberAEmail = memberA,
                MemberBEmail = memberB,
                IsAccepted = isAccepted,
                CreatedAt = created,
                AcceptedAtUtc = isAccepted ? created.AddHours(2) : null,
                Version = DeterministicGuid(seed, "FriendVersion", friendId)
            });
        }

        // Guarantee demo-owner specific cases:
        // 1) Outgoing pending: demo-owner -> demo-friend
        AddFriend(owner, friend, false);

        // 2) Incoming pending: user001 -> demo-owner
        var user001 = result.Members.First(m => m.Email == "user001@example.test");
        AddFriend(user001, owner, false);

        // 3) Accepted friend: demo-owner <-> user002
        var user002 = result.Members.First(m => m.Email == "user002@example.test");
        AddFriend(owner, user002, true);

        // 4) Accepted friend: user003 <-> demo-owner
        var user003 = result.Members.First(m => m.Email == "user003@example.test");
        AddFriend(user003, owner, true);

        // Candidate pairs from all eligible members
        var allPairs = new List<(Member M1, Member M2)>();
        for (int i = 0; i < eligibleAuthors.Count; i++)
        {
            for (int j = i + 1; j < eligibleAuthors.Count; j++)
            {
                allPairs.Add((eligibleAuthors[i], eligibleAuthors[j]));
            }
        }

        // Deterministic Fisher-Yates shuffle using seeded rng
        for (int i = allPairs.Count - 1; i > 0; i--)
        {
            int k = rng.Next(i + 1);
            (allPairs[i], allPairs[k]) = (allPairs[k], allPairs[i]);
        }

        // Fill remaining accepted friends up to 180
        int pairIdx = 0;
        while (result.Friends.Count(f => f.IsAccepted) < 180 && pairIdx < allPairs.Count)
        {
            var p = allPairs[pairIdx++];
            bool flip = (rng.Next(2) == 0);
            AddFriend(flip ? p.M2 : p.M1, flip ? p.M1 : p.M2, true);
        }

        // Fill remaining pending friends up to 120 (total 300)
        while (result.Friends.Count(f => !f.IsAccepted) < 120 && pairIdx < allPairs.Count)
        {
            var p = allPairs[pairIdx++];
            bool flip = (rng.Next(2) == 0);
            AddFriend(flip ? p.M2 : p.M1, flip ? p.M1 : p.M2, false);
        }

        // 8. Compute Deterministic Business Fingerprint
        using var fpSha = SHA256.Create();
        var fpBuilder = new StringBuilder();
        fpBuilder.Append($"{seed}|{generatorVersion}|{baseTimeUtc:O}|");
        fpBuilder.Append($"{result.Members.Count}|{result.Pets.Count}|{result.Posts.Count}|{result.Comments.Count}|{result.Likes.Count}|{result.Friends.Count}|");
        foreach (var m in result.Members.Take(5)) fpBuilder.Append($"{m.Email}:{m.Name};");
        foreach (var p in result.Pets.Take(5)) fpBuilder.Append($"{p.Name}:{p.OwnerEmail};");
        foreach (var post in result.Posts.Take(5)) fpBuilder.Append($"{post.Id}:{post.Title};");
        var fpHash = fpSha.ComputeHash(Encoding.UTF8.GetBytes(fpBuilder.ToString()));
        result.BusinessFingerprint = Convert.ToHexString(fpHash).ToLowerInvariant();

        return result;
    }
}
