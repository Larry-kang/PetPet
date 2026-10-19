using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PetPet.Application.Media;
using PetPet.Application.Social;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using PetPet.Infrastructure.Services;

namespace PetPet.Web.Controllers;

public class PostController : Controller
{
    private readonly PetPetDbContext _context;
    private readonly INotificationService _notifications;
    private readonly IMediaService _mediaService;

    public PostController(PetPetDbContext context, INotificationService notifications, IMediaService mediaService)
    {
        _context = context;
        _notifications = notifications;
        _mediaService = mediaService;
    }

    public static string ComputeCanonicalPayloadDigest(string title, string content, string? imageUrl)
    {
        var t = title?.Trim() ?? string.Empty;
        var c = content?.Trim() ?? string.Empty;
        var img = imageUrl?.Trim() ?? string.Empty;
        var canonical = $"T:{Encoding.UTF8.GetByteCount(t)}:{t}|C:{Encoding.UTF8.GetByteCount(c)}:{c}|I:{Encoding.UTF8.GetByteCount(img)}:{img}";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // GET: Post (The Wall / Feed)
    [AllowAnonymous]
    public async Task<IActionResult> Index(int page = 1)
    {
        if (page < 1) page = 1;
        const int pageSize = 20;

        var viewerEmail = User.FindFirstValue(ClaimTypes.Email);

        var query = _context.Posts.AsNoTracking()
            .Where(p => p.IsEnabled && !p.IsDeleted);

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        if (totalPages > 0 && page > totalPages) page = totalPages;

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PostFeedItemDto
            {
                Id = p.Id,
                Title = p.Title,
                Snippet = p.Content.Length > 160 ? p.Content.Substring(0, 160) + "..." : p.Content,
                ImageUrl = p.ImageUrl,
                CreatedAt = p.CreatedAt,
                AuthorName = p.Author.Name,
                AuthorPublicId = p.Author.PublicId,
                AuthorPhoto = p.Author.Photo,
                LikeCount = p.Likes.Count(),
                CommentCount = p.Comments.Count(c => !c.IsDeleted),
                ViewerHasLiked = viewerEmail != null && p.Likes.Any(l => l.UserEmail == viewerEmail)
            })
            .ToListAsync();

        var viewModel = new PostFeedViewModel
        {
            Items = items,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalItems = totalItems
        };

        return View(viewModel);
    }

