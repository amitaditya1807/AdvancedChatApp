using ChatService.Contracts;
using ChatService.Models;
using ChatService.Repositories.Interfaces;
using ChatService.Services.Interfaces;

namespace ChatService.Services;

public sealed class ChatRoomService : IChatRoomService
{
    private readonly IChatRoomRepository _chatRoomRepository;

    public ChatRoomService(IChatRoomRepository chatRoomRepository)
    {
        _chatRoomRepository = chatRoomRepository;
    }

    public Task<IReadOnlyCollection<ChatRoom>> GetRoomsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return _chatRoomRepository.GetRoomsAsync(userId, cancellationToken);
    }

    public Task<ChatRoom> CreateRoomAsync(
        string userId,
        CreateRoomRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Room name is required.", nameof(request));
        }

        var room = new ChatRoom(
            Guid.NewGuid(),
            request.Name.Trim(),
            userId,
            DateTime.UtcNow);

        return _chatRoomRepository.CreateRoomAsync(room, cancellationToken);
    }

    public Task<IReadOnlyCollection<ChatMessage>> GetMessagesAsync(
        Guid roomId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return _chatRoomRepository.GetMessagesAsync(roomId, cancellationToken);
    }

    public Task<ChatMessage> SendMessageAsync(
        Guid roomId,
        string userId,
        string senderName,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Message content is required.", nameof(request));
        }

        var message = new ChatMessage(
            Guid.NewGuid(),
            roomId,
            userId,
            string.IsNullOrWhiteSpace(senderName) ? "Chat user" : senderName.Trim(),
            request.Content.Trim(),
            DateTime.UtcNow);

        return _chatRoomRepository.AddMessageAsync(message, cancellationToken);
    }
}