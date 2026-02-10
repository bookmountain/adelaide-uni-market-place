using System.Security.Claims;
using Application.Chats.Commands.SendMessage;
using Contracts.DTO.Chats;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly ISender _sender;

    public ChatHub(ISender sender)
    {
        _sender = sender;
    }

    public async Task<ChatMessageDto> SendMessage(SendMessageRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var senderId))
        {
            throw new HubException("Unauthorized");
        }

        var command = new SendMessageCommand(
            senderId,
            request.ToUserId,
            request.Body,
            request.ItemId,
            request.AttachmentUrl);

        var message = await _sender.Send(command, cancellationToken);

        await Clients.User(request.ToUserId.ToString())
            .SendAsync("ReceiveMessage", message, cancellationToken);

        await Clients.Caller
            .SendAsync("ReceiveMessage", message, cancellationToken);

        return message;
    }

    private bool TryGetUserId(out Guid userId)
    {
        var claim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("sub");
        return Guid.TryParse(claim, out userId);
    }
}