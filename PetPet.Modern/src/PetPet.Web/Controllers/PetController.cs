using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetPet.Application.Media;
using PetPet.Application.Pets;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;

namespace PetPet.Web.Controllers;

[Authorize]
public class PetController : Controller
{
    private readonly PetPetDbContext _context;
    private readonly IMediaService _mediaService;

    public PetController(PetPetDbContext context, IMediaService mediaService)
    {
        _context = context;
        _mediaService = mediaService;
    }

    // GET: Pet (My Pets)
    public async Task<IActionResult> Index()
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        var pets = await _context.Pets.AsNoTracking()
            .Where(p => p.OwnerEmail == userEmail)
            .OrderByDescending(p => p.Id)
            .ToListAsync();

        ViewBag.Varieties = await _context.Varieties.AsNoTracking().ToDictionaryAsync(v => v.Id, v => v.Name);

        return View(pets);
    }

    // GET: Pet/Details/5
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id)
    {
        var pet = await _context.Pets.AsNoTracking()
            .Include(p => p.Owner)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (pet == null) return NotFound("找不到指定的寵物資料。");

        var viewerEmail = User.FindFirstValue(ClaimTypes.Email);
        bool isOwner = viewerEmail != null && string.Equals(viewerEmail, pet.OwnerEmail, StringComparison.OrdinalIgnoreCase);

        // If private, only owner can view; others receive 404
        if (!pet.IsPublic && !isOwner)
        {
            return NotFound("找不到指定的寵物資料。");
        }

        // Mutual block check
        if (viewerEmail != null && !isOwner)
        {
            var isBlocked = await _context.Blocks.AsNoTracking().AnyAsync(b =>
                (b.BlockerEmail == viewerEmail && b.BlockedEmail == pet.OwnerEmail) ||
                (b.BlockerEmail == pet.OwnerEmail && b.BlockedEmail == viewerEmail));

            if (isBlocked)
            {
                return NotFound("找不到指定的寵物資料。");
            }
        }

        string? varietyName = null;
        if (pet.VarietyId.HasValue)
        {
            var variety = await _context.Varieties.AsNoTracking().FirstOrDefaultAsync(v => v.Id == pet.VarietyId.Value);
            varietyName = variety?.Name;
        }

        string? ownerCityName = null;
        if (pet.Owner.CityId.HasValue)
        {
            var city = await _context.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.Id == pet.Owner.CityId.Value);
            ownerCityName = city?.Name;
        }

        var dto = new PetDetailDto
        {
            Id = pet.Id,
            Name = pet.Name,
            KindCode = pet.KindCode,
            VarietyName = varietyName,
            Photo = pet.Photo,
            Gender = pet.Gender,
            BirthDate = pet.BirthDate,
            EstimatedAgeMonths = pet.EstimatedAgeMonths,
            Bio = pet.Bio,
            IsPublic = pet.IsPublic,
            OwnerName = pet.Owner.Name,
            OwnerPublicId = pet.Owner.PublicId,
            OwnerCityName = ownerCityName,
            IsOwner = isOwner,
            Version = pet.Version
        };

        return View(dto);
    }

    // GET: Pet/Create
    public async Task<IActionResult> Create()
    {
        ViewBag.Varieties = await _context.Varieties.AsNoTracking().OrderBy(v => v.Id).ToListAsync();
        return View(new CreatePetDto());
    }

    // POST: Pet/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePetDto model)
    {
        if (model.BirthDate.HasValue && model.BirthDate.Value > DateTime.UtcNow)
        {
            ModelState.AddModelError(nameof(model.BirthDate), "生日不得為未來時間。");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Varieties = await _context.Varieties.AsNoTracking().OrderBy(v => v.Id).ToListAsync();
            return View(model);
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

        string? resolvedPhoto = await _mediaService.ValidateAndResolveImageAsync(model.Photo, userEmail);
        string photo = resolvedPhoto ?? "/images/presets/avatar-dog.svg";

        var pet = new Pet
        {
            OwnerEmail = userEmail,
            Name = model.Name.Trim(),
            KindCode = string.IsNullOrWhiteSpace(model.KindCode) ? "Dog" : model.KindCode,
            VarietyId = model.VarietyId ?? 1,
            Gender = model.Gender,
            BirthDate = model.BirthDate,
            EstimatedAgeMonths = model.EstimatedAgeMonths,
            Bio = model.Bio?.Trim(),
            Photo = photo,
            IsPublic = model.IsPublic,
            Version = Guid.NewGuid()
        };

        _context.Pets.Add(pet);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"成功新增寵物：{pet.Name}！";
        return RedirectToAction(nameof(Index));
    }

    // GET: Pet/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var pet = await _context.Pets.FindAsync(id);
        if (pet == null) return NotFound();

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (pet.OwnerEmail != userEmail) return Forbid();

        ViewBag.Varieties = await _context.Varieties.AsNoTracking().OrderBy(v => v.Id).ToListAsync();

        var dto = new EditPetDto
        {
            Id = pet.Id,
            Name = pet.Name,
            KindCode = pet.KindCode,
            VarietyId = pet.VarietyId,
            Gender = pet.Gender,
            BirthDate = pet.BirthDate,
            EstimatedAgeMonths = pet.EstimatedAgeMonths,
            IsPublic = pet.IsPublic,
            Photo = pet.Photo,
            Bio = pet.Bio,
            Version = pet.Version
        };

        return View(dto);
    }

    // POST: Pet/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditPetDto model)
    {
        if (id != model.Id) return BadRequest();

        if (model.BirthDate.HasValue && model.BirthDate.Value > DateTime.UtcNow)
        {
            ModelState.AddModelError(nameof(model.BirthDate), "生日不得為未來時間。");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Varieties = await _context.Varieties.AsNoTracking().OrderBy(v => v.Id).ToListAsync();
            return View(model);
        }

        var pet = await _context.Pets.FindAsync(id);
        if (pet == null) return NotFound();

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (pet.OwnerEmail != userEmail) return Forbid();

        if (pet.Version != model.Version)
        {
            TempData["ErrorMessage"] = "寵物資料已被其他操作變更，請重新整理頁面。";
            return StatusCode(StatusCodes.Status409Conflict, "資料已被修改，請重新載入後再試。");
        }

        string? resolvedPhoto = await _mediaService.ValidateAndResolveImageAsync(model.Photo, userEmail);
        string photo = resolvedPhoto ?? pet.Photo ?? "/images/presets/avatar-dog.svg";

        pet.Name = model.Name.Trim();
        pet.KindCode = model.KindCode;
        pet.VarietyId = model.VarietyId;
        pet.Gender = model.Gender;
        pet.BirthDate = model.BirthDate;
        pet.EstimatedAgeMonths = model.EstimatedAgeMonths;
        pet.IsPublic = model.IsPublic;
        pet.Photo = photo;
        pet.Bio = model.Bio?.Trim();
        pet.Version = Guid.NewGuid();

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["ErrorMessage"] = "寵物資料已被其他操作變更，請重新整理頁面。";
            return StatusCode(StatusCodes.Status409Conflict, "資料已被修改，請重新載入後再試。");
        }

        TempData["SuccessMessage"] = $"已更新寵物資料：{pet.Name}。";
        return RedirectToAction(nameof(Index));
    }

    // POST: Pet/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, Guid version)
    {
        var pet = await _context.Pets.FindAsync(id);
        if (pet == null) return NotFound();

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (pet.OwnerEmail != userEmail) return Forbid();

        if (version != Guid.Empty && pet.Version != version)
        {
            TempData["ErrorMessage"] = "寵物資料已被其他操作變更，請重新整理頁面。";
            return StatusCode(StatusCodes.Status409Conflict, "資料版本不符，請重新整理後再試。");
        }

        _context.Pets.Remove(pet);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["ErrorMessage"] = "寵物資料已被其他操作變更，請重新整理頁面。";
            return StatusCode(StatusCodes.Status409Conflict, "資料版本不符，請重新整理後再試。");
        }

        TempData["SuccessMessage"] = $"已移除寵物：{pet.Name}。";
        return RedirectToAction(nameof(Index));
    }
}
