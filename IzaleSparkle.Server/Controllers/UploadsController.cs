using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Server.Controllers;

[ApiController]
[Route("api/uploads")]
[Authorize(Policy = "AdminOnly")]
public class UploadsController(IWebHostEnvironment env, ILogger<UploadsController> logger)
    : ControllerBase
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    // WebRootPath is null when the Server project has no wwwroot of its own
    // (Blazor Hosted uses UseBlazorFrameworkFiles which sets its own file provider).
    // Fall back to ContentRootPath/wwwroot which always exists.
    private string GetUploadsDir()
    {
        var webRoot = env.WebRootPath
            ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Upload a single image file. Returns its public URL.</summary>
    [HttpPost("image")]
    [RequestSizeLimit(10_485_760)] // 10 MB
    [ProducesResponseType(typeof(ApiResponse<UploadImageResponse>), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("No file provided."));

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(ApiResponse<object>.Fail("File exceeds 10 MB limit."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(ApiResponse<object>.Fail(
                $"File type '{ext}' not allowed. Use JPG, PNG, WebP or GIF."));

        // Save to wwwroot/uploads on the server (served as static files)
        var uploadsDir = GetUploadsDir();

        // Unique filename to avoid collisions
        var fileName  = $"{Guid.NewGuid():N}{ext}";
        var filePath  = Path.Combine(uploadsDir, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream, ct);

        logger.LogInformation("[Upload] Saved image: {FileName} ({Size} bytes)", fileName, file.Length);

        // Return the relative URL — served via UseStaticFiles()
        var url = $"/uploads/{fileName}";

        return Ok(ApiResponse<UploadImageResponse>.Ok(
            new UploadImageResponse(url, fileName, file.Length),
            "Image uploaded successfully."));
    }

    /// <summary>Upload multiple images at once (up to 6).</summary>
    [HttpPost("images")]
    [RequestSizeLimit(62_914_560)] // 60 MB total
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UploadImageResponse>>), 200)]
    public async Task<IActionResult> UploadImages(IList<IFormFile> files, CancellationToken ct)
    {
        if (files == null || files.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("No files provided."));

        if (files.Count > 6)
            return BadRequest(ApiResponse<object>.Fail("Maximum 6 images per upload."));

        var uploadsDir = GetUploadsDir();

        var results = new List<UploadImageResponse>();
        foreach (var file in files)
        {
            if (file.Length == 0 || file.Length > MaxFileSizeBytes) continue;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) continue;

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);
            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, ct);
            results.Add(new UploadImageResponse($"/uploads/{fileName}", fileName, file.Length));
        }

        return Ok(ApiResponse<IEnumerable<UploadImageResponse>>.Ok(results));
    }

    /// <summary>Delete an uploaded image by filename.</summary>
    [HttpDelete("{fileName}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
    public IActionResult DeleteImage(string fileName)
    {
        // Safety check — only allow deleting from the uploads directory
        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            return BadRequest(ApiResponse<object>.Fail("Invalid filename."));

        var uploadsDir2 = GetUploadsDir();
        var filePath = Path.Combine(uploadsDir2, fileName);
        if (!System.IO.File.Exists(filePath))
            return Ok(ApiResponse<bool>.Ok(true)); // idempotent

        System.IO.File.Delete(filePath);
        logger.LogInformation("[Upload] Deleted image: {FileName}", fileName);
        return Ok(ApiResponse<bool>.Ok(true, "Image deleted."));
    }
}
