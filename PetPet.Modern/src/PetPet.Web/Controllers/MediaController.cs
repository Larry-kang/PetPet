using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PetPet.Application.Media;
using System.Security.Claims;

namespace PetPet.Web.Controllers;

public class MediaController : Controller
{
    private readonly IMediaService _mediaService;

    public MediaController(IMediaService mediaService)
    {
        _mediaService = mediaService;
    }

    [HttpPost("Media/Upload")]
    [Authorize]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("upload-limit")]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        var ownerEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(ownerEmail)) return Unauthorized();

        if (file == null || file.Length == 0)
        {
            return BadRequest("未選擇上傳檔案或檔案為空。");
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, "檔案大小超過 5 MiB 限制。");
        }

        try
        {
            using var stream = file.OpenReadStream();
            var result = await _mediaService.UploadImageAsync(stream, file.ContentType, file.Length, ownerEmail);
            return Ok(new
            {
                id = result.Id,
                url = result.Url,
                contentType = result.ContentType,
                width = result.Width,
                height = result.Height
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("5 MiB"))
            {
                return StatusCode(StatusCodes.Status413PayloadTooLarge, ex.Message);
            }
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("Media/{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var viewerEmail = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var result = await _mediaService.GetMediaFileAsync(id, viewerEmail);

        if (result == null)
        {
            return NotFound("媒體檔案不存在或無權限存取。");
        }

        Response.Headers.Append("X-Content-Type-Options", "nosniff");
        return File(result.Stream, result.ContentType);
    }
}
