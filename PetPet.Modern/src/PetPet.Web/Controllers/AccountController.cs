using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetPet.Application.Media;
using PetPet.Application.Members;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;

namespace PetPet.Web.Controllers;

public class AccountController : Controller
{
    private readonly PetPetDbContext _context;
    private readonly IMemberPasswordHasher _hasher;
    private readonly IMediaService _mediaService;

    public AccountController(PetPetDbContext context, IMemberPasswordHasher hasher, IMediaService mediaService)
    {
        _context = context;
        _hasher = hasher;
        _mediaService = mediaService;
    }

    // GET: /Members/{publicId}
    [HttpGet]
    [AllowAnonymous]
    [Route("Members/{publicId:guid}")]
    public async Task<IActionResult> PublicProfile(Guid publicId)
    {
        var member = await _context.Members.AsNoTracking()
            .FirstOrDefaultAsync(m => m.PublicId == publicId);

        if (member == null || !member.IsEnabled)
        {
            return NotFound("找不到該會員檔案，或該帳號已被停用。");
        }

        var viewerEmail = User.FindFirstValue(ClaimTypes.Email);
        bool isSelf = viewerEmail != null && string.Equals(viewerEmail, member.Email, StringComparison.OrdinalIgnoreCase);

        // Block check
        if (viewerEmail != null && !isSelf)
        {
            var isBlocked = await _context.Blocks.AsNoTracking().AnyAsync(b =>
                (b.BlockerEmail == viewerEmail && b.BlockedEmail == member.Email) ||
                (b.BlockerEmail == member.Email && b.BlockedEmail == viewerEmail));

            if (isBlocked)
            {
                return NotFound("找不到該會員檔案。");
            }
        }

        // City
        string? cityName = null;
        if (member.CityId.HasValue)
        {
            var city = await _context.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.Id == member.CityId.Value);
            cityName = city?.Name;
        }

        // Public Pets
        var varieties = await _context.Varieties.AsNoTracking().ToDictionaryAsync(v => v.Id, v => v.Name);
        var pets = await _context.Pets.AsNoTracking()
            .Where(p => p.OwnerEmail == member.Email && p.IsPublic)
            .OrderBy(p => p.Id)
            .Select(p => new PublicPetSummaryDto
            {
                Id = p.Id,
                Name = p.Name,
                KindCode = p.KindCode,
                VarietyName = p.VarietyId.HasValue && varieties.ContainsKey(p.VarietyId.Value) ? varieties[p.VarietyId.Value] : null,
                Photo = p.Photo,
                Gender = p.Gender,
                EstimatedAgeMonths = p.EstimatedAgeMonths,
                Bio = p.Bio
            })
            .ToListAsync();

        // Visible Posts
        var posts = await _context.Posts.AsNoTracking()
            .Where(p => p.AuthorEmail == member.Email && p.IsEnabled && !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .Take(20)
            .Select(p => new PublicPostSummaryDto
            {
                Id = p.Id,
                Title = p.Title,
                Snippet = p.Content.Length > 160 ? p.Content.Substring(0, 160) + "..." : p.Content,
                ImageUrl = p.ImageUrl,
                CreatedAt = p.CreatedAt,
                LikeCount = p.Likes.Count(),
                CommentCount = p.Comments.Count(c => !c.IsDeleted)
            })
            .ToListAsync();

        bool isFriend = false;
        bool hasPending = false;
        if (viewerEmail != null && !isSelf)
        {
            string memberA = string.CompareOrdinal(viewerEmail, member.Email) < 0 ? viewerEmail : member.Email;
            string memberB = string.CompareOrdinal(viewerEmail, member.Email) < 0 ? member.Email : viewerEmail;
            var fr = await _context.Friends.AsNoTracking().FirstOrDefaultAsync(f => f.MemberAEmail == memberA && f.MemberBEmail == memberB);
            if (fr != null)
            {
                isFriend = fr.IsAccepted;
                hasPending = !fr.IsAccepted;
            }
        }

        var dto = new PublicMemberProfileDto
        {
            PublicId = member.PublicId,
            Name = member.Name,
            Bio = member.Bio,
            CityName = cityName,
            Photo = member.Photo,
            Pets = pets,
            Posts = posts,
            IsSelf = isSelf,
            IsFriend = isFriend,
            HasPendingRequest = hasPending
        };

        return View(dto);
    }

    // GET: /Account/Profile
    [HttpGet]
    [Authorize]
    [Route("Account/Profile")]
    public async Task<IActionResult> Profile()
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

        var member = await _context.Members.AsNoTracking().FirstOrDefaultAsync(m => m.Email == userEmail);
        if (member == null) return NotFound();

        ViewBag.Cities = await _context.Cities.AsNoTracking().OrderBy(c => c.Id).ToListAsync();

        var dto = new EditProfileDto
        {
            Name = member.Name,
            Bio = member.Bio,
            CityId = member.CityId,
            Photo = member.Photo,
            IsMatchEnabled = member.IsMatchEnabled,
            Version = member.Version
        };

        return View(dto);
    }

    // POST: /Account/Profile
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [Route("Account/Profile")]
    public async Task<IActionResult> Profile(EditProfileDto model)
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

        if (!ModelState.IsValid)
        {
            ViewBag.Cities = await _context.Cities.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
            return View(model);
        }

        var member = await _context.Members.FirstOrDefaultAsync(m => m.Email == userEmail);
        if (member == null) return NotFound();

        if (member.Version != model.Version)
        {
            TempData["ErrorMessage"] = "個人資料已被其他操作變更，請重新整理頁面。";
            return StatusCode(StatusCodes.Status409Conflict, "版本衝突。");
        }

        string? resolvedPhoto = await _mediaService.ValidateAndResolveImageAsync(model.Photo, userEmail);

        member.Name = model.Name.Trim();
        member.Bio = string.IsNullOrWhiteSpace(model.Bio) ? null : model.Bio.Trim();
        member.CityId = model.CityId;
        member.Photo = resolvedPhoto;
        member.IsMatchEnabled = model.IsMatchEnabled;
        member.Version = Guid.NewGuid();

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["ErrorMessage"] = "個人資料已被其他操作變更，請重新整理頁面。";
            return StatusCode(StatusCodes.Status409Conflict, "版本衝突。");
        }

        TempData["SuccessMessage"] = "個人資料已成功更新！";

        return RedirectToAction(nameof(Profile));
    }

    // GET: /Account/ChangePassword
    [HttpGet]
    [Authorize]
    [Route("Account/ChangePassword")]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordDto());
    }

    // POST: /Account/ChangePassword
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [Route("Account/ChangePassword")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

        var member = await _context.Members.FirstOrDefaultAsync(m => m.Email == userEmail);
        if (member == null) return NotFound();

        var check = _hasher.Verify(member, model.CurrentPassword);
        if (check == PasswordCheck.Failed)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "目前密碼不正確。");
            return View(model);
        }

        member.Password = _hasher.Hash(member, model.NewPassword);
        member.SecurityStamp = Guid.NewGuid().ToString("N");
        member.Version = Guid.NewGuid();

        await _context.SaveChangesAsync();

        // Sign out all sessions
        await HttpContext.SignOutAsync();
        TempData["SuccessMessage"] = "密碼已成功更新，請以新密碼重新登入。";

        return RedirectToAction("Login", "Member");
    }

    // GET: /Account/AccessDenied
    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
