using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TherapuHubAPI.DTOs.Requests.Menus;
using TherapuHubAPI.DTOs.Responses.Menus;
using TherapuHubAPI.Models;
using TherapuHubAPI.Repositorio;
using TherapuHubAPI.Repositorio.IRepositorio;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services.Implementations;

public class MenuService : IMenuService
{
    private readonly IMenuRepositorio _menuRepositorio;
    private readonly ITipoUsuarioRepositorio _tipoUsuarioRepositorio;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<MenuService> _logger;
    private readonly ContextDB _context;

    public MenuService(
        IMenuRepositorio menuRepositorio,
        ITipoUsuarioRepositorio tipoUsuarioRepositorio,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<MenuService> logger,
        ContextDB context)
    {
        _menuRepositorio = menuRepositorio;
        _tipoUsuarioRepositorio = tipoUsuarioRepositorio;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _context = context;
    }

    public async Task<IEnumerable<MenuResponseDto>> GetAllAsync()
    {
        _logger.LogInformation("Obteniendo todos los menús");
        var menus = await _menuRepositorio.GetAllAsync();
        var dtos = _mapper.Map<IEnumerable<MenuResponseDto>>(menus.OrderBy(m => m.SortOrder)).ToList();
        foreach (var d in dtos) d.Children = new List<MenuResponseDto>();
        return dtos;
    }

    public async Task<MenuResponseDto?> GetByIdAsync(int id)
    {
        var menu = await _menuRepositorio.GetByIdAsync(id);
        return menu == null ? null : _mapper.Map<MenuResponseDto>(menu);
    }

    public async Task<IEnumerable<MenuResponseDto>> GetMenusByTipoUsuarioIdAsync(int tipoUsuarioId)
    {
        _logger.LogInformation("Obteniendo menús para tipo de usuario Id: {UserTypeId}", tipoUsuarioId);
        return await BuildMenuTreeForTipoUsuarioAsync(tipoUsuarioId);
    }

    public async Task<TipoUsuarioConMenusResponseDto> GetTipoUsuarioConMenusAsync(int tipoUsuarioId)
    {
        _logger.LogInformation("Obteniendo tipo de usuario con menús para Id: {UserTypeId}", tipoUsuarioId);

        var tipoUsuario = await _tipoUsuarioRepositorio.GetByIdAsync(tipoUsuarioId);
        if (tipoUsuario == null)
        {
            throw new InvalidOperationException($"User type with Id {tipoUsuarioId} not found");
        }

        var menus = await _menuRepositorio.GetMenusByTipoUsuarioIdAsync(tipoUsuarioId);

        return new TipoUsuarioConMenusResponseDto
        {
            UserTypeId = tipoUsuario.Id,
            TipoUsuarioNombre = tipoUsuario.Name,
            Menus = _mapper.Map<List<MenuResponseDto>>(menus)
        };
    }

    public async Task AsignarMenusAsync(AsignarMenusRequestDto request)
    {
        _logger.LogInformation("Asignando menús al tipo de usuario Id: {UserTypeId}", request.UserTypeId);

        var tipoUsuario = await _tipoUsuarioRepositorio.GetByIdAsync(request.UserTypeId);
        if (tipoUsuario == null)
        {
            throw new InvalidOperationException($"Tipo de usuario con Id {request.UserTypeId} no encontrado");
        }

        var todosLosMenus = await _menuRepositorio.GetAllAsync();
        var menuIdsExistentes = todosLosMenus.Select(m => m.Id).ToList();
        var menusNoExistentes = request.MenuIds.Except(menuIdsExistentes).ToList();

        if (menusNoExistentes.Any())
        {
            throw new InvalidOperationException($"The following menus do not exist: {string.Join(", ", menusNoExistentes)}");
        }

        var asignacionesExistentes = await _context.UserTypeMenus
            .Where(tum => tum.UserTypeId == request.UserTypeId)
            .ToListAsync();

        _context.UserTypeMenus.RemoveRange(asignacionesExistentes);

        var nuevasAsignaciones = request.MenuIds.Select(menuId => new UserTypeMenus
        {
            UserTypeId = request.UserTypeId,
            MenuId = menuId,
            AssignedAt = DateTime.UtcNow
        }).ToList();

        await _context.UserTypeMenus.AddRangeAsync(nuevasAsignaciones);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Menús asignados exitosamente al tipo de usuario Id: {UserTypeId}", request.UserTypeId);
    }

    public async Task<IEnumerable<MenuResponseDto>> GetMenusUsuarioActualAsync(int tipoUsuarioId)
    {
        _logger.LogInformation("Obteniendo menús para usuario con tipo de usuario Id: {UserTypeId}", tipoUsuarioId);
        return await BuildMenuTreeForTipoUsuarioAsync(tipoUsuarioId);
    }

