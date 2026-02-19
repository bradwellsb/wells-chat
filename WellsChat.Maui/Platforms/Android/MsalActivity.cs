using Android.App;
using Android.Content;
using Microsoft.Identity.Client;

namespace WellsChat.Maui
{
    [Activity(Exported = true)]
    [IntentFilter(new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryBrowsable, Intent.CategoryDefault },
    DataHost = "auth",
    DataScheme = $"msalff7f5b3b-5da1-4ff6-86f0-46b06e282160")]
    public class MsalActivity : BrowserTabActivity
    {
    }
}
