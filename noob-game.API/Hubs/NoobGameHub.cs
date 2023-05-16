using Microsoft.AspNetCore.SignalR;

namespace noob_game.API.Hubs
{
    public class NoobGameHub:Hub
    {
        public async Task SendMessage(string message)
        {
            await Clients.Others.SendAsync("ReceiveMessage",message);
        }
    }
}
