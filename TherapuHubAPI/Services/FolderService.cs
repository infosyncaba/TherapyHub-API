using Microsoft.EntityFrameworkCore;
using TherapuHubAPI.DTOs.Requests.Folders;
using TherapuHubAPI.DTOs.Responses.Folders;
using TherapuHubAPI.Models;
using TherapuHubAPI.Repositorio;
using TherapuHubAPI.Repositorio.IRepositorio;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services;

public class FolderService : IFolderService
{
    private readonly IFolderRepositorio _folderRepo;
    private readonly IFileRepositorio _fileRepo;
    private readonly IFileStorageService _storage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ContextDB _context;
    private readonly ILogger<FolderService> _logger;

    public FolderService(
        IFolderRepositorio folderRepo,
        IFileRepositorio fileRepo,
        IFileStorageService storage,
        IUnitOfWork unitOfWork,
        ContextDB context,
        ILogger<FolderService> logger)
    {
        _folderRepo = folderRepo;
        _fileRepo = fileRepo;
        _storage = storage;
        _unitOfWork = unitOfWork;
        _context = context;
        _logger = logger;
    }

    // ─── Folders ─────────────────────────────────────────────────────────────

    public async Task<IEnumerable<FolderResponseDto>> GetFoldersByTypeAsync(int companyId, byte folderTypeId, int actorId, int userTypeId, int? sectionId = null)
    {
        // Everyone sees only global folders or folders they created — privacy applies to all users
        var folders = await _folderRepo.GetVisibleByCompanyAndTypeAsync(companyId, folderTypeId, actorId, sectionId);

        var folderTypes = await _context.FolderTypes.ToListAsync();

        var result = new List<FolderResponseDto>();
        foreach (var f in folders)
        {
            var fileCount = await _fileRepo.CountByFolderAsync(f.Id);
            result.Add(MapFolderToDto(f, fileCount, folderTypes));
        }

        return result;
    }

    public async Task<IEnumerable<FolderResponseDto>> GetSubfoldersAsync(int parentFolderId, int companyId, int actorId, int userTypeId)
    {
        // Everyone sees only global subfolders or subfolders they created — privacy applies to all users
        var folders = await _folderRepo.GetVisibleSubfoldersAsync(parentFolderId, companyId, actorId);

        var folderTypes = await _context.FolderTypes.ToListAsync();
        var result = new List<FolderResponseDto>();
        foreach (var f in folders)
        {
            var fileCount = await _fileRepo.CountByFolderAsync(f.Id);
            result.Add(MapFolderToDto(f, fileCount, folderTypes));
        }

        return result;
    }

    public async Task<FolderResponseDto?> GetFolderByIdAsync(int id, int companyId)
    {
        var folder = await _folderRepo.GetByIdAndCompanyAsync(id, companyId);
        if (folder == null) return null;

        var folderTypes = await _context.FolderTypes.ToListAsync();
        var fileCount = await _fileRepo.CountByFolderAsync(folder.Id);

        return MapFolderToDto(folder, fileCount, folderTypes);
    }

