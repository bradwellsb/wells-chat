using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Identity.Web.Resource;
using WellsChat.Server.Services;
using WellsChat.Shared;

namespace WellsChat.Server.Hubs
{
    [Authorize]
    [RequiredScope("chat")]
    public class ChatHub : Hub
    {
        internal const string ACTIVE_USERS = "activeUsers";
        private readonly AppData _appData;
        public ChatHub(AppData appData)
        {
            _appData = appData;
        }
        public async Task SendMessage(MessageDto messageDto)
        {
            if (!string.IsNullOrWhiteSpace(messageDto.Payload))
            {
                switch (messageDto.Payload.ToLower())
                {
                    case "!users":
                        await Clients.Caller.SendAsync("ListUsers", _appData.MemoryCache.Get<List<User>>(ACTIVE_USERS));
                        break;
                    default:
                        await Clients.Caller.SendAsync("SendSuccess", messageDto);
                        await Clients.All.SendAsync("ReceiveMessage", messageDto);                        
                        break;
                }
            }
        }

        public override async Task OnConnectedAsync()
        {
            var activeUsers = _appData.MemoryCache.Get<List<User>>(ACTIVE_USERS);
            User? user = activeUsers.Where(u => u.Email == Context.User.Claims.FirstOrDefault(c => c.Type.Equals("email")).Value).SingleOrDefault();

            if (user is not null)
            {
                user.ActiveConnections = ++activeUsers.Where(u => u.Email == user.Email).SingleOrDefault().ActiveConnections;
            }
            else
            {
                user = new()
                {
                    DisplayName = Context.User.Claims.FirstOrDefault(c => c.Type.Equals("name")).Value,
                    Email = Context.User.Claims.FirstOrDefault(c => c.Type.Equals("email")).Value,
                    ActiveConnections = 1
                };

                activeUsers.Add(user);
            }
            _appData.MemoryCache.Set<List<User>>(ACTIVE_USERS, activeUsers);
            await Clients.All.SendAsync("UserConnected", user);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var activeUsers = _appData.MemoryCache.Get<List<User>>(ACTIVE_USERS);
            User user = activeUsers.Where(u => u.Email == Context.User.Claims.FirstOrDefault(c => c.Type.Equals("email")).Value).SingleOrDefault();           

            if (user.ActiveConnections <= 1)
            {               
                activeUsers.Remove(user);
                user.ActiveConnections = 0;
            }
            else
            {
                user.ActiveConnections = --activeUsers.Where(u => u.Email == user.Email).SingleOrDefault().ActiveConnections;
                
            }
                

            await Clients.All.SendAsync("UserDisconnected", user);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
