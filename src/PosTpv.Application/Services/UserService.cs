using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;

namespace PosTpv.Application.Services;

public interface IUserService
{
    Task<List<UserDto>> GetAllAsync(CancellationToken ct = default);
    Task<UserFormDto?> GetForEditAsync(int id, CancellationToken ct = default);
    Task<int> CreateAsync(UserFormDto form, CancellationToken ct = default);
    Task UpdateAsync(UserFormDto form, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task UnlockAsync(int id, CancellationToken ct = default);
}

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IPasswordHasher _hasher;

    public UserService(IUnitOfWork uow, IMapper mapper, IPasswordHasher hasher)
    {
        _uow = uow;
        _mapper = mapper;
        _hasher = hasher;
    }

    public async Task<List<UserDto>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await _uow.Repository<User>().Query()
            .OrderBy(u => u.Role).ThenBy(u => u.FullName).ToListAsync(ct);
        return _mapper.Map<List<UserDto>>(users);
    }

    public async Task<UserFormDto?> GetForEditAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<User>().GetByIdAsync(id, ct);
        return entity is null ? null : _mapper.Map<UserFormDto>(entity);
    }

    public async Task<int> CreateAsync(UserFormDto form, CancellationToken ct = default)
    {
        await EnsureUsernameFreeAsync(form.Username, null, ct);
        if (string.IsNullOrWhiteSpace(form.Pin))
            throw new InvalidOperationException("A PIN is required for new users.");

        var entity = _mapper.Map<User>(form);
        entity.Id = 0;
        entity.PasswordHash = _hasher.Hash(form.Pin);
        await _uow.Repository<User>().AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task UpdateAsync(UserFormDto form, CancellationToken ct = default)
    {
        await EnsureUsernameFreeAsync(form.Username, form.Id, ct);

        var entity = await _uow.Repository<User>().GetByIdAsync(form.Id, ct)
            ?? throw new KeyNotFoundException($"User {form.Id} not found.");
        _mapper.Map(form, entity);
        if (!string.IsNullOrWhiteSpace(form.Pin))
            entity.PasswordHash = _hasher.Hash(form.Pin);
        _uow.Repository<User>().Update(entity);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<User>().GetByIdAsync(id, ct);
        if (entity is null) return;
        try
        {
            _uow.Repository<User>().Remove(entity);
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The user has orders on record (Order.WaiterId is a Restrict FK) — deactivating
            // instead of deleting keeps history intact and blocks further sign-ins.
            throw new InvalidOperationException(
                "This user has orders on record and can't be deleted. Deactivate it instead.");
        }
    }

    public async Task UnlockAsync(int id, CancellationToken ct = default)
    {
        var entity = await _uow.Repository<User>().GetByIdAsync(id, ct);
        if (entity is null) return;
        entity.FailedLoginAttempts = 0;
        entity.LockedUntil = null;
        _uow.Repository<User>().Update(entity);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task EnsureUsernameFreeAsync(string username, int? excludingId, CancellationToken ct)
    {
        var query = _uow.Repository<User>().Query().Where(u => u.Username == username);
        if (excludingId is int id)
            query = query.Where(u => u.Id != id);
        var taken = await query.AnyAsync(ct);
        if (taken)
            throw new InvalidOperationException($"Username \"{username}\" is already in use.");
    }
}
