using ChatService.Contracts;
using ChatService.Services.Interfaces;
using System.Security.Claims;

namespace ChatService.Endpoints;

public static class ChatRoomEndpoints
{
    public static RouteGroupBuilder MapChatRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/chat/rooms")
            .RequireAuthorization()
            .WithTags("Chat Rooms");

        group.MapGet("/", async (
            HttpContext context,
            IChatRoomService chatRoomService,
            CancellationToken cancellationToken) =>
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            var rooms = await chatRoomService.GetRoomsAsync(
                userId,
                cancellationToken);

            return Results.Ok(rooms);
        });

        group.MapPost("/", async (
            CreateRoomRequest request,
            HttpContext context,
            IChatRoomService chatRoomService,
            CancellationToken cancellationToken) =>
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var room = await chatRoomService.CreateRoomAsync(
                    userId,
                    request,
                    cancellationToken);

                return Results.Created(
                    $"/chat/rooms/{room.Id}",
                    room);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new
                {
                    error = ex.Message
                });
            }
        });

        return group;
    }
}