    public async Task<MenuResponseDto> CreateAsync(CreateMenuRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Titulo))
            throw new InvalidOperationException("Title is required.");

        var ruta = string.IsNullOrWhiteSpace(request.Ruta) ? "#" : request.Ruta.Trim();
        if (ruta != "#" && await _menuRepositorio.ExistsByRutaAsync(ruta))
            throw new InvalidOperationException($"A menu with route '{ruta}' already exists.");

        if (request.ParentId.HasValue)
        {
            var parent = await _menuRepositorio.GetByIdAsync(request.ParentId.Value);
            if (parent == null)
                throw new InvalidOperationException($"Parent menu with Id {request.ParentId} not found.");
        }

        var menu = new Menus
        {
            Title = request.Titulo.Trim(),
            Route = ruta,
            Icon = request.Icono?.Trim(),
            SortOrder = request.Orden,
            ParentId = request.ParentId,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };
        await _menuRepositorio.AddAsync(menu);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Menu created with Id {Id}", menu.Id);
        return _mapper.Map<MenuResponseDto>(menu);
    }

    public async Task<MenuResponseDto> UpdateAsync(int id, UpdateMenuRequestDto request)
    {
        var menu = await _menuRepositorio.GetByIdAsync(id);
        if (menu == null)
            throw new InvalidOperationException($"Menu with Id {id} not found.");

        if (string.IsNullOrWhiteSpace(request.Titulo))
            throw new InvalidOperationException("Title is required.");

        var ruta = string.IsNullOrWhiteSpace(request.Ruta) ? "#" : request.Ruta.Trim();
        if (ruta != "#" && await _menuRepositorio.ExistsByRutaAsync(ruta, id))
            throw new InvalidOperationException($"A menu with route '{ruta}' already exists.");

        if (request.ParentId.HasValue)
        {
            if (request.ParentId.Value == id)
                throw new InvalidOperationException("A menu cannot be its own parent.");
            var parent = await _menuRepositorio.GetByIdAsync(request.ParentId.Value);
            if (parent == null)
                throw new InvalidOperationException($"Parent menu with Id {request.ParentId} not found.");
        }

        menu.Title = request.Titulo.Trim();
        menu.Route = ruta;
        menu.Icon = request.Icono?.Trim();
        menu.SortOrder = request.Orden;
        menu.ParentId = request.ParentId;
        menu.IsActive = request.IsActive;
        _menuRepositorio.Update(menu);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Menu updated: Id {Id}", id);
        return _mapper.Map<MenuResponseDto>(menu);
    }

    public async Task DeleteAsync(int id)
    {
        var menu = await _menuRepositorio.GetByIdAsync(id);
        if (menu == null)
            throw new InvalidOperationException($"Menu with Id {id} not found.");

        var hasChildren = await _context.Menus.AnyAsync(m => m.ParentId == id);
        if (hasChildren)
            throw new InvalidOperationException("Cannot delete a menu that has submenus. Remove or reassign submenus first.");

        var asignaciones = await _context.UserTypeMenus.Where(tum => tum.MenuId == id).ToListAsync();
        _context.UserTypeMenus.RemoveRange(asignaciones);
        _menuRepositorio.Remove(menu);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Menu deleted: Id {Id}", id);
    }

    public async Task MoveUpAsync(int id)
    {
        var menu = await _menuRepositorio.GetByIdAsync(id);
        if (menu == null)
            throw new InvalidOperationException($"Menu with Id {id} not found.");

        var siblings = (await _context.Menus
            .Where(m => m.ParentId == menu.ParentId)
            .OrderBy(m => m.SortOrder)
            .ToListAsync()).ToList();

        var idx = siblings.FindIndex(m => m.Id == id);
        if (idx <= 0) return;

        // New order: swap item at idx with item at idx-1, then assign explicit Orden 0,1,2,...
        (siblings[idx], siblings[idx - 1]) = (siblings[idx - 1], siblings[idx]);
        for (var i = 0; i < siblings.Count; i++)
        {
            siblings[i].SortOrder = i;
            _menuRepositorio.Update(siblings[i]);
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("Menu Id {Id} moved up.", id);
    }

    public async Task MoveDownAsync(int id)
    {
        var menu = await _menuRepositorio.GetByIdAsync(id);
        if (menu == null)
            throw new InvalidOperationException($"Menu with Id {id} not found.");

        var siblings = (await _context.Menus
            .Where(m => m.ParentId == menu.ParentId)
            .OrderBy(m => m.SortOrder)
            .ToListAsync()).ToList();

        var idx = siblings.FindIndex(m => m.Id == id);
        if (idx < 0 || idx >= siblings.Count - 1) return;

        // New order: swap item at idx with item at idx+1, then assign explicit Orden 0,1,2,...
        (siblings[idx], siblings[idx + 1]) = (siblings[idx + 1], siblings[idx]);
        for (var i = 0; i < siblings.Count; i++)
        {
            siblings[i].SortOrder = i;
            _menuRepositorio.Update(siblings[i]);
        }
        await _context.SaveChangesAsync();
        _logger.LogInformation("Menu Id {Id} moved down.", id);
    }

    private async Task<List<MenuResponseDto>> BuildMenuTreeForTipoUsuarioAsync(int tipoUsuarioId)
    {
        var allMenus = (await _menuRepositorio.GetAllAsync()).Where(m => m.IsActive).OrderBy(m => m.SortOrder).ToList();
        var assigned = await _menuRepositorio.GetMenusByTipoUsuarioIdAsync(tipoUsuarioId);
        var assignedIds = new HashSet<int>(assigned.Select(m => m.Id));

        var effectiveIds = new HashSet<int>(assignedIds);
        foreach (var menu in assigned)
        {
            var current = menu;
            while (current?.ParentId != null)
            {
                effectiveIds.Add(current.ParentId.Value);
                current = allMenus.FirstOrDefault(m => m.Id == current.ParentId.Value);
            }
        }

        var dict = allMenus.Where(m => effectiveIds.Contains(m.Id))
            .ToDictionary(m => m.Id, m => _mapper.Map<MenuResponseDto>(m));
        foreach (var d in dict.Values) d.Children = new List<MenuResponseDto>();

        foreach (var kv in dict)
        {
            var dto = kv.Value;
            if (dto.ParentId.HasValue && dict.TryGetValue(dto.ParentId.Value, out var parent))
                parent.Children!.Add(dto);
        }

        return dict.Values.Where(d => !d.ParentId.HasValue).OrderBy(d => d.Orden).ToList();
    }
}
