using Microsoft.AspNetCore.Identity;

namespace BuilderAssistantApi.Domain.Entities;

public class UserRole : IdentityRole<long>
{
    public UserRole() { }

    public UserRole(string roleName) : base(roleName) { }
}
