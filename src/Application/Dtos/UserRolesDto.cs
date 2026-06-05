namespace BuilderAssistantApi.Application.Dtos;

public sealed record UserRolesDto(
    long UserId,
    IReadOnlyList<string> Roles
);
