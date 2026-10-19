using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PetPet.Application.Media;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;
using SkiaSharp;

namespace PetPet.Infrastructure.Services;

public class MediaService : IMediaService
{
    private readonly PetPetDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<MediaService> _logger;

    public MediaService(PetPetDbContext db, IWebHostEnvironment env, ILogger<MediaService> logger)
    {
        _db = db;
        _env = env;
        _logger = logger;
    }

    private string GetUploadsDirectory()
    {
        var dir = Path.Combine(_env.ContentRootPath, "App_Data", "uploads");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return dir;
    }

    public async Task<MediaUploadResult> UploadImageAsync(Stream stream, string claimedContentType, long length, string ownerEmail)
    {
        if (length <= 0)
        {
            throw new ArgumentException("上傳檔案為空。");
        }

        if (length > 5 * 1024 * 1024)
        {
            throw new InvalidOperationException("檔案大小超過 5 MiB 限制。");
        }

        // Magic byte check (only JPEG, PNG, WebP allowed; specifically rejects SVG, GIF, HTML, etc.)
        byte[] header = new byte[12];
        int read = await stream.ReadAsync(header.AsMemory(0, 12));
        if (read < 12)
        {
            throw new ArgumentException("檔案標頭不完整或格式無效。");
        }

        bool isJpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        bool isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                     header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
        bool isWebp = header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 && // RIFF
                      header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50; // WEBP

        if (!isJpeg && !isPng && !isWebp)
        {
            throw new ArgumentException("僅支援 JPEG、PNG 或 WebP 格式圖片。");
        }

        // Reset stream
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        // Decode using SkiaSharp
        using var codec = SKCodec.Create(stream);
        if (codec == null)
        {
            throw new ArgumentException("圖片解碼失敗，可能為損毀或不支援之檔案。");
        }

        int width = codec.Info.Width;
        int height = codec.Info.Height;

        if (width <= 0 || height <= 0 || width > 4096 || height > 4096 || (long)width * height > 16_000_000)
        {
            throw new ArgumentException("圖片解析度不可超過 4096x4096 且總像素不可超過 1,600 萬。");
        }

        using var bitmap = SKBitmap.Decode(codec);
        if (bitmap == null)
        {
            throw new ArgumentException("圖片無法正確解碼。");
        }

        // Re-encode to strip metadata and active content
        SKEncodedImageFormat format;
        string ext;
        string contentType;
        int quality;

        if (isJpeg)
        {
            format = SKEncodedImageFormat.Jpeg;
            ext = ".jpg";
            contentType = "image/jpeg";
            quality = 85;
        }
        else if (isPng)
        {
            format = SKEncodedImageFormat.Png;
            ext = ".png";
            contentType = "image/png";
            quality = 100;
        }
        else
        {
            format = SKEncodedImageFormat.Webp;
            ext = ".webp";
            contentType = "image/webp";
            quality = 85;
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var encodedData = image.Encode(format, quality);
        if (encodedData == null)
        {
            throw new InvalidOperationException("圖片重新編碼失敗。");
        }

        var mediaId = Guid.NewGuid();
        var storageKey = $"{mediaId:N}{ext}";
        var uploadsDir = GetUploadsDirectory();
        var targetPath = Path.Combine(uploadsDir, storageKey);

        // Write re-encoded clean image to disk
        using (var fs = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            encodedData.SaveTo(fs);
        }

        var media = new Media
        {
            Id = mediaId,
            OwnerEmail = ownerEmail,
            StorageKey = storageKey,
            ContentType = contentType,
            ByteLength = encodedData.Size,
            Width = width,
            Height = height,
            CreatedAt = DateTime.UtcNow
        };

        _db.Media.Add(media);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch
        {
            // Avoid orphaned file on disk
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            throw;
        }

        return new MediaUploadResult(
            media.Id,
            $"/Media/{media.Id}",
            media.ContentType,
            media.ByteLength,
            media.Width,
            media.Height
        );
    }

    public async Task<MediaFileResult?> GetMediaFileAsync(Guid id, string? viewerEmail)
    {
        var media = await _db.Media.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (media == null) return null;

        // Check ownership or public reference
        bool isOwner = !string.IsNullOrEmpty(viewerEmail) && string.Equals(viewerEmail, media.OwnerEmail, StringComparison.OrdinalIgnoreCase);

        if (!isOwner)
        {
            var mediaUrl = $"/Media/{id}";
            bool isMemberPhoto = await _db.Members.AsNoTracking().AnyAsync(m => m.Photo == mediaUrl);
            bool isPetPhoto = await _db.Pets.AsNoTracking().AnyAsync(p => p.Photo == mediaUrl && p.IsPublic);
            bool isPostImg = await _db.Posts.AsNoTracking().AnyAsync(p => p.ImageUrl == mediaUrl && !p.IsDeleted && p.IsEnabled);

            if (!isMemberPhoto && !isPetPhoto && !isPostImg)
            {
                return null; // Forbidden / Not accessible
            }
        }

        var uploadsDir = GetUploadsDirectory();
        var targetPath = Path.Combine(uploadsDir, media.StorageKey);

        if (!File.Exists(targetPath))
        {
            _logger.LogWarning("媒體檔案實體遺失: {Path}", targetPath);
            return null;
        }

        var stream = new FileStream(targetPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new MediaFileResult(stream, media.ContentType, media.ByteLength);
    }

    public async Task<int> CleanupOrphansAsync(TimeSpan olderThan)
    {
        var cutoff = DateTime.UtcNow.Subtract(olderThan);
        var candidates = await _db.Media
            .Where(m => m.CreatedAt < cutoff)
            .ToListAsync();

        int cleaned = 0;
        var uploadsDir = GetUploadsDirectory();

        foreach (var media in candidates)
        {
            var mediaUrl = $"/Media/{media.Id}";
            bool isMemberPhoto = await _db.Members.AsNoTracking().AnyAsync(m => m.Photo == mediaUrl);
            bool isPetPhoto = await _db.Pets.AsNoTracking().AnyAsync(p => p.Photo == mediaUrl);
            bool isPostImg = await _db.Posts.AsNoTracking().AnyAsync(p => p.ImageUrl == mediaUrl && !p.IsDeleted);

            if (!isMemberPhoto && !isPetPhoto && !isPostImg)
            {
                var filePath = Path.Combine(uploadsDir, media.StorageKey);
                if (File.Exists(filePath))
                {
                    try { File.Delete(filePath); } catch { /* ignore */ }
                }
                _db.Media.Remove(media);
                cleaned++;
            }
        }

        if (cleaned > 0)
        {
            await _db.SaveChangesAsync();
        }

        return cleaned;
    }

    public static readonly HashSet<string> AllowedPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        "/images/presets/avatar-dog.svg",
        "/images/presets/avatar-cat.svg",
        "/images/presets/avatar-boy.svg",
        "/images/presets/avatar-girl.svg"
    };

    public async Task<string?> ValidateAndResolveImageAsync(string? imageUrl, string userEmail)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;
        var trimmed = imageUrl.Trim();

        // 1. Check if it is an allowed preset
        if (AllowedPresets.Contains(trimmed))
        {
            return trimmed;
        }

        // 2. Check if it is a Media reference (/Media/{guid} or raw {guid})
        Guid mediaId;
        if (trimmed.StartsWith("/Media/", StringComparison.OrdinalIgnoreCase))
        {
            var idPart = trimmed.Substring("/Media/".Length);
            if (!Guid.TryParse(idPart, out mediaId))
            {
                return null;
            }
        }
        else if (!Guid.TryParse(trimmed, out mediaId))
        {
            return null;
        }

        // 3. Verify media exists and is owned by current user
        var media = await _db.Media.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mediaId);
        if (media != null && string.Equals(media.OwnerEmail, userEmail, StringComparison.OrdinalIgnoreCase))
        {
            return $"/Media/{media.Id}";
        }

        return null;
    }
}
