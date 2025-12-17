using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;

namespace PetPet.Web.Controllers;

[Authorize(Roles = "Admin")]
public class PetTypeController : Controller
{
    private readonly PetPetDbContext _context;

    public PetTypeController(PetPetDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.PetTypes.ToListAsync());
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Name")] PetType petType)
    {
        if (ModelState.IsValid)
        {
            _context.Add(petType);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(petType);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var petType = await _context.PetTypes.FindAsync(id);
        if (petType == null) return NotFound();
        return View(petType);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name")] PetType petType)
    {
        if (id != petType.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(petType);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PetTypeExists(petType.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        return View(petType);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var petType = await _context.PetTypes.FirstOrDefaultAsync(m => m.Id == id);
        if (petType == null) return NotFound();
        return View(petType);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var petType = await _context.PetTypes.FindAsync(id);
        if (petType != null)
        {
            _context.PetTypes.Remove(petType);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private bool PetTypeExists(int id)
    {
        return _context.PetTypes.Any(e => e.Id == id);
    }
}
