namespace ChatService.Models;

public sealed record ChatParticipant(
    string UserId,
    string DisplayName,
    DateTime LastSeenUtc);