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

    public async Task<ChatRoom> CreateRoomAsync(string userId, string senderName, CreateRoomRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Room name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Room password is required.", nameof(request));
        }

        var roomName = request.Name.Trim();
        var existingRoom = await _chatRoomRepository.GetRoomByNameAsync(roomName, cancellationToken);

        if (existingRoom is not null)
        {
            throw new ArgumentException("Room name already exists. Enter the correct password to join this room.", nameof(request));
        }

        var salt = CreateSalt();
        var room = new ChatRoom(
            Guid.NewGuid(),
            roomName,
            userId,
            DateTime.UtcNow,
            true,
            HashPassword(request.Password, salt),
            salt);

        var createdRoom = await _chatRoomRepository.CreateRoomAsync(room, cancellationToken);
        await _chatRoomRepository.AddParticipantAsync(createdRoom.Id, userId, NormalizeSenderName(senderName), cancellationToken);

        return createdRoom;
    }

    public async Task<ChatRoom> JoinRoomAsync(string roomKey, string userId, string senderName, string? password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roomKey))
        {
            throw new ArgumentException("Room ID or name is required.", nameof(roomKey));
        }

        var room = await GetRoomByKeyAsync(roomKey.Trim(), cancellationToken);

        if (room is null)
        {
            throw new KeyNotFoundException($"Room '{roomKey}' was not found.");
        }

        EnsureRoomAccess(room, password);
        await _chatRoomRepository.AddParticipantAsync(room.Id, userId, NormalizeSenderName(senderName), cancellationToken);

        return room;
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

        var normalizedSenderName = NormalizeSenderName(senderName);
        await _chatRoomRepository.AddParticipantAsync(roomId, userId, normalizedSenderName, cancellationToken);

        var message = new ChatMessage(
            Guid.NewGuid(),
            roomId,
            userId,
            normalizedSenderName,
            request.Content.Trim(),
            DateTime.UtcNow);

        return await _chatRoomRepository.AddMessageAsync(message, cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetParticipantsAsync(Guid roomId, string? password, CancellationToken cancellationToken = default)
    {
        await EnsureRoomAccessAsync(roomId, password, cancellationToken);

        return await _chatRoomRepository.GetParticipantsAsync(roomId, cancellationToken);
    }

    private async Task<ChatRoom?> GetRoomByKeyAsync(string roomKey, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(roomKey, out var roomId))
        {
            return await _chatRoomRepository.GetRoomAsync(roomId, cancellationToken);
        }

        return await _chatRoomRepository.GetRoomByNameAsync(roomKey, cancellationToken);
    }

    private async Task<ChatRoom> EnsureRoomAccessAsync(Guid roomId, string? password, CancellationToken cancellationToken)
    {
        var room = await _chatRoomRepository.GetRoomAsync(roomId, cancellationToken);

        if (room is null)
        {
            throw new KeyNotFoundException($"Room '{roomId}' was not found.");
        }

        EnsureRoomAccess(room, password);

        return room;
    }

    private static void EnsureRoomAccess(ChatRoom room, string? password)
    {
        if (!room.IsPasswordProtected)
        {
            return;
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
    }

    private static string NormalizeSenderName(string senderName)
    {
        return string.IsNullOrWhiteSpace(senderName) ? "Chat user" : senderName.Trim();
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