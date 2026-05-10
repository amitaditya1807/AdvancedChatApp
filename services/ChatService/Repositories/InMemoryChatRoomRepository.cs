using ChatService.Models;
using ChatService.Repositories.Interfaces;
using System.Collections.Concurrent;

namespace ChatService.Repositories;

public sealed class InMemoryChatRoomRepository : IChatRoomRepository
{
    private readonly ConcurrentDictionary<Guid, ChatRoom> _rooms = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<ChatMessage>> _messages = new();

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
    }

    public Task<IReadOnlyCollection<ChatRoom>> GetRoomsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var rooms = _rooms.Values
            .OrderBy(room => room.CreatedAtUtc)
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<ChatRoom>>(rooms);
    }

    public Task<ChatRoom?> GetRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        _rooms.TryGetValue(roomId, out var room);

        return Task.FromResult(room);
    }

    public Task<ChatRoom> CreateRoomAsync(ChatRoom room, CancellationToken cancellationToken = default)
    {
        _rooms[room.Id] = room;
        _messages.TryAdd(room.Id, new ConcurrentQueue<ChatMessage>());

        return Task.FromResult(room);
    }

    public Task<bool> DeleteRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var roomDeleted = _rooms.TryRemove(roomId, out _);

        if (roomDeleted)
        {
            _messages.TryRemove(roomId, out _);
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
}