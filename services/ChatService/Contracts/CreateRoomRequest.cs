namespace ChatService.Contracts;

public sealed record CreateRoomRequest(string Name, string Password);