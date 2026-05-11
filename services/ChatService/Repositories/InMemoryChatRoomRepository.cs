using ChatService.Models;
using ChatService.Repositories.Interfaces;
using System.Collections.Concurrent;

namespace ChatService.Repositories;

public sealed class InMemoryChatRoomRepository : IChatRoomRepository
{
    private readonly ConcurrentDictionary<Guid, ChatRoom> _rooms = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<ChatMessage>> _messages = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, ChatParticipantPresence>> _participants = new();

    public InMemoryChatRoomRepository()
    {
        var generalRoom = new ChatRoom(
            Guid.NewGuid(),
            "General",
            "system",
            DateTime.UtcNow,
            false,
            null,
            null);

        _rooms[generalRoom.Id] = generalRoom;
        _messages[generalRoom.Id] = new ConcurrentQueue<ChatMessage>();
        _participants[generalRoom.Id] = new ConcurrentDictionary<string, ChatParticipantPresence>();
    }

    public Task<IReadOnlyCollection<ChatRoom>> GetRoomsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var rooms = _rooms.Values
            .Where(room => string.Equals(room.CreatedByUserId, userId, StringComparison.Ordinal))
            .OrderBy(room => room.CreatedAtUtc)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<ChatRoom>>(rooms);
    }

    public Task<ChatRoom?> GetRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        _rooms.TryGetValue(roomId, out var room);

        return Task.FromResult(room);
    }

    public Task<ChatRoom?> GetRoomByNameAsync(string roomName, CancellationToken cancellationToken = default)
    {
        var room = _rooms.Values.FirstOrDefault(room =>
            string.Equals(room.Name, roomName, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(room);
    }

    public Task<ChatRoom> CreateRoomAsync(ChatRoom room, CancellationToken cancellationToken = default)
    {
        _rooms[room.Id] = room;
        _messages.TryAdd(room.Id, new ConcurrentQueue<ChatMessage>());
        _participants.TryAdd(room.Id, new ConcurrentDictionary<string, ChatParticipantPresence>());

        return Task.FromResult(room);
    }

    public Task<bool> DeleteRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var roomDeleted = _rooms.TryRemove(roomId, out _);

        if (roomDeleted)
        {
            _messages.TryRemove(roomId, out _);
            _participants.TryRemove(roomId, out _);
        }

        return Task.FromResult(roomDeleted);
    }

    public Task<IReadOnlyCollection<ChatMessage>> GetMessagesAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        if (!_rooms.ContainsKey(roomId))
        {
            return Task.FromResult<IReadOnlyCollection<ChatMessage>>(Array.Empty<ChatMessage>());
        }

        var messages = _messages
            .GetOrAdd(roomId, _ => new ConcurrentQueue<ChatMessage>())
            .OrderBy(message => message.SentAtUtc)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<ChatMessage>>(messages);
    }

    public Task<ChatMessage> AddMessageAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        if (!_rooms.ContainsKey(message.RoomId))
        {
            throw new KeyNotFoundException($"Room '{message.RoomId}' was not found.");
        }

        _messages
            .GetOrAdd(message.RoomId, _ => new ConcurrentQueue<ChatMessage>())
            .Enqueue(message);

        return Task.FromResult(message);
    }

    public Task AddParticipantAsync(Guid roomId, string userId, string displayName, DateTime lastSeenUtc, CancellationToken cancellationToken = default)
    {
        if (!_rooms.ContainsKey(roomId))
        {
            throw new KeyNotFoundException($"Room '{roomId}' was not found.");
        }

        var presence = new ChatParticipantPresence(displayName, lastSeenUtc);

        _participants
            .GetOrAdd(roomId, _ => new ConcurrentDictionary<string, ChatParticipantPresence>())
            .AddOrUpdate(userId, presence, (_, _) => presence);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<ChatParticipant>> GetParticipantsAsync(Guid roomId, DateTime activeAfterUtc, CancellationToken cancellationToken = default)
    {
        if (!_rooms.ContainsKey(roomId))
        {
            return Task.FromResult<IReadOnlyCollection<ChatParticipant>>(Array.Empty<ChatParticipant>());
        }

        var participantMap = _participants
            .GetOrAdd(roomId, _ => new ConcurrentDictionary<string, ChatParticipantPresence>());

        foreach (var participant in participantMap)
        {
            if (participant.Value.LastSeenUtc < activeAfterUtc)
            {
                participantMap.TryRemove(participant.Key, out _);
            }
        }

        var participants = participantMap
            .Where(participant => participant.Value.LastSeenUtc >= activeAfterUtc && !string.IsNullOrWhiteSpace(participant.Value.DisplayName))
            .Select(participant => new ChatParticipant(
                participant.Key,
                participant.Value.DisplayName,
                participant.Value.LastSeenUtc))
            .OrderBy(participant => participant.DisplayName)
            .ThenBy(participant => participant.UserId)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<ChatParticipant>>(participants);
    }

    private sealed record ChatParticipantPresence(string DisplayName, DateTime LastSeenUtc);
}