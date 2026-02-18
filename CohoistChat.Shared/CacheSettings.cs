using Microsoft.Identity.Client.Extensions.Msal;

namespace CohoistChat.Shared
{
    public class CacheSettings
    {
        private static readonly string s_cacheFilePath =
                   Path.Combine(MsalCacheHelper.UserRootDirectory, "CohoistChat.cache");

        public static readonly string CacheFileName = Path.GetFileName(s_cacheFilePath);
        public static readonly string CacheDir = Path.GetDirectoryName(s_cacheFilePath);


        public static readonly string KeyChainServiceName = "CohoistChat";
        public static readonly string KeyChainAccountName = "TokenCache";

        public static readonly string LinuxKeyRingSchema = "CohoistChat.tokencache";
        public static readonly string LinuxKeyRingCollection = MsalCacheHelper.LinuxKeyRingDefaultCollection;
        public static readonly string LinuxKeyRingLabel = "Token cache for Wells Chat Client";
        public static readonly KeyValuePair<string, string> LinuxKeyRingAttr1 = new KeyValuePair<string, string>("Version", "1");
        public static readonly KeyValuePair<string, string> LinuxKeyRingAttr2 = new KeyValuePair<string, string>("ProductGroup", "Apps");
    }
}
