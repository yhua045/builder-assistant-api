using BuilderAssistantApi.Application.Dtos;
using BuilderAssistantApi.Application.Interfaces;
using BuilderAssistantApi.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace BuilderAssistantApi.Infrastructure.Services;

public sealed class RoleService : IRoleService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<long>> _roleManager;

    public RoleService(UserManager<User> userManager, RoleManager<IdentityRole<long>> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public Task<IReadOnlyList<string>> ListAllRolesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<string> roles = _roleManager.Roles.Select(r => r.Name!).ToList();
        return Task.FromResult(roles);
    }

    public async Task<UserRolesDto?> GetUserRolesAsync(long userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return null;

        var roles = await _userManager.GetRolesAsync(user);
        return new UserRolesDto(userId, roles.ToList());
    }

    public async Task<bool> AssignRoleAsync(long userId, string roleName, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return false;

        if (!await _roleManager.RoleExistsAsync(roleName))
            return false;

        var result = await _userManager.AddToRoleAsync(user, roleName);
        return result.Succeeded;
    }

    public async Task<bool> RemoveRoleAsync(long userId, string roleName, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return false;

        if (!await _userManager.IsInRoleAsync(user, roleName))
            return false;

        var result = await _userManager.RemoveFromRoleAsync(user, roleName);
        return result.Succeeded;
    }
}
