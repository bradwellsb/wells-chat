using Microsoft.Extensions.Caching.Memory;
using WellsChat.Server.Hubs;
using WellsChat.Shared;

namespace WellsChat.Server.Services
{
    public class AppData
    {
        public AppData(IMemoryCache memoryCache)
        {
            this.MemoryCache = memoryCache;
            this.MemoryCache.Set<List<User>>(ChatHub.ACTIVE_USERS, new List<User>());
        }
        public IMemoryCache MemoryCache { get; set; }
        /*
        private static List<User> _currentUsers = new();
        public List<User> CurrentUsers
        {
            get
            {
                return _currentUsers;
            }
            set
            {
                _currentUsers = value;
                //NotifyDataChanged();
            }
        }

        //public event Action OnChange;
        //private void NotifyDataChanged() => OnChange?.Invoke();
        */
    }
}
