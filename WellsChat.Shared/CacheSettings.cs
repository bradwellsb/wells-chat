using Microsoft.Identity.Client.Extensions.Msal;

namespace WellsChat.Shared
{
    public class CacheSettings
    {
        private static readonly string s_cacheFilePath =
                   Path.Combine(MsalCacheHelper.UserRootDirectory, "WellsChat.cache");

        public static readonly string CacheFileName = Path.GetFileName(s_cacheFilePath);
        public static readonly string CacheDir = Path.GetDirectoryName(s_cacheFilePath);


        public static readonly string KeyChainServiceName = "WellsChat";
        public static readonly string KeyChainAccountName = "TokenCache";

        public static readonly string LinuxKeyRingSchema = "WellsChat.tokencache";
        public static readonly string LinuxKeyRingCollection = MsalCacheHelper.LinuxKeyRingDefaultCollection;
        public static readonly string LinuxKeyRingLabel = "Token cache for Wells Chat Client";
        public static readonly KeyValuePair<string, string> LinuxKeyRingAttr1 = new KeyValuePair<string, string>("Version", "1");
        public static readonly KeyValuePair<string, string> LinuxKeyRingAttr2 = new KeyValuePair<string, string>("ProductGroup", "Apps");
    }
}
