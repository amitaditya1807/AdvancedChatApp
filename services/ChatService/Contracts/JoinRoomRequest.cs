namespace ChatService.Contracts;

public sealed class JoinRoomRequest
{
    public string RoomKey { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}