using Amazon.S3;
using TherapuHubAPI.DTOs.Requests.Library;
using TherapuHubAPI.DTOs.Responses.Library;
using TherapuHubAPI.Models;
using TherapuHubAPI.Repositorio;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services.Implementations;

public class LibraryItemService : ILibraryItemService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFileStorageService _storage;
    private readonly ILogger<LibraryItemService> _logger;

    public LibraryItemService(
        IUnitOfWork unitOfWork,
        IFileStorageService storage,
        ILogger<LibraryItemService> logger)
    {
        _unitOfWork = unitOfWork;
        _storage = storage;
        _logger = logger;
    }

    // ── Categories ──────────────────────────────────────────────────────────────

    public async Task<IEnumerable<LibraryCategoryResponseDto>> GetCategoriesAsync()
    {
        var categories = await _unitOfWork.LibraryItems.GetAllCategoriesAsync();
        return categories.Select(c => new LibraryCategoryResponseDto
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug,
            IsActive = c.IsActive
        });
    }

    // ── Items ───────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<LibraryItemResponseDto>> GetByCategorySlugAsync(string categorySlug)
    {
        var categories = await _unitOfWork.LibraryItems.GetAllCategoriesAsync();
        var category = categories.FirstOrDefault(c => c.Slug == categorySlug);
        if (category == null) return Enumerable.Empty<LibraryItemResponseDto>();

        var items = await _unitOfWork.LibraryItems.GetByCategoryAsync(category.Id);
        var result = new List<LibraryItemResponseDto>();

        foreach (var item in items)
        {
            var full = await _unitOfWork.LibraryItems.GetByIdWithFilesAsync(item.Id);
            if (full != null) result.Add(MapToDto(full));
        }

        return result;
    }

    public async Task<LibraryItemResponseDto?> GetByIdAsync(int id)
    {
        var item = await _unitOfWork.LibraryItems.GetByIdWithFilesAsync(id);
        return item == null ? null : MapToDto(item);
    }

    public async Task<LibraryItemResponseDto> CreateAsync(CreateLibraryItemRequestDto dto, int createdByActorId)
    {
        var entity = new LibraryItems
        {
            CategoryId       = dto.CategoryId,
            Name             = dto.Name,
            Description      = dto.Description,
            Barriers         = dto.Barriers,
            Measurement      = dto.Measurement,
            Functions        = dto.Functions,
            Topography       = dto.Topography,
            Definition       = dto.Definition,
            Objective        = dto.Objective,
            Procedures       = dto.Procedures,
            TeachingMaterials = dto.TeachingMaterials,
            CreatedByActorId = createdByActorId,
            CreatedAt        = DateTime.UtcNow,
            IsActive         = true,
            IsDeleted        = false
        };

        await _unitOfWork.LibraryItems.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        var full = await _unitOfWork.LibraryItems.GetByIdWithFilesAsync(entity.Id);
        return MapToDto(full!);
    }

    public async Task<LibraryItemResponseDto?> UpdateAsync(int id, UpdateLibraryItemRequestDto dto)
    {
        var entity = await _unitOfWork.LibraryItems.GetByIdWithFilesAsync(id);
        if (entity == null) return null;

        entity.Name             = dto.Name;
        entity.Description      = dto.Description;
        entity.Barriers         = dto.Barriers;
        entity.Measurement      = dto.Measurement;
        entity.Functions        = dto.Functions;
        entity.Topography       = dto.Topography;
        entity.Definition       = dto.Definition;
        entity.Objective        = dto.Objective;
        entity.Procedures       = dto.Procedures;
        entity.TeachingMaterials = dto.TeachingMaterials;

        _unitOfWork.LibraryItems.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id, int deletedByActorId)
    {
        var entity = await _unitOfWork.LibraryItems.GetByIdAsync(id);
        if (entity == null) return false;

        entity.IsDeleted        = true;
        entity.DeletedAt        = DateTime.UtcNow;
        entity.DeletedByActorId = deletedByActorId;
        _unitOfWork.LibraryItems.Update(entity);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    // ── Files ───────────────────────────────────────────────────────────────────

    public async Task<LibraryItemFileResponseDto> UploadFileAsync(int itemId, IFormFile file, int uploaderActorId)
    {
        var item = await _unitOfWork.LibraryItems.GetByIdAsync(itemId);
        if (item == null) throw new KeyNotFoundException($"Library item {itemId} not found.");
        if (file.Length == 0) throw new InvalidOperationException("File is empty.");

        var categories = await _unitOfWork.LibraryItems.GetAllCategoriesAsync();
        var category = categories.First(c => c.Id == item.CategoryId);

        var containerPath = $"library/{category.Slug}/{itemId}";
        var blobPath = await _storage.SaveFileAsync(file, containerPath);
        var contentType = _storage.ResolveContentType(file.FileName);

        var entry = new LibraryItemFiles
        {
            LibraryItemId     = itemId,
            FileName          = file.FileName,
            BlobPath          = blobPath,
            ContentType       = contentType,
            FileSize          = file.Length,
            UploadedByActorId = uploaderActorId,
            UploadedAt        = DateTime.UtcNow,
            IsDeleted         = false
        };

        await _unitOfWork.LibraryItems.AddLibraryItemFileAsync(entry);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("File '{Name}' uploaded for library item {ItemId}", file.FileName, itemId);

        return new LibraryItemFileResponseDto
        {
            Id          = entry.Id,
            FileName    = entry.FileName,
            FileSize    = entry.FileSize,
            ContentType = entry.ContentType,
            UploadedAt  = entry.UploadedAt
        };
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> DownloadFileAsync(int itemId, int associationId)
    {
        var item = await _unitOfWork.LibraryItems.GetByIdWithFilesAsync(itemId);
        if (item == null) return null;

        var entry = item.LibraryItemFiles.FirstOrDefault(f => f.Id == associationId);
        if (entry == null) return null;

        try
        {
            var (stream, contentType, _) = await _storage.GetFileAsync(entry.BlobPath);
            return (stream, contentType, entry.FileName);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("R2 object not found for blob path: {BlobPath}", entry.BlobPath);
            return null;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, "R2 error downloading blob: {BlobPath}", entry.BlobPath);
            throw;
        }
    }

    public async Task<bool> DeleteFileAsync(int itemId, int associationId, int deletedByActorId)
    {
        var item = await _unitOfWork.LibraryItems.GetByIdWithFilesAsync(itemId);
        if (item == null) return false;

        var entry = item.LibraryItemFiles.FirstOrDefault(f => f.Id == associationId);
        if (entry == null) return false;

        try { await _storage.DeleteFileAsync(entry.BlobPath); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not delete blob for library file {Id}", associationId); }

        entry.IsDeleted        = true;
        entry.DeletedAt        = DateTime.UtcNow;
        entry.DeletedByActorId = deletedByActorId;
        _unitOfWork.LibraryItems.UpdateLibraryItemFile(entry);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    // ── Mapping ─────────────────────────────────────────────────────────────────

    private LibraryItemResponseDto MapToDto(LibraryItems item)
    {
        var files = item.LibraryItemFiles
            .Select(f => new LibraryItemFileResponseDto
            {
                Id          = f.Id,
                FileName    = f.FileName,
                FileSize    = f.FileSize,
                ContentType = f.ContentType,
                UploadedAt  = f.UploadedAt
            })
            .ToList();

        return new LibraryItemResponseDto
        {
            Id               = item.Id,
            CategoryId       = item.CategoryId,
            CategoryName     = item.Category?.Name ?? string.Empty,
            CategorySlug     = item.Category?.Slug ?? string.Empty,
            Name             = item.Name,
            Description      = item.Description,
            Barriers         = item.Barriers,
            Measurement      = item.Measurement,
            Functions        = item.Functions,
            Topography       = item.Topography,
            Definition       = item.Definition,
            Objective        = item.Objective,
            Procedures       = item.Procedures,
            TeachingMaterials = item.TeachingMaterials,
            CreatedByActorId = item.CreatedByActorId,
            CreatedAt        = item.CreatedAt,
            IsActive         = item.IsActive,
            Files            = files
        };
    }
}
