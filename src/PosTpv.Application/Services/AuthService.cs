using AutoMapper;
using Microsoft.EntityFrameworkCore;
using PosTpv.Application.Common.Interfaces;
using PosTpv.Application.DTOs;
using PosTpv.Domain.Entities;

namespace PosTpv.Application.Services;

public interface IAuthService
{
    Task<UserDto?> AuthenticateAsync(string username, string password, CancellationToken ct = default);
    Task<List<UserDto>> GetUsersAsync(CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly IMapper _mapper;

    public AuthService(IUnitOfWork uow, IPasswordHasher hasher, IMapper mapper)
    {
        _uow = uow;
        _hasher = hasher;
        _mapper = mapper;
    }

    public async Task<UserDto?> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        var user = await _uow.Repository<User>().Query()
            .FirstOrDefaultAsync(u => u.Username == username && u.IsActive, ct);

        if (user is null || !_hasher.Verify(password, user.PasswordHash))
            return null;

        return _mapper.Map<UserDto>(user);
    }

    public async Task<List<UserDto>> GetUsersAsync(CancellationToken ct = default)
    {
        var users = await _uow.Repository<User>().Query()
            .OrderBy(u => u.Role).ThenBy(u => u.FullName).ToListAsync(ct);
        return _mapper.Map<List<UserDto>>(users);
    }
}
