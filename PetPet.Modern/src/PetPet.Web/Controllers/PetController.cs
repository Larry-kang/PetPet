using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using System.Security.Claims;

namespace PetPet.Web.Controllers;

[Authorize]
public class PetController : Controller
{
    private readonly PetPetDbContext _context;

    public PetController(PetPetDbContext context)
    {
        _context = context;
    }

    // GET: Pet (My Pets)
    public async Task<IActionResult> Index()
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var pets = await _context.Pets
            .Where(p => p.OwnerEmail == userEmail)
            .OrderByDescending(p => p.Id)
            .ToListAsync();
        
        return View(pets);
    }

    // GET: Pet/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: Pet/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Gender,VarietyId")] Pet pet)
    {
        if (ModelState.IsValid)
        {
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

            pet.OwnerEmail = userEmail;
            pet.Photo = "default_pet.jpg"; // Temporary default
            
            // Hardcode VarietyId for now if not selected (or handle in UI)
            if (pet.VarietyId == 0) pet.VarietyId = 1; 

            _context.Add(pet);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(pet);
    }

    // POST: Pet/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var pet = await _context.Pets.FindAsync(id);
        if (pet == null) return NotFound();

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (pet.OwnerEmail != userEmail) return Forbid();

        _context.Pets.Remove(pet);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
