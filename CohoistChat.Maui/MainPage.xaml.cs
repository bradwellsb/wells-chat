using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WellsChat.Maui.Services;
using WellsChat.Shared;
using Application = Microsoft.Maui.Controls.Application;

namespace WellsChat.Maui
{
    public enum StatusEnum{ Connecting, Connected, Reconnecting, Disconnected }
    public partial class ChatViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Message> Messages { get; init; }
        private StatusEnum _status;
        private string _statusText;
        private bool _isMultiline;
        public StatusEnum Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); } }
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(nameof(StatusText)); } }
        public bool IsMultiline { get => _isMultiline; set { _isMultiline = value; OnPropertyChanged(nameof(IsMultiline)); } }
        public Command<string> CopyCommand { get; init; }
        public ChatViewModel()
        {
            Messages = new ObservableCollection<Message>();
            CopyCommand = new Command<string>(async (string text) => await Clipboard.Default.SetTextAsync(text));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public partial class MainPage : ContentPage
    {
        private HubConnection hubConnection = null;
        private Command EntryReturnCommand { get; init; }
        private string _accessToken = string.Empty;
        private readonly IPublicClientApplication app = null;
        private readonly Aes256Cipher cipher = null;
        private User me = null;
        private readonly ChatViewModel vm = new();
        private readonly DataService _dataService;
        public MainPage(DataService dataService)
        {
            _dataService = dataService;
            InitializeComponent();
            BindingContext = vm;            
            EntryReturnCommand = new Command(async () => await SendMessage());
            MessageEntry.ReturnCommand = EntryReturnCommand;

            try
            {
                cipher = new Aes256Cipher(Convert.FromBase64String(Task.Run(async () => await _dataService.GetSecretAsync("Key")).Result));
            }
            catch
            {
                Environment.Exit(403);
            }
            app = BuildApp();
            if (app == null) return;
            SetStatus(StatusEnum.Connecting, "Connecting...");
            Task.Run(async () =>
            {
                await RegisterCache();                
                if (await EstablishConnection())
                {
                    SetStatus(StatusEnum.Connected, "Connected");
                }
                else
                {
                    SetStatus(StatusEnum.Disconnected, "Error connecting to server.");
                }
            });
        }

        private async Task SendMessage()
        {
            if (vm.Status == StatusEnum.Connected && (!string.IsNullOrWhiteSpace(MessageEntry.Text) || !string.IsNullOrWhiteSpace(MessageEditor.Text)))
            {
                Message message = new() { Payload = (checkBox.IsChecked ? MessageEditor.Text : MessageEntry.Text) };
                if (message.Payload.ToLower() != "!users") //do not encrypt command messages
                {
                    message.SenderEmail = me.Email;
                    message.SenderDisplayName = me.DisplayName;
                    message = cipher.EncryptMessage(message);
                }

                try
                {
                    await hubConnection.SendAsync("SendMessage", message);
                    MessageEntry.Text = string.Empty;
                    MessageEditor.Text = string.Empty;
                }
                catch
                {
                    Console.WriteLine("Message not sent");
                }                
            }
        }

        private async void SendButton_Clicked(object sender, EventArgs e)
        {
            await SendMessage();                     
        }

        private void checkBox_CheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            if (e.Value)
            {
                MessageEditor.Text = MessageEntry.Text;
            }
        }
        private async void ReconnectButton_Clicked(object sender, EventArgs e)
        {
            if (hubConnection.State != HubConnectionState.Disconnected) return;
            SetStatus(StatusEnum.Connecting, "Connecting...");
            if (await EstablishConnection())
            {
                SetStatus(StatusEnum.Connected, "Connected");
            }
            else
            {
                SetStatus(StatusEnum.Disconnected, "Error connecting to server.");
            }
        }

        private async Task RegisterCache()
        {
#if ANDROID
#elif IOS
#else
            var storageProperties = new StorageCreationPropertiesBuilder(CacheSettings.CacheFileName, CacheSettings.CacheDir)
                .WithLinuxKeyring(
                    CacheSettings.LinuxKeyRingSchema,
                    CacheSettings.LinuxKeyRingCollection,
                    CacheSettings.LinuxKeyRingLabel,
                    CacheSettings.LinuxKeyRingAttr1,
                    CacheSettings.LinuxKeyRingAttr2)
                .WithMacKeyChain(
                    CacheSettings.KeyChainServiceName,
                    CacheSettings.KeyChainAccountName)
                .Build();
            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.RegisterCache(app.UserTokenCache);
#endif            
        }

        private IPublicClientApplication BuildApp()
        {
#if ANDROID
            return PublicClientApplicationBuilder.Create(Task.Run(async () => await _dataService.GetSecretAsync("ClientId")).Result)
                .WithRedirectUri($"msal{Task.Run(async () => await _dataService.GetSecretAsync("ClientId")).Result}://auth")
                .WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
                .WithTenantId(Task.Run(async () => await _dataService.GetSecretAsync("TenantId")).Result)
                .WithParentActivityOrWindow(() => Platform.CurrentActivity)
                .Build();
#elif IOS
            return PublicClientApplicationBuilder.Create(Task.Run(async () => await _dataService.GetSecretAsync("ClientId")).Result)
                .WithRedirectUri($"msal{Task.Run(async () => await _dataService.GetSecretAsync("ClientId")).Result}://auth")
                .WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
                .WithIosKeychainSecurityGroup("com.microsoft.adalcache")
                .WithTenantId(Task.Run(async () => await _dataService.GetSecretAsync("TenantId")).Result)
                .Build();
#elif WINDOWS
            return PublicClientApplicationBuilder.Create(Task.Run(async () => await _dataService.GetSecretAsync("ClientId")).Result)
                .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
                .WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
                .WithTenantId(Task.Run(async () => await _dataService.GetSecretAsync("TenantId")).Result)
                .WithParentActivityOrWindow(() => WinRT.Interop.WindowNative.GetWindowHandle(
                    Application.Current.Windows[0].Handler.PlatformView))
                .WithRedirectUri("http://localhost")
                .Build();
#else
            return PublicClientApplicationBuilder.Create(Task.Run(async () => await _dataService.GetSecretAsync("ClientId")).Result)
                .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
                .WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
                .WithTenantId(Task.Run(async () => await _dataService.GetSecretAsync("TenantId")).Result)
                .Build();
#endif
        }

        private async Task<bool> EstablishConnection()
        {
            if (hubConnection != null && hubConnection.State != HubConnectionState.Disconnected)
            {
                await hubConnection.StopAsync();
                await hubConnection.DisposeAsync();
            }

            AuthenticationResult result;
            var account = await app.GetAccountsAsync();
            var scopes = new string[] { Task.Run(async () => await _dataService.GetSecretAsync("ApiScope")).Result };

            try
            {
                result = await app.AcquireTokenSilent(scopes,
                    account.FirstOrDefault()).ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                try
                {
                    result = await MainThread.InvokeOnMainThreadAsync(
                        async () => await app.AcquireTokenInteractive(scopes).ExecuteAsync());
                }
                catch (MsalServiceException e)
                {
                    SetStatus(StatusEnum.Disconnected, "Not authorized");
                    result = null;
                }
            }


            if (result != null)
            {
                _accessToken = result.AccessToken;
                me = new User()
                {
                    Email = result.ClaimsPrincipal.FindFirst("preferred_username").Value, //email
                    DisplayName = result.ClaimsPrincipal.FindFirst("name").Value
                };


                hubConnection = new HubConnectionBuilder()
                    .WithUrl(Task.Run(async () => await _dataService.GetSecretAsync("HubUrl")).Result, options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult(_accessToken);
                    })
                    .WithAutomaticReconnect()
                    .Build();


                try
                {
                    AddHandlers();
                    await hubConnection.StartAsync();
                    return true;
                }
                catch (HttpRequestException e)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }
        private void SetStatus(StatusEnum status, string statustTest)
        {
            Application.Current.Dispatcher.Dispatch(() =>
            {
                vm.Status = status;
                vm.StatusText = statustTest;
            });
        }

        private void AddMessage(Message message)
        {
            Application.Current.Dispatcher.Dispatch(() => {
                vm.Messages.Add(message);
                MessagesList.ScrollTo(vm.Messages.Last(), ScrollToPosition.End);
            });
        }
        private void AddHandlers()
        {
            hubConnection.On<User>("UserConnected", (user) =>
            {
                var message = new Message()
                {
                    SenderDisplayName = "Info",
                    MessageType = MessageTypeEnum.Connected,
                    Payload = $"{user.Email} ({user.DisplayName}) [{user.ActiveConnections}] connected."
                };
                AddMessage(message);
            });

            hubConnection.On<User>("UserDisconnected", (user) =>
            {
                var message = new Message()
                {
                    SenderDisplayName = "Info",
                    MessageType = MessageTypeEnum.Disconnected,
                    Payload = $"{user.Email} ({user.DisplayName}) [{user.ActiveConnections}] disconnected."
                };
                AddMessage(message);
            });

            hubConnection.On<Message>("ReceiveMessage", (message) =>
            {
                message = cipher.DecryptMessage(message);
                bool isMe = message.SenderEmail == me.Email;
                if (isMe) message.MessageType = MessageTypeEnum.Me;
                else message.MessageType = MessageTypeEnum.NotMe;
                AddMessage(message);
            });

            hubConnection.On<Message>("SendSuccess", (message) =>
            {
                
            });

            hubConnection.On<List<User>>("ListUsers", (users) =>
            {
                var message = new Message()
                {
                    SenderDisplayName = "Info",
                    MessageType = MessageTypeEnum.Info,
                    Payload = $"{users.Count} users online"
                };
                foreach (var user in users)
                {
                    message.Payload += $"\n{user.Email} ({user.DisplayName}) [{user.ActiveConnections}]";
                }
                AddMessage(message);
            });

            hubConnection.Closed += HubConnection_Closed;
            hubConnection.Reconnected += HubConnection_Reconnected;
            hubConnection.Reconnecting += HubConnection_Reconnecting;
        }

        private Task HubConnection_Reconnecting(Exception? arg)
        {
            SetStatus(StatusEnum.Reconnecting, "Connection lost. Reconnecting...");
            return Task.CompletedTask;
        }

        private Task HubConnection_Reconnected(string? arg)
        {
            SetStatus(StatusEnum.Connected, "Connected");
            return Task.CompletedTask;
        }

        private async Task HubConnection_Closed(Exception? arg)
        {
            if (arg == null) return; //if client deliberately disconnects, do nothing
            var tryAgainString = " Try again in ";
            for (int i = 1; i <= 5; i++) //attempt to reconnect 5 times
            {
                SetStatus(StatusEnum.Reconnecting, $"Attempting to reconnect {i}/5... ");
                if (await EstablishConnection())
                {
                    //Connection successful
                    SetStatus(StatusEnum.Connected, "Connected");
                    return;
                }
                else
                {
                    //Connection failed
                    if (i < 5)
                    {
                        vm.StatusText += "FAILED.";
                        vm.StatusText += tryAgainString;
                        for (int j = i * 5; j > 0; j--) //wait i * 5 seconds before trying again 
                        {
                            vm.StatusText += $"{j}";
                            await Task.Delay(1000);
                            //erase current countdown number, get ready to write new number
                            if (j > 1)
                            {                                
                                vm.StatusText = vm.StatusText[..^j.ToString().Length]; //status text without timer                            
                            }
                            else //if countdown complete
                            {
                                vm.StatusText = vm.StatusText.Substring(0,vm.StatusText.Length - j.ToString().Length - tryAgainString.Length); //status text without tryagain string
                            }
                        }                        
                    }
                }
            }
            //Failed 5 reconnection attempts
            SetStatus(StatusEnum.Disconnected, "Disconnected");
            //TODO: Add reconnect button if status disconnected
            Console.WriteLine($" Enter !reconnect to try again.");
            return;
        }
        private async void CopyFromMenuAsync(object sender, EventArgs e)
        {
            await Clipboard.Default.SetTextAsync((string)((MenuFlyoutItem)sender).CommandParameter);

            /*
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            string text = "Copied to clipboard";
            ToastDuration duration = ToastDuration.Short;
            double fontSize = 14;
            var toast = Toast.Make(text, duration, fontSize);
            await toast.Show(cancellationTokenSource.Token);
            */
        }
    }
}