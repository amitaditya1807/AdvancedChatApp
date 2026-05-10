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
            var userId = GetUserId(context);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            var rooms = await chatRoomService.GetRoomsAsync(userId, cancellationToken);

            return Results.Ok(rooms);
        });

        group.MapPost("/", async (
            CreateRoomRequest request,
            HttpContext context,
            IChatRoomService chatRoomService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(context);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var room = await chatRoomService.CreateRoomAsync(userId, request, cancellationToken);

                return Results.Created($"/chat/rooms/{room.Id}", room);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new
                {
                    error = ex.Message
                });
            }
        });

        group.MapGet("/{roomId:guid}/messages", async (
            Guid roomId,
            HttpContext context,
            IChatRoomService chatRoomService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(context);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            var messages = await chatRoomService.GetMessagesAsync(roomId, userId, cancellationToken);

            return Results.Ok(messages);
        });

        group.MapPost("/{roomId:guid}/messages", async (
            Guid roomId,
            SendMessageRequest request,
            HttpContext context,
            IChatRoomService chatRoomService,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(context);
            var senderName = GetSenderName(context);

            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var message = await chatRoomService.SendMessageAsync(
                    roomId,
                    userId,
                    senderName,
                    request,
                    cancellationToken);

                return Results.Created(
                    $"/chat/rooms/{roomId}/messages/{message.Id}",
                    message);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new
                {
                    error = ex.Message
                });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new
                {
                    error = ex.Message
                });
            }
        });

        return group;
    }

    private static string? GetUserId(HttpContext context)
    {
        return context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private static string GetSenderName(HttpContext context)
    {
        return context.User.FindFirst(ClaimTypes.Name)?.Value
            ?? context.User.FindFirst(ClaimTypes.Email)?.Value
            ?? "Chat user";
    }
}