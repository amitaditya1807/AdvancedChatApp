namespace ChatService.Models;

public sealed record ChatRoom(
    Guid Id,
    string Name,
    string CreatedByUserId,
    DateTime CreatedAtUtc);