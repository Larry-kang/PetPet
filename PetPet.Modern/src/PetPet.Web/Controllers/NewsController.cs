using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetPet.Infrastructure.Data;

namespace PetPet.Web.Controllers;

public class NewsController : Controller
{
    private readonly PetPetDbContext _context;

    public NewsController(PetPetDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var news = await _context.News
            .Where(n => n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(n => n.PublishedAt)
            .ToListAsync();
            
        return View(news);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var news = await _context.News.FirstOrDefaultAsync(m => m.Id == id);
        if (news == null) return NotFound();

        return View(news);
    }
}