    // GET: Post/Details/5
    [AllowAnonymous]
    public async Task<IActionResult> Details(int id, int commentPage = 1)
    {
        if (commentPage < 1) commentPage = 1;
        const int commentPageSize = 20;

        var viewerEmail = User.FindFirstValue(ClaimTypes.Email);

        var post = await _context.Posts.AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Likes)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsEnabled && !p.IsDeleted);

        if (post == null)
        {
            return NotFound("找不到指定貼文或該貼文已被刪除。");
        }

        var commentsQuery = _context.Comments.AsNoTracking()
            .Include(c => c.Author)
            .Where(c => c.PostId == id && !c.IsDeleted);

        int totalComments = await commentsQuery.CountAsync();
        int totalCommentPages = (int)Math.Ceiling(totalComments / (double)commentPageSize);
        if (totalCommentPages > 0 && commentPage > totalCommentPages) commentPage = totalCommentPages;

        var comments = await commentsQuery
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Skip((commentPage - 1) * commentPageSize)
            .Take(commentPageSize)
            .Select(c => new CommentItemDto
            {
                Id = c.Id,
                AuthorName = c.Author.Name,
                AuthorPublicId = c.Author.PublicId,
                AuthorPhoto = c.Author.Photo,
                AuthorEmail = c.UserEmail,
                Content = c.Content,
                CreatedAt = c.CreatedAt,
                Version = c.Version,
                IsMyComment = viewerEmail != null && c.UserEmail == viewerEmail
            })
            .ToListAsync();

        var dto = new PostDetailDto
        {
            Id = post.Id,
            Title = post.Title,
            Content = post.Content,
            ImageUrl = post.ImageUrl,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt,
            AuthorName = post.Author.Name,
            AuthorEmail = post.AuthorEmail,
            AuthorPublicId = post.Author.PublicId,
            AuthorPhoto = post.Author.Photo,
            Version = post.Version,
            LikeCount = post.Likes.Count,
            ViewerHasLiked = viewerEmail != null && post.Likes.Any(l => l.UserEmail == viewerEmail),
            IsMyPost = viewerEmail != null && post.AuthorEmail == viewerEmail,
            Comments = comments,
            CommentPage = commentPage,
            TotalCommentPages = totalCommentPages,
            TotalComments = totalComments
        };

        return View(dto);
    }

    // GET: Post/Create
    [Authorize]
    public IActionResult Create()
    {
        return View(new CreatePostDto());
    }

    // POST: Post/Create
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("content-limit")]
    public async Task<IActionResult> Create(CreatePostDto model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

        // Validate image: preset or user-owned media
        string? resolvedImg = await _mediaService.ValidateAndResolveImageAsync(model.ImageUrl, userEmail);

        // Canonical digest computation
        var payloadDigest = ComputeCanonicalPayloadDigest(model.Title, model.Content, resolvedImg);

        // Idempotency check with CommandId
        if (model.CommandId.HasValue && model.CommandId.Value != Guid.Empty)
        {
            var receipt = await _context.OperationReceipts
                .FirstOrDefaultAsync(r => r.ActorEmail == userEmail && r.Operation == "CreatePost" && r.CommandId == model.CommandId.Value);
            if (receipt != null)
            {
                if (receipt.PayloadDigest != payloadDigest)
                {
                    return StatusCode(StatusCodes.Status409Conflict, "相同 CommandId 但內容不一致 (Idempotency conflict)。");
                }
                if (int.TryParse(receipt.TargetId, out int existingId))
                {
                    return RedirectToAction(nameof(Details), new { id = existingId });
                }
            }
        }

        // Single transaction for post + receipt
        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var post = new Post
            {
                AuthorEmail = userEmail,
                Title = model.Title.Trim(),
                Content = model.Content.Trim(),
                ImageUrl = resolvedImg,
                CreatedAt = DateTime.UtcNow,
                IsEnabled = true,
                IsDeleted = false,
                Version = Guid.NewGuid()
            };

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            if (model.CommandId.HasValue && model.CommandId.Value != Guid.Empty)
            {
                _context.OperationReceipts.Add(new OperationReceipt
                {
                    ActorEmail = userEmail,
                    Operation = "CreatePost",
                    CommandId = model.CommandId.Value,
                    PayloadDigest = payloadDigest,
                    ResultCode = "SUCCESS",
                    TargetId = post.Id.ToString(),
                    CompletedAtUtc = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            await tx.CommitAsync();
            TempData["Message"] = "貼文發布成功！";
            return RedirectToAction(nameof(Details), new { id = post.Id });
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync();
            if (model.CommandId.HasValue && model.CommandId.Value != Guid.Empty)
            {
                var existing = await _context.OperationReceipts.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.ActorEmail == userEmail && r.Operation == "CreatePost" && r.CommandId == model.CommandId.Value);
                if (existing != null)
                {
                    if (existing.PayloadDigest == payloadDigest && int.TryParse(existing.TargetId, out int existingId))
                    {
                        return RedirectToAction(nameof(Details), new { id = existingId });
                    }
                    return StatusCode(StatusCodes.Status409Conflict, "相同 CommandId 但內容不一致 (Idempotency conflict)。");
                }
            }
            throw;
        }
    }

    // GET: Post/Edit/5
    [Authorize]
    public async Task<IActionResult> Edit(int id)
    {
        var post = await _context.Posts.FindAsync(id);
        if (post == null || post.IsDeleted) return NotFound();

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (post.AuthorEmail != userEmail) return Forbid();

        var dto = new EditPostDto
        {
            Id = post.Id,
            Title = post.Title,
            Content = post.Content,
            ImageUrl = post.ImageUrl,
            Version = post.Version
        };

        return View(dto);
    }

    // POST: Post/Edit/5
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditPostDto model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id);
        if (post == null || post.IsDeleted) return NotFound();

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (post.AuthorEmail != userEmail) return Forbid();

        if (post.Version != model.Version)
        {
            return StatusCode(StatusCodes.Status409Conflict, "貼文已被修改，請重新整理後再試。");
        }

        string? resolvedImg = await _mediaService.ValidateAndResolveImageAsync(model.ImageUrl, userEmail);

        post.Title = model.Title.Trim();
        post.Content = model.Content.Trim();
        post.ImageUrl = resolvedImg;
        post.UpdatedAt = DateTime.UtcNow;
        post.Version = Guid.NewGuid();

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return StatusCode(StatusCodes.Status409Conflict, "貼文已被修改，請重新整理後再試。");
        }

        TempData["Message"] = "貼文已更新！";
        return RedirectToAction(nameof(Details), new { id = post.Id });
    }

    // POST: Post/Delete/5
    [HttpPost, ActionName("Delete")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, Guid version)
    {
        var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id);
        if (post == null || post.IsDeleted) return NotFound();

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (post.AuthorEmail != userEmail) return Forbid();

        if (version != Guid.Empty && post.Version != version)
        {
            return StatusCode(StatusCodes.Status409Conflict, "貼文已被修改，請重新整理後再試。");
        }

        // Soft delete per PP-DATA-03
        post.IsDeleted = true;
        post.Version = Guid.NewGuid();

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return StatusCode(StatusCodes.Status409Conflict, "貼文已被修改，請重新整理後再試。");
        }

        TempData["Message"] = "貼文已刪除。";
        return RedirectToAction(nameof(Index));
    }

    // POST: Post/SetLike
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLike(int? postId = null, int? id = null, bool? desiredLiked = null, bool? liked = null, string? returnUrl = null)
    {
        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

        int targetPostId = postId ?? id ?? 0;
        bool targetLiked = desiredLiked ?? liked ?? true;

        var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == targetPostId && p.IsEnabled && !p.IsDeleted);
        if (post == null) return NotFound("找不到指定貼文。");

        var existingLike = await _context.Likes.FirstOrDefaultAsync(l => l.PostId == targetPostId && l.UserEmail == userEmail);

        try
        {
            if (targetLiked && existingLike == null)
            {
                _context.Likes.Add(new Like { PostId = targetPostId, UserEmail = userEmail, CreatedAt = DateTime.UtcNow });
                await _context.SaveChangesAsync();
            }
            else if (!targetLiked && existingLike != null)
            {
                _context.Likes.Remove(existingLike);
                await _context.SaveChangesAsync();
            }
        }
        catch (DbUpdateException)
        {
            // Already in desired state by concurrent request, ignore idempotent success
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Details), new { id = targetPostId });
    }

    // POST: Post/AddComment and Post/CreateComment
    [HttpPost]
    [ActionName("CreateComment")]
    [Authorize]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("content-limit")]
    public async Task<IActionResult> CreateComment(int postId, string content)
    {
        return await HandleAddComment(postId, content);
    }

    [HttpPost]
    [ActionName("AddComment")]
    [Authorize]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("content-limit")]
    public async Task<IActionResult> AddComment(int postId, string content)
    {
        return await HandleAddComment(postId, content);
    }

    private async Task<IActionResult> HandleAddComment(int postId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return RedirectToAction(nameof(Details), new { id = postId });
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

        var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == postId && p.IsEnabled && !p.IsDeleted);
        if (post == null) return NotFound("找不到指定貼文。");

        string trimmed = content.Trim();
        if (trimmed.Length > 500) trimmed = trimmed.Substring(0, 500);

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var comment = new Comment
            {
                PostId = postId,
                UserEmail = userEmail,
                Content = trimmed,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,
                Version = Guid.NewGuid()
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            if (!string.Equals(post.AuthorEmail, userEmail, StringComparison.OrdinalIgnoreCase))
            {
                var currentName = User.Identity?.Name ?? "會員";
                await _notifications.CreateNotificationAsync(
                    post.AuthorEmail,
                    userEmail,
                    "comment_created",
                    $"comment:{comment.Id}",
                    "Post",
                    post.Id,
                    $"{currentName} 在您的貼文「{post.Title}」留下了回應。");
            }

            await tx.CommitAsync();
            TempData["SuccessMessage"] = "留言已成功發布！";
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return RedirectToAction(nameof(Details), new { id = postId });
    }

    // POST: Post/EditComment
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("content-limit")]
    public async Task<IActionResult> EditComment(int id, int? commentId, string content, Guid version)
    {
        int targetId = id > 0 ? id : (commentId ?? 0);
        if (string.IsNullOrWhiteSpace(content))
        {
            return BadRequest("留言內容不能為空。");
        }

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(userEmail)) return Unauthorized();

        var comment = await _context.Comments.FirstOrDefaultAsync(c => c.Id == targetId);
        if (comment == null || comment.IsDeleted) return NotFound("找不到指定留言。");

        if (!string.Equals(comment.UserEmail, userEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == comment.PostId && p.IsEnabled && !p.IsDeleted);
        if (post == null) return NotFound("貼文不存在或已被隱藏。");

        if (version != Guid.Empty && comment.Version != version)
        {
            return StatusCode(StatusCodes.Status409Conflict, "留言已被修改，請重新整理後再試。");
        }

        string trimmed = content.Trim();
        if (trimmed.Length > 500) trimmed = trimmed.Substring(0, 500);

        comment.Content = trimmed;
        comment.Version = Guid.NewGuid();

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return StatusCode(StatusCodes.Status409Conflict, "留言已被修改，請重新整理後再試。");
        }

        // Per PP-SOC-03: comment edit does not re-send notification
        TempData["SuccessMessage"] = "留言已更新。";
        return RedirectToAction(nameof(Details), new { id = comment.PostId });
    }

    // POST: Post/DeleteComment/5
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int id, Guid version)
    {
        var comment = await _context.Comments.FirstOrDefaultAsync(c => c.Id == id);
        if (comment == null || comment.IsDeleted) return NotFound();

        var userEmail = User.FindFirstValue(ClaimTypes.Email);
        if (comment.UserEmail != userEmail) return Forbid();

        if (version != Guid.Empty && comment.Version != version)
        {
            return StatusCode(StatusCodes.Status409Conflict, "留言已被修改，請重新整理後再試。");
        }

        // Soft delete comment
        comment.IsDeleted = true;
        comment.Version = Guid.NewGuid();

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return StatusCode(StatusCodes.Status409Conflict, "留言已被修改，請重新整理後再試。");
        }

        TempData["Message"] = "留言已刪除。";
        return RedirectToAction(nameof(Details), new { id = comment.PostId });
    }
}
