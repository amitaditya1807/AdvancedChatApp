using ChatService.Contracts;
using ChatService.Models;

namespace ChatService.Services.Interfaces;

public interface IChatRoomService
{
    Task<IReadOnlyCollection<ChatRoom>> GetRoomsAsync(string userId, CancellationToken cancellationToken = default);

    Task<ChatRoom> CreateRoomAsync(string userId, CreateRoomRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ChatMessage>> GetMessagesAsync(Guid roomId, string userId, CancellationToken cancellationToken = default);

    Task<ChatMessage> SendMessageAsync(Guid roomId, string userId, string senderName, SendMessageRequest request, CancellationToken cancellationToken = default);
}