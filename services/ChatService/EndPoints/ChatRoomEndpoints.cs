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
                var room = await chatRoomService.CreateRoomAsync(
                    userId,
                    GetSenderName(context),
                    request,
                    cancellationToken);

                return Results.Created($"/chat/rooms/{room.Id}", room);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/join", async (
            JoinRoomRequest request,
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
                var room = await chatRoomService.JoinRoomAsync(
                    request.RoomKey,
                    userId,
                    GetSenderName(context),
                    request.Password,
                    cancellationToken);

                return Results.Ok(room);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        });

        group.MapPost("/{roomId:guid}/join", async (
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

            try
            {
                var room = await chatRoomService.JoinRoomAsync(
                    roomId.ToString(),
                    userId,
                    GetSenderName(context),
                    GetRoomPassword(context),
                    cancellationToken);

                return Results.Ok(room);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        });

        group.MapDelete("/{roomId:guid}", async (
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

            try
            {
                await chatRoomService.DeleteRoomAsync(roomId, userId, cancellationToken);

                return Results.NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
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

            try
            {
                var messages = await chatRoomService.GetMessagesAsync(
                    roomId,
                    userId,
                    GetRoomPassword(context),
                    cancellationToken);

                return Results.Ok(messages);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        });

        group.MapPost("/{roomId:guid}/messages", async (
            Guid roomId,
            SendMessageRequest request,
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
                var message = await chatRoomService.SendMessageAsync(
                    roomId,
                    userId,
                    GetSenderName(context),
                    GetRoomPassword(context),
                    request,
                    cancellationToken);

                return Results.Created($"/chat/rooms/{roomId}/messages/{message.Id}", message);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        });

        group.MapPost("/{roomId:guid}/participants/heartbeat", async (
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

            try
            {
                await chatRoomService.TouchParticipantAsync(
                    roomId,
                    userId,
                    GetSenderName(context),
                    GetRoomPassword(context),
                    cancellationToken);

                return Results.NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        });

        group.MapGet("/{roomId:guid}/participants", async (
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

            try
            {
                var participants = await chatRoomService.GetParticipantsAsync(
                    roomId,
                    GetRoomPassword(context),
                    cancellationToken);

                return Results.Ok(participants);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
            }
        });

        return group;
    }

    private static string? GetUserId(HttpContext context)
    {
        return context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private static string? GetRoomPassword(HttpContext context)
    {
        return context.Request.Headers.TryGetValue("X-Room-Password", out var password)
            ? password.ToString()
            : null;
    }

    private static string GetSenderName(HttpContext context)
    {
        return context.User.FindFirst(ClaimTypes.Name)?.Value
            ?? context.User.FindFirst(ClaimTypes.Email)?.Value
            ?? "Chat user";
    }
}