    public async Task<FolderResponseDto> CreateFolderAsync(CreateFolderRequestDto request, int companyId, int actorId, int userTypeId)
    {
        _logger.LogInformation("Creating folder '{Name}' (type {TypeId}) for company {CompanyId}", request.Name, request.FolderTypeId, companyId);

        var isSystem = await _context.UserTypes.Where(t => t.Id == userTypeId).Select(t => t.IsSystem).FirstOrDefaultAsync();

        var level = 0;
        var parentPath = string.Empty;
        bool effectiveIsGlobal;

        if (request.ParentFolderId.HasValue)
        {
            var parent = await _folderRepo.GetByIdAndCompanyAsync(request.ParentFolderId.Value, companyId);
            if (parent == null)
                throw new InvalidOperationException("Parent folder not found");

            level = parent.Level + 1;
            parentPath = parent.Path;

            // Child folders always inherit IsGlobal from their parent
            effectiveIsGlobal = parent.IsGlobal;
        }
        else
        {
            // Root folder: only system users can create global folders
            if (request.IsGlobal && !isSystem)
                throw new InvalidOperationException("Only system users can create global folders");

            effectiveIsGlobal = isSystem && request.IsGlobal;
        }

        // Name must be unique within the same parent scope
        if (await _folderRepo.ExistsByNameAsync(companyId, request.FolderTypeId, request.Name.Trim(), parentFolderId: request.ParentFolderId))
            throw new InvalidOperationException($"A folder named '{request.Name}' already exists here");

        // CreatedByActorId is always the creator — IsGlobal only controls visibility
        var folder = new Folders
        {
            CompanyId = companyId,
            CreatedByActorId = actorId,
            OwnerActorId = request.OwnerActorId,
            MenuId = request.MenuId,
            SectionId = request.SectionId,
            ParentFolderId = request.ParentFolderId,
            FolderTypeId = request.FolderTypeId,
            Name = request.Name.Trim(),
            Path = string.Empty, // will be set after insert (needs the generated Id)
            Level = level,
            IsGlobal = effectiveIsGlobal,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            IsDeleted = false
        };

        await _folderRepo.AddAsync(folder);
        await _unitOfWork.SaveChangesAsync();

        // Build ID-based path: root = /{id}, subfolder = {parentPath}/{id}
        folder.Path = string.IsNullOrEmpty(parentPath)
            ? $"/{folder.Id}"
            : $"{parentPath}/{folder.Id}";

        _folderRepo.Update(folder);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Folder created with Id: {Id}, Path: {Path}", folder.Id, folder.Path);

        var folderTypes = await _context.FolderTypes.ToListAsync();
        return MapFolderToDto(folder, 0, folderTypes);
    }

    public async Task<FolderResponseDto?> UpdateFolderAsync(int id, UpdateFolderRequestDto request, int companyId)
    {
        var folder = await _folderRepo.GetByIdAndCompanyAsync(id, companyId);
        if (folder == null) return null;

        if (await _folderRepo.ExistsByNameAsync(companyId, folder.FolderTypeId, request.Name.Trim(), excludeId: id, parentFolderId: folder.ParentFolderId))
            throw new InvalidOperationException($"A folder named '{request.Name}' already exists here");

        folder.Name = request.Name.Trim();
        folder.IsGlobal = request.IsGlobal;
        // CreatedByActorId is never cleared on update — ownership belongs to the creator

        _folderRepo.Update(folder);
        await _unitOfWork.SaveChangesAsync();

        var folderTypes = await _context.FolderTypes.ToListAsync();
        var fileCount = await _fileRepo.CountByFolderAsync(folder.Id);
        return MapFolderToDto(folder, fileCount, folderTypes);
    }

