using System.Text.Json.Serialization;

namespace ChatService.Models;

public sealed record ChatRoom(
    Guid Id,
    string Name,
    string CreatedByUserId,
    DateTime CreatedAtUtc,
    bool IsPasswordProtected,
    [property: JsonIgnore] string? PasswordHash,
    [property: JsonIgnore] string? PasswordSalt);