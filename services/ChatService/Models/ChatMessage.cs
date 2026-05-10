namespace ChatService.Models;

public sealed record ChatMessage(
    Guid Id,
    Guid RoomId,
    string SenderUserId,
    string SenderName,
    string Content,
    DateTime SentAtUtc);