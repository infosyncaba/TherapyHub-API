using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TherapuHubAPI.DTOs.Requests.Events;
using TherapuHubAPI.DTOs.Responses.Events;
using TherapuHubAPI.Models;
using TherapuHubAPI.Repositorio;
using TherapuHubAPI.Repositorio.IRepositorio;
using TherapuHubAPI.Services.IServices;

namespace TherapuHubAPI.Services;

public class EventosService : IEventosService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUsuarioRepositorio _usuarioRepositorio;
    private readonly IMapper _mapper;
    private readonly ContextDB _context;

    public EventosService(IUnitOfWork unitOfWork, IUsuarioRepositorio usuarioRepositorio, IMapper mapper, ContextDB context)
    {
        _unitOfWork = unitOfWork;
        _usuarioRepositorio = usuarioRepositorio;
        _mapper = mapper;
        _context = context;
    }

    public async Task<IEnumerable<EventoResponseDto>> GetEventosByUserAsync(int userId, DateTime? start, DateTime? end, bool? esTodoElDia = null)
    {
        var user = await _usuarioRepositorio.GetByIdAsync(userId);
        if (user == null) return Array.Empty<EventoResponseDto>();

        var eventos = await _unitOfWork.Events.GetEventosByUserAsync(userId, user.Actor.CompanyId, start, end, esTodoElDia);
        
        // Obtener todos los tipos de evento de una vez para evitar múltiples consultas
        var tiposEventoIds = eventos.Select(e => e.EventTypeId).Distinct().ToList();
        var tiposEvento = await _unitOfWork.EventTypes.FindAsync(t => tiposEventoIds.Contains(t.Id));
        var tiposEventoDict = tiposEvento.ToDictionary(t => t.Id);

        var eventosDto = eventos.Select(evento =>
        {
            var eventoDto = _mapper.Map<EventoResponseDto>(evento);
            if (tiposEventoDict.TryGetValue(evento.EventTypeId, out var tipoEvento))
            {
                eventoDto.TipoEventoNombre = tipoEvento.Name;
                eventoDto.TipoEventoColor = tipoEvento.Color;
            }
            return eventoDto;
        }).ToList();

        return eventosDto;
    }

    public async Task<EventoResponseDto?> GetEventoByIdAsync(int id, int currentUserId)
    {
        var evento = await _unitOfWork.Events.GetByIdAsync(id);
        if (evento == null) return null;

        var user = await _usuarioRepositorio.GetByIdAsync(currentUserId);
        if (user == null) return null;

        if (evento.IsGlobal)
        {
            if (evento.CompanyId != user.Actor.CompanyId) return null;
        }
        else
        {
            var isAssigned = (await _unitOfWork.EventoUsuarios.FindAsync(eu => eu.EventId == id && eu.UserId == currentUserId)).Any();
            if (!isAssigned) return null;
        }

        var tipoEvento = await _unitOfWork.EventTypes.GetByIdAsync(evento.EventTypeId);
        var eventoDto = _mapper.Map<EventoResponseDto>(evento);
        eventoDto.TipoEventoNombre = tipoEvento?.Name;
        eventoDto.TipoEventoColor = tipoEvento?.Color;
        return eventoDto;
    }

    public async Task<EventoResponseDto> CreateEventoAsync(CreateEventoRequestDto request, int currentUserId)
    {
        var user = await _usuarioRepositorio.GetByIdAsync(currentUserId);
        if (user == null) throw new InvalidOperationException("Current user not found.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var evento = _mapper.Map<Events>(request);
            evento.CreatedAt = DateTime.UtcNow;
            evento.CompanyId = user.Actor.CompanyId;

            await _unitOfWork.Events.AddAsync(evento);
            await _unitOfWork.SaveChangesAsync();

            if (!request.IsGlobal && request.UsuariosIds != null && request.UsuariosIds.Any())
            {
                var eventoUsuarios = request.UsuariosIds
                    .Distinct()
                    .Select(userId => new EventUsers
                    {
                        EventId = evento.Id,
                        UserId = userId
                    });
                await _unitOfWork.EventoUsuarios.AddRangeAsync(eventoUsuarios);
                await _unitOfWork.SaveChangesAsync();
            }

            await _unitOfWork.CommitTransactionAsync();
            
            var tipoEvento = await _unitOfWork.EventTypes.GetByIdAsync(evento.EventTypeId);
            var eventoDto = _mapper.Map<EventoResponseDto>(evento);
            eventoDto.TipoEventoNombre = tipoEvento?.Name;
            eventoDto.TipoEventoColor = tipoEvento?.Color;
            return eventoDto;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<bool> UpdateEventoAsync(int id, UpdateEventoRequestDto request, int currentUserId)
    {
        var evento = await _unitOfWork.Events.GetByIdAsync(id);
        if (evento == null) return false;

        var user = await _usuarioRepositorio.GetByIdAsync(currentUserId);
        if (user == null || evento.CompanyId != user.Actor.CompanyId) return false;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            _mapper.Map(request, evento);
            _unitOfWork.Events.Update(evento);
            await _unitOfWork.SaveChangesAsync();

            // Actualizar usuarios asignados
            var usuariosActuales = await _unitOfWork.EventoUsuarios.FindAsync(eu => eu.EventId == id);
            _unitOfWork.EventoUsuarios.RemoveRange(usuariosActuales);
            await _unitOfWork.SaveChangesAsync();

            if (!request.IsGlobal && request.UsuariosIds != null && request.UsuariosIds.Any())
            {
                var eventoUsuarios = request.UsuariosIds
                    .Distinct()
                    .Select(userId => new EventUsers
                    {
                        EventId = id,
                        UserId = userId
                    });
                await _unitOfWork.EventoUsuarios.AddRangeAsync(eventoUsuarios);
                await _unitOfWork.SaveChangesAsync();
            }

            await _unitOfWork.CommitTransactionAsync();
            return true;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<bool> DeleteEventoAsync(int id, int currentUserId)
    {
        var evento = await _unitOfWork.Events.GetByIdAsync(id);
        if (evento == null) return false;

        var user = await _usuarioRepositorio.GetByIdAsync(currentUserId);
        if (user == null || evento.CompanyId != user.Actor.CompanyId) return false;

        _unitOfWork.Events.Remove(evento);
        return await _unitOfWork.SaveChangesAsync() > 0;
    }

    /// <summary>
    /// Returns public (IsPrivate != true) events for a target user.
    /// Requires a type-2 (USER_DELEGATE) relationship from requesterActorId → targetActorId.
    /// </summary>
    public async Task<IEnumerable<EventoResponseDto>> GetDelegatedCalendarAsync(
        int currentUserId, int targetActorId, DateTime? start, DateTime? end)
    {
        const int typeUserDelegate = 2;

        // Resolve requester's actorId from userId
        var requester = await _usuarioRepositorio.GetByIdAsync(currentUserId);
        if (requester == null) return Array.Empty<EventoResponseDto>();

        // Security: verify the requester has a type-2 relationship with the target
        var relationshipExists = await _context.ActorRelationships
            .AnyAsync(r => r.SourceActorId == requester.ActorId
                        && r.TargetActorId == targetActorId
                        && r.RelationshipTypeId == typeUserDelegate);

        if (!relationshipExists) return Array.Empty<EventoResponseDto>();

        // Resolve target's userId and companyId
        var targetUser = await _context.Users
            .Join(_context.Actors, u => u.ActorId, a => a.Id, (u, a) => new { User = u, Actor = a })
            .FirstOrDefaultAsync(x => x.Actor.Id == targetActorId && !x.Actor.IsDeleted);

        if (targetUser == null) return Array.Empty<EventoResponseDto>();

        var eventos = await _unitOfWork.Events.GetPublicEventosByUserAsync(
            targetUser.User.Id, targetUser.Actor.CompanyId, start, end);

        var typeIds = eventos.Select(e => e.EventTypeId).Distinct().ToList();
        var types = (await _unitOfWork.EventTypes.FindAsync(t => typeIds.Contains(t.Id)))
            .ToDictionary(t => t.Id);

        return eventos.Select(evento =>
        {
            var dto = _mapper.Map<EventoResponseDto>(evento);
            if (types.TryGetValue(evento.EventTypeId, out var t))
            {
                dto.TipoEventoNombre = t.Name;
                dto.TipoEventoColor = t.Color;
            }
            return dto;
        }).ToList();
    }
}
