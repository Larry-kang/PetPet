using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;

namespace PetPet.Web.Controllers;

[Authorize(Roles = "Admin")]
public class PetVarietyController : Controller
{
    private readonly PetPetDbContext _context;

    public PetVarietyController(PetPetDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var varieties = _context.PetVarieties.Include(p => p.PetType);
        return View(await varieties.ToListAsync());
    }

    public IActionResult Create()
    {
        ViewData["PetTypeId"] = new SelectList(_context.PetTypes, "Id", "Name");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Name,PetTypeId")] PetVariety petVariety)
    {
        if (ModelState.IsValid)
        {
            _context.Add(petVariety);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        ViewData["PetTypeId"] = new SelectList(_context.PetTypes, "Id", "Name", petVariety.PetTypeId);
        return View(petVariety);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var petVariety = await _context.PetVarieties.FindAsync(id);
        if (petVariety == null) return NotFound();
        ViewData["PetTypeId"] = new SelectList(_context.PetTypes, "Id", "Name", petVariety.PetTypeId);
        return View(petVariety);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,PetTypeId")] PetVariety petVariety)
    {
        if (id != petVariety.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(petVariety);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PetVarietyExists(petVariety.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index));
        }
        ViewData["PetTypeId"] = new SelectList(_context.PetTypes, "Id", "Name", petVariety.PetTypeId);
        return View(petVariety);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var petVariety = await _context.PetVarieties
            .Include(p => p.PetType)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (petVariety == null) return NotFound();
        return View(petVariety);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var petVariety = await _context.PetVarieties.FindAsync(id);
        if (petVariety != null)
        {
            _context.PetVarieties.Remove(petVariety);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private bool PetVarietyExists(int id)
    {
        return _context.PetVarieties.Any(e => e.Id == id);
    }
}
