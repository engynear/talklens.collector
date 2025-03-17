using Microsoft.AspNetCore.SignalR;

namespace TalkLens.Collector.Api.Hubs;

public class TelegramHub : Hub
{
    public async Task SubmitUserInput(string what, string value)
    {
        // Здесь можно обработать входящие данные от клиента
        // или вызвать сервис, который пробросит их в WTelegramClient
    }
}