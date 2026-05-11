using ChatService.Contracts;
using ChatService.Models;
using ChatService.Repositories.Interfaces;
using ChatService.Services.Interfaces;
using System.Security.Cryptography;

namespace ChatService.Services;

public sealed class ChatRoomService : IChatRoomService
{
    private readonly IChatRoomRepository _chatRoomRepository;

    public ChatRoomService(IChatRoomRepository chatRoomRepository)
    {
        _chatRoomRepository = chatRoomRepository;
    }

    public Task<IReadOnlyCollection<ChatRoom>> GetRoomsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return _chatRoomRepository.GetRoomsAsync(userId, cancellationToken);
    }

    public Task<ChatRoom> CreateRoomAsync(string userId, CreateRoomRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Room name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Room password is required.", nameof(request));
        }

        var salt = CreateSalt();
        var room = new ChatRoom(
            Guid.NewGuid(),
            request.Name.Trim(),
            userId,
            DateTime.UtcNow,
            true,
            HashPassword(request.Password, salt),
            salt);

        return _chatRoomRepository.CreateRoomAsync(room, cancellationToken);
    }

    public async Task<ChatRoom> JoinRoomAsync(Guid roomId, string? password, CancellationToken cancellationToken = default)
    {
        return await EnsureRoomAccessAsync(roomId, password, cancellationToken);
    }

    public async Task DeleteRoomAsync(Guid roomId, string userId, CancellationToken cancellationToken = default)
    {
        var room = await _chatRoomRepository.GetRoomAsync(roomId, cancellationToken);

        if (room is null)
        {
            throw new KeyNotFoundException($"Room '{roomId}' was not found.");
        }

        if (!string.Equals(room.CreatedByUserId, userId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Only the room owner can delete this room.");
        }

        await _chatRoomRepository.DeleteRoomAsync(roomId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ChatMessage>> GetMessagesAsync(Guid roomId, string userId, string? password, CancellationToken cancellationToken = default)
    {
        await EnsureRoomAccessAsync(roomId, password, cancellationToken);

        return await _chatRoomRepository.GetMessagesAsync(roomId, cancellationToken);
    }

    public async Task<ChatMessage> SendMessageAsync(Guid roomId, string userId, string senderName, string? password, SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ArgumentException("Message content is required.", nameof(request));
        }

        await EnsureRoomAccessAsync(roomId, password, cancellationToken);

        var message = new ChatMessage(
            Guid.NewGuid(),
            roomId,
            userId,
            string.IsNullOrWhiteSpace(senderName) ? "Chat user" : senderName.Trim(),
            request.Content.Trim(),
            DateTime.UtcNow);

        return await _chatRoomRepository.AddMessageAsync(message, cancellationToken);
    }

    private async Task<ChatRoom> EnsureRoomAccessAsync(Guid roomId, string? password, CancellationToken cancellationToken)
    {
        var room = await _chatRoomRepository.GetRoomAsync(roomId, cancellationToken);

        if (room is null)
        {
            throw new KeyNotFoundException($"Room '{roomId}' was not found.");
        }

        if (!room.IsPasswordProtected)
        {
            return room;
        }

        if (string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(room.PasswordHash)
            || string.IsNullOrWhiteSpace(room.PasswordSalt)
            || !CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(room.PasswordHash),
                Convert.FromHexString(HashPassword(password, room.PasswordSalt))))
        {
            throw new UnauthorizedAccessException("Room password is required or incorrect.");
        }

        return room;
    }

    private static string CreateSalt()
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);

        return Convert.ToHexString(saltBytes);
    }

    private static string HashPassword(string password, string salt)
    {
        return Convert.ToHexString(Rfc2898DeriveBytes.Pbkdf2(
            password,
            Convert.FromHexString(salt),
            100_000,
            HashAlgorithmName.SHA256,
            32));
    }
}