using ChatService.Models;

namespace ChatService.Repositories.Interfaces;

public interface IChatRoomRepository
{
    Task<IReadOnlyCollection<ChatRoom>> GetRoomsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<ChatRoom> CreateRoomAsync(
        ChatRoom room,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ChatMessage>> GetMessagesAsync(
        Guid roomId,
        CancellationToken cancellationToken = default);

    Task<ChatMessage> AddMessageAsync(
        ChatMessage message,
        CancellationToken cancellationToken = default);
}