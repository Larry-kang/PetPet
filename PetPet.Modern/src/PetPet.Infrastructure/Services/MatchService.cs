using Microsoft.EntityFrameworkCore;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;

namespace PetPet.Infrastructure.Services;

public enum FriendshipStatus
{
    None,
    Accepted,
    SentPending,
    ReceivedPending
}

public class MatchCandidateDto
{
    public Guid MemberPublicId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string? MemberPhoto { get; set; }
    public string MemberEmail { get; set; } = string.Empty;
    public string? CityName { get; set; }
    public string? PrimaryPetName { get; set; }
    public string? PrimaryPetPhoto { get; set; }
    public string? PrimaryPetVarietyName { get; set; }
    public int Score { get; set; }
    public List<string> MatchReasons { get; set; } = [];
    public FriendshipStatus FriendshipStatus { get; set; }
    public int? FriendId { get; set; }
}

public class MatchIndexViewModel
{
    public bool IsCurrentUserMatchEnabled { get; set; } = true;
    public bool CurrentUserHasPets { get; set; } = true;
    public List<MatchCandidateDto> Candidates { get; set; } = [];
}

public interface IMatchService
{
    Task<MatchIndexViewModel> GetPlaymateRecommendationsAsync(string currentMemberEmail);
}

public class MatchService : IMatchService
{
    private readonly PetPetDbContext _context;

    public MatchService(PetPetDbContext context)
    {
        _context = context;
    }

    public async Task<MatchIndexViewModel> GetPlaymateRecommendationsAsync(string currentMemberEmail)
    {
        var me = await _context.Members
            .Include(m => m.Pets)
            .FirstOrDefaultAsync(m => m.Email == currentMemberEmail);

        if (me == null)
        {
            return new MatchIndexViewModel
            {
                IsCurrentUserMatchEnabled = false,
                CurrentUserHasPets = false
            };
        }

        var result = new MatchIndexViewModel
        {
            IsCurrentUserMatchEnabled = me.IsMatchEnabled,
            CurrentUserHasPets = me.Pets.Any()
        };

        if (!me.IsMatchEnabled || !me.Pets.Any())
        {
            return result;
        }

        // Exclude blocked members (mutual)
        var blockedEmails = await _context.Blocks.AsNoTracking()
            .Where(b => b.BlockerEmail == me.Email || b.BlockedEmail == me.Email)
            .Select(b => b.BlockerEmail == me.Email ? b.BlockedEmail : b.BlockerEmail)
            .Distinct()
            .ToListAsync();

        var blockedSet = new HashSet<string>(blockedEmails, StringComparer.OrdinalIgnoreCase);
        blockedSet.Add(me.Email);

        // Preload Cities and Varieties
        var cities = await _context.Cities.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name);
        var varieties = await _context.Varieties.AsNoTracking().ToDictionaryAsync(v => v.Id, v => v.Name);

        // Load candidates
        var candidates = await _context.Members.AsNoTracking()
            .Include(m => m.Pets)
            .Where(m => m.IsEnabled && m.IsMatchEnabled && !blockedSet.Contains(m.Email))
            .ToListAsync();

        // Load friendship relationships involving me
        var myFriends = await _context.Friends.AsNoTracking()
            .Where(f => f.MemberAEmail == me.Email || f.MemberBEmail == me.Email || f.RequesterEmail == me.Email || f.AddresseeEmail == me.Email)
            .ToListAsync();

        var myPetVarietyIds = me.Pets
            .Where(p => p.VarietyId.HasValue)
            .Select(p => p.VarietyId!.Value)
            .ToHashSet();

        var dtos = new List<MatchCandidateDto>();

        foreach (var candidate in candidates)
        {
            int score = 0;
            var reasons = new List<string>();

            // 1. Geo Match (+30)
            if (me.CityId.HasValue && candidate.CityId.HasValue && me.CityId.Value == candidate.CityId.Value)
            {
                score += 30;
                string cityName = cities.TryGetValue(me.CityId.Value, out var cName) ? cName : "同縣市";
                reasons.Add($"同在 {cityName} (+30)");
            }

            // 2. Pet Variety Match (+30)
            var matchingPet = candidate.Pets.FirstOrDefault(p => p.VarietyId.HasValue && myPetVarietyIds.Contains(p.VarietyId.Value));
            if (matchingPet != null && matchingPet.VarietyId.HasValue)
            {
                score += 30;
                string varietyName = varieties.TryGetValue(matchingPet.VarietyId.Value, out var vName) ? vName : "同品種";
                reasons.Add($"同為 {varietyName} 毛孩 (+30)");
            }

            // 3. Transparent deterministic exploration factor (0-19)
            string pairKey = string.CompareOrdinal(me.Email, candidate.Email) < 0
                ? $"{me.Email}:{candidate.Email}"
                : $"{candidate.Email}:{me.Email}";
            int bonus = Math.Abs(pairKey.GetHashCode()) % 20;
            score += bonus;
            reasons.Add($"探索加分 (+{bonus})");

            // Friendship status
            var friendRecord = myFriends.FirstOrDefault(f =>
                (f.RequesterEmail == me.Email && f.AddresseeEmail == candidate.Email) ||
                (f.RequesterEmail == candidate.Email && f.AddresseeEmail == me.Email));

            var friendshipStatus = FriendshipStatus.None;
            int? friendId = null;
            if (friendRecord != null)
            {
                friendId = friendRecord.Id;
                if (friendRecord.IsAccepted)
                {
                    friendshipStatus = FriendshipStatus.Accepted;
                }
                else if (friendRecord.RequesterEmail == me.Email)
                {
                    friendshipStatus = FriendshipStatus.SentPending;
                }
                else
                {
                    friendshipStatus = FriendshipStatus.ReceivedPending;
                }
            }

            var primaryPet = candidate.Pets.FirstOrDefault();
            string? petVarietyName = null;
            if (primaryPet?.VarietyId != null && varieties.TryGetValue(primaryPet.VarietyId.Value, out var pvName))
            {
                petVarietyName = pvName;
            }

            dtos.Add(new MatchCandidateDto
            {
                MemberPublicId = candidate.PublicId,
                MemberName = candidate.Name,
                MemberPhoto = candidate.Photo,
                MemberEmail = candidate.Email,
                CityName = candidate.CityId.HasValue && cities.TryGetValue(candidate.CityId.Value, out var cn) ? cn : null,
                PrimaryPetName = primaryPet?.Name,
                PrimaryPetPhoto = primaryPet?.Photo,
                PrimaryPetVarietyName = petVarietyName,
                Score = score,
                MatchReasons = reasons,
                FriendshipStatus = friendshipStatus,
                FriendId = friendId
            });
        }

        result.Candidates = dtos
            .OrderByDescending(d => d.Score)
            .ThenBy(d => d.MemberName)
            .ToList();

        return result;
    }
}
