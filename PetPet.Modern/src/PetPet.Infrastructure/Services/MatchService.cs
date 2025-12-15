using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;

namespace PetPet.Infrastructure.Services
{
    public interface IMatchService
    {
        Task<List<MatchResultDto>> CleanFindMatchesAsync(string currentMemberEmail);
    }

    public class MatchResultDto
    {
        public Member TargetMember { get; set; } = null!;
        public Pet? PrimaryPet { get; set; }
        public int Score { get; set; }
        public List<string> MatchReasons { get; set; } = new List<string>();
    }

    public class MatchService : IMatchService
    {
        private readonly PetPetDbContext _context;

        public MatchService(PetPetDbContext context)
        {
            _context = context;
        }

        public async Task<List<MatchResultDto>> CleanFindMatchesAsync(string currentMemberEmail)
        {
            // 1. Load current user info
            var me = await _context.Members
                .Include(m => m.Pets)
                .FirstOrDefaultAsync(m => m.Email == currentMemberEmail);

            if (me == null) return new List<MatchResultDto>();

            // 2. Load candidates (Exclude self)
            var candidates = await _context.Members
                .Include(m => m.Pets)
                .Where(m => m.Email != currentMemberEmail && m.IsEnabled)
                .ToListAsync();

            var results = new List<MatchResultDto>();

            foreach (var candidate in candidates)
            {
                int score = 0;
                var reasons = new List<string>();

                // A. Geo Match
                if (candidate.CityId == me.CityId)
                {
                    score += 30;
                    reasons.Add("同城 (+30)");
                }

                // B. Pet Match (Compare primary pets)
                var myPet = me.Pets.FirstOrDefault();
                var theirPet = candidate.Pets.FirstOrDefault();

                if (myPet != null && theirPet != null)
                {
                    if (myPet.VarietyId == theirPet.VarietyId) // Assuming VarietyId similarity
                    {
                        score += 30;
                        reasons.Add("同品種 (+30)");
                    }
                    
                    // Simple Age Check
                    // ... 
                }

                // C. Random Factor (Destiny)
                var randomBonus = new Random().Next(0, 20);
                score += randomBonus;

                results.Add(new MatchResultDto
                {
                    TargetMember = candidate,
                    PrimaryPet = theirPet,
                    Score = score,
                    MatchReasons = reasons
                });
            }

            // 3. Sort by Score DESC
            return results.OrderByDescending(r => r.Score).ToList();
        }
    }
}
