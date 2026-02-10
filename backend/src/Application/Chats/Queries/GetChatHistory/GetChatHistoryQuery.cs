using Contracts.DTO.Chats;
using MediatR;

namespace Application.Chats.Queries.GetChatHistory;

public sealed record GetChatHistoryQuery(Guid CurrentUserId, Guid OtherUserId) : IRequest<List<ChatMessageDto>>;
