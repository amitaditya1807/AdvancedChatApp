using ChatService.Contracts;
using ChatService.Models;

namespace ChatService.Services.Interfaces;

public interface IChatRoomService
{
    Task<IReadOnlyCollection<ChatRoom>> GetRoomsAsync(string userId, CancellationToken cancellationToken = default);

    Task<ChatRoom> CreateRoomAsync(string userId, string senderName, CreateRoomRequest request, CancellationToken cancellationToken = default);

    Task<ChatRoom> JoinRoomAsync(string roomKey, string userId, string senderName, string? password, CancellationToken cancellationToken = default);

    Task DeleteRoomAsync(Guid roomId, string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ChatMessage>> GetMessagesAsync(Guid roomId, string userId, string? password, CancellationToken cancellationToken = default);

    Task<ChatMessage> SendMessageAsync(Guid roomId, string userId, string senderName, string? password, SendMessageRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ChatParticipant>> GetParticipantsAsync(Guid roomId, string? password, CancellationToken cancellationToken = default);

    Task TouchParticipantAsync(Guid roomId, string userId, string senderName, string? password, CancellationToken cancellationToken = default);
}