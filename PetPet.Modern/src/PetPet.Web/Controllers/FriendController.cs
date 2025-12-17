using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetPet.Infrastructure.Data;
using System.Security.Claims;

namespace PetPet.Web.Controllers;

[Authorize]
public class FriendController : Controller
{
    private readonly PetPetDbContext _context;

    public FriendController(PetPetDbContext context)
    {
        _context = context;
    }

    // POST: Friend/Add?targetEmail=...
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(string targetEmail)
    {
        var myEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myEmail) || string.IsNullOrEmpty(targetEmail)) return BadRequest();

        // Check if already friends
        var existing = await _context.Friends.FirstOrDefaultAsync(f => 
            (f.RequesterEmail == myEmail && f.AddresseeEmail == targetEmail) ||
            (f.RequesterEmail == targetEmail && f.AddresseeEmail == myEmail));

        if (existing == null)
        {
            var friendship = new PetPet.Domain.Entities.Friend
            {
                RequesterEmail = myEmail,
                AddresseeEmail = targetEmail,
                IsAccepted = true // Auto-accept for demo simplicity
            };
            _context.Friends.Add(friendship);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    // GET: Friend
    public async Task<IActionResult> Index()
    {
        var myEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(myEmail)) return RedirectToAction("Login", "Member");

        var friendships = await _context.Friends
            .Include(f => f.Requester)
            .Include(f => f.Addressee)
            .Where(f => (f.RequesterEmail == myEmail || f.AddresseeEmail == myEmail) && f.IsAccepted)
            .ToListAsync();

        return View(friendships);
    }

    // POST: Friend/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var myEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        var friend = await _context.Friends.FindAsync(id);
        if (friend == null) return NotFound();

        // Verify ownership
        if (friend.RequesterEmail != myEmail && friend.AddresseeEmail != myEmail) 
        {
            return Forbid();
        }

        _context.Friends.Remove(friend);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
