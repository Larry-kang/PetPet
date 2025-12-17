using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;

namespace PetPet.Infrastructure.Services
{
    public interface IMatchService
    {
        Task<List<MatchResultDto>> CleanFindMatchesAsync(string currentMemberEmail);
        Task<bool> RecordSwipeAsync(string sourceEmail, string targetEmail, MatchAction action);
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
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly PetPet.Application.Services.ZiweiService _ziweiService;

        public MatchService(PetPetDbContext context, IPublishEndpoint publishEndpoint, PetPet.Application.Services.ZiweiService ziweiService)
        {
            _context = context;
            _publishEndpoint = publishEndpoint;
            _ziweiService = ziweiService;
        }

        public async Task<bool> RecordSwipeAsync(string sourceEmail, string targetEmail, MatchAction action)
        {
             // 1. Record the action
             var interaction = new MatchInteraction
             {
                 SourceMemberId = sourceEmail,
                 TargetMemberId = targetEmail,
                 Action = action
             };
             _context.MatchInteractions.Add(interaction);

             // 2. Double Opt-in Check (Only if I LIKED them)
             bool isMatch = false;
             if (action == MatchAction.Like)
             {
                 // Check if they liked me before
                 var theyLikedMe = await _context.MatchInteractions
                     .AnyAsync(m => m.SourceMemberId == targetEmail && 
                                    m.TargetMemberId == sourceEmail && 
                                    m.Action == MatchAction.Like);
                 
                 if (theyLikedMe)
                 {
                     isMatch = true;
                     // Create Friendship efficiently
                     if (!await _context.Friends.AnyAsync(f => f.RequesterEmail == sourceEmail && f.AddresseeEmail == targetEmail))
                     {
                         _context.Friends.Add(new Friend 
                         { 
                             RequesterEmail = sourceEmail, 
                             AddresseeEmail = targetEmail, 
                             IsAccepted = true 
                         });
                         
                         // Publish Event (Async, Decoupled)
                         await _publishEndpoint.Publish(new PetPet.Domain.Events.MatchSuccessEvent 
                         { 
                             SourceEmail = sourceEmail, 
                             TargetEmail = targetEmail 
                         });
                     }
                 }
             }

             await _context.SaveChangesAsync();
             return isMatch;
        }

        public async Task<List<MatchResultDto>> CleanFindMatchesAsync(string currentMemberEmail)
        {
            // 1. Load my info
            var me = await _context.Members
                .Include(m => m.Pets)
                .FirstOrDefaultAsync(m => m.Email == currentMemberEmail);

            if (me == null) return new List<MatchResultDto>();

            // 2. Get Exclusions (Who did I already swipe?)
            var swipedIds = await _context.MatchInteractions
                .Where(m => m.SourceMemberId == currentMemberEmail)
                .Select(m => m.TargetMemberId)
                .ToListAsync();

            // 3. Load candidates (Exclude self + swiped + disabled)
            var candidates = await _context.Members
                .Include(m => m.Pets)
                .Where(m => m.Email != currentMemberEmail 
                            && m.IsEnabled
                            && !swipedIds.Contains(m.Email))
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

                // B. Pet Match
                var myPet = me.Pets.FirstOrDefault();
                var theirPet = candidate.Pets.FirstOrDefault();

                if (myPet != null && theirPet != null)
                {
                    if (myPet.VarietyId == theirPet.VarietyId) 
                    {
                        score += 30;
                        reasons.Add("同品種 (+30)");
                    }
                }

                // C. Ziwei Compatibility (New!)
                // Simulate Main Star for both (In real app, Member would have ZiweiStar property cached)
                // Use consistent hash for demo stability
                var myStar = _ziweiService.CalculateStar(new DateTime(1990, (Math.Abs(currentMemberEmail.GetHashCode()) % 12) + 1, 15));
                var theirStar = _ziweiService.CalculateStar(new DateTime(1995, (Math.Abs(candidate.Email.GetHashCode()) % 12) + 1, (Math.Abs(candidate.Email.GetHashCode()) % 28) + 1));
                
                var ziweiScore = _ziweiService.CalculateMatchScore(myStar, theirStar);
                if (ziweiScore >= 85)
                {
                    score += 25; // Significant boost for destiny match
                    reasons.Add($"命中註定 ({myStar} & {theirStar})");
                }
                else if (ziweiScore >= 75)
                {
                    score += 10;
                }

                // D. Random Factor (for fun)
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
            return results.OrderByDescending(r => r.Score).Take(20).ToList(); // Increased limit
        }
    }
}