    public async Task<bool> DeleteFolderAsync(int id, int companyId, int actorId, int userTypeId)
    {
        var folder = await _folderRepo.GetByIdAndCompanyAsync(id, companyId);
        if (folder == null) return false;

        var isSystem = await _context.UserTypes.Where(t => t.Id == userTypeId).Select(t => t.IsSystem).FirstOrDefaultAsync();

        if (!isSystem && folder.CreatedByActorId != actorId)
            throw new UnauthorizedAccessException("Only the folder owner can delete it");


        // Soft-delete all files in the folder and remove from storage
        var files = await _fileRepo.GetByFolderAsync(id);
        foreach (var file in files)
        {
            try { await _storage.DeleteFileAsync(file.BlobPath); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not delete blob for file Id {FileId}", file.Id); }

            file.IsDeleted = true;
            file.DeletedAt = DateTime.UtcNow;
            _fileRepo.Update(file);
        }

        folder.IsDeleted = true;
        folder.IsActive = false;
        folder.DeletedAt = DateTime.UtcNow;
        folder.DeletedByActorId = actorId;
        _folderRepo.Update(folder);

        await _unitOfWork.SaveChangesAsync();
        _logger.LogInformation("Folder {Id} soft-deleted", id);
        return true;
    }

    // ─── Files ────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<FileResponseDto>> GetFilesAsync(int folderId, int companyId)
    {
        // Ensure the folder belongs to this company
        var folder = await _folderRepo.GetByIdAndCompanyAsync(folderId, companyId);
        if (folder == null) return [];

        var files = await _fileRepo.GetByFolderAsync(folderId);
        var result = new List<FileResponseDto>();

        foreach (var f in files)
        {
            long size = 0;
            try { size = await _storage.GetFileSizeAsync(f.BlobPath); }
            catch { /* file may not exist on disk in edge cases */ }

            result.Add(MapFileToDto(f, size));
        }

        return result;
    }

    public async Task<FileResponseDto> UploadFileAsync(int folderId, IFormFile file, int companyId, int actorId)
    {
        var folder = await _folderRepo.GetByIdAndCompanyAsync(folderId, companyId);
        if (folder == null)
            throw new InvalidOperationException("Folder not found");

        if (file.Length == 0)
            throw new InvalidOperationException("The uploaded file is empty");

        // Container path: e.g. "1/42" → company 1, folder 42
        var containerPath = $"{companyId}/{folderId}";
        var blobPath = await _storage.SaveFileAsync(file, containerPath);

        var entity = new Files
        {
            FolderId = folderId,
            FileName = file.FileName,
            BlobPath = blobPath,
            UploadedByActorId = actorId,
            OwnerActorId = folder.OwnerActorId ?? actorId,
            FilesTypeId = 1,
            UploadedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        await _fileRepo.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("File '{Name}' uploaded to folder {FolderId}, Id: {FileId}", file.FileName, folderId, entity.Id);
        return MapFileToDto(entity, file.Length);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)?> DownloadFileAsync(long fileId, int folderId, int companyId)
    {
        // Ensure folder belongs to company
        var folder = await _folderRepo.GetByIdAndCompanyAsync(folderId, companyId);
        if (folder == null) return null;

        var file = await _fileRepo.GetByIdAndFolderAsync(fileId, folderId);
        if (file == null) return null;

        try
        {
            var result = await _storage.GetFileAsync(file.BlobPath);
            return result;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    public async Task<bool> DeleteFileAsync(long fileId, int folderId, int companyId, int deleterActorId)
    {
        var folder = await _folderRepo.GetByIdAndCompanyAsync(folderId, companyId);
        if (folder == null) return false;

        var file = await _fileRepo.GetByIdAndFolderAsync(fileId, folderId);
        if (file == null) return false;

        try { await _storage.DeleteFileAsync(file.BlobPath); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not delete blob for file Id {FileId}", fileId); }

        file.IsDeleted = true;
        file.DeletedAt = DateTime.UtcNow;
        file.DeletedActorId = deleterActorId;
        _fileRepo.Update(file);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("File {Id} soft-deleted from folder {FolderId}", fileId, folderId);
        return true;
    }

    // ─── Mapping ──────────────────────────────────────────────────────────────

    private static FolderResponseDto MapFolderToDto(Folders f, int fileCount, List<FolderTypes> folderTypes)
    {
        var typeName = folderTypes.FirstOrDefault(t => t.Id == f.FolderTypeId)?.Name;
        return new FolderResponseDto
        {
            Id = f.Id,
            Name = f.Name,
            FolderTypeId = f.FolderTypeId,
            FolderTypeName = typeName,
            CompanyId = f.CompanyId,
            CreatedByActorId = f.CreatedByActorId,
            OwnerActorId = f.OwnerActorId,
            MenuId = f.MenuId,
            SectionId = f.SectionId,
            ParentFolderId = f.ParentFolderId,
            Path = f.Path,
            Level = f.Level,
            IsGlobal = f.IsGlobal,
            CreatedAt = f.CreatedAt,
            IsActive = f.IsActive,
            FileCount = fileCount
        };
    }

    private FileResponseDto MapFileToDto(Files f, long size)
    {
        return new FileResponseDto
        {
            Id = f.Id,
            FolderId = f.FolderId,
            FileName = f.FileName,
            FileSize = size,
            ContentType = _storage.ResolveContentType(f.FileName),
            UploadedByActorId = f.UploadedByActorId,
            UploadedAt = f.UploadedAt
        };
    }
}
