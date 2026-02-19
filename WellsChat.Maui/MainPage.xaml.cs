using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WellsChat.Maui.Services;
using WellsChat.Shared;
using Syncfusion.Maui.Popup;
using System.Text.RegularExpressions;
using Application = Microsoft.Maui.Controls.Application;

namespace WellsChat.Maui
{
    public enum StatusEnum { Connecting, Connected, Reconnecting, Disconnected }
    public partial class ChatViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Message> Messages { get; init; }
        private StatusEnum _status;
        private string _statusText;
        public bool _isPrivate = false;
        public StatusEnum Status { get => _status; set { _status = value; OnPropertyChanged(nameof(Status)); } }
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(nameof(StatusText)); } }
        private bool _isMultiline = false;
        public bool IsMultiline { get => _isMultiline; set { _isMultiline = value; OnPropertyChanged(nameof(IsMultiline)); } }
        public Command<string> CopyCommand { get; init; }
        public Command<string> CopyUrlCommand { get; init; }
        public ChatViewModel()
        {
            Messages = new ObservableCollection<Message>();
            CopyCommand = new Command<string>(async (string text) => await Clipboard.Default.SetTextAsync(text));
            CopyUrlCommand = new Command<string>(async (string text) =>
            {
                string url = Regex.Match(text, @"(https?://[^\s]+)").Value;
                if (string.IsNullOrEmpty(url)) return;
                await Clipboard.Default.SetTextAsync(url);
            });
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
        private SfPopup pwPopup = new SfPopup();
        private Entry pwEntry = new Entry();
        private DataTemplate pwTemplate;
        private string passwordHash = string.Empty;
        private readonly string[] _commands = new string[] { "!users" };

        public MainPage(DataService dataService)
        {
            _dataService = dataService;
            InitializeComponent();
            BindingContext = vm;
            EntryReturnCommand = new Command(async () => await SendMessage());
            MessageEntry.ReturnCommand = EntryReturnCommand;
            ConfigurePopup();
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
        private void ConfigurePopup()
        {
            pwPopup.ShowFooter = true;
            pwPopup.AppearanceMode = PopupButtonAppearanceMode.TwoButton;
            pwPopup.AcceptButtonText = "Submit";
            pwPopup.DeclineButtonText = "Cancel";
            pwPopup.AcceptCommand = new Command(async () => { await ToggleMessages(); });
            pwPopup.DeclineCommand = new Command(async () => { await CancelPopup(); });

            pwTemplate = new DataTemplate(() =>
            {
                pwEntry = new Entry();
                pwEntry.IsPassword = true;
                pwEntry.Text = string.Empty;
                pwEntry.Completed += async (sender, e) => { await ToggleMessages(); };
                return pwEntry;
            });
            pwPopup.ContentTemplate = pwTemplate;
        }
        private async Task SendMessage()
        {
            var messageText = vm.IsMultiline ? MessageEditor.Text : MessageEntry.Text;
            if (vm.Status == StatusEnum.Connected && !string.IsNullOrWhiteSpace(messageText))
            {
                MessageDto messageDto = new()
                {
                    Payload = messageText,
                    SenderEmail = me.Email,
                    SenderDisplayName = me.DisplayName,
                    TimeSent = DateTime.UtcNow.ToString("O")
                };
                if (!_commands.Contains(messageDto.Payload.ToLower())) //do not encrypt command messages
                {

                    messageDto = cipher.EncryptMessage(messageDto);
                }

                try
                {
                    await hubConnection.SendAsync("SendMessage", messageDto);
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
        private async void OnUsersClicked(object sender, EventArgs e)
        {
            await ListUsers();
        }
        private async void OnPrivateClicked(object sender, EventArgs e)
        {
            if (vm._isPrivate)
            {
                pwEntry.Placeholder = "Enter password";
                pwPopup.HeaderTitle = "Unlock Messages";
            }
            else
            {
                pwEntry.Placeholder = "Set password";
                pwPopup.HeaderTitle = "Lock Messages";
            }
            pwPopup.Show();
            pwEntry.Focus();
        }
        private async void OnClearClicked(object sender, EventArgs e)
        {
            await ClearMessages();
        }
        private async Task ToggleMessages()
        {
            if (vm._isPrivate)
            {
                if (!string.IsNullOrEmpty(pwEntry.Text) && !string.IsNullOrEmpty(passwordHash))
                {
                    if (BCrypt.Net.BCrypt.Verify(pwEntry.Text, passwordHash))
                    {
                        //Success. Unlock messages
                        pwEntry.Text = string.Empty;
                        passwordHash = string.Empty;
                        pwPopup.IsOpen = false;
                    }
                    else
                    {
                        //Incorrect password. Cancel
                        pwEntry.Text = string.Empty;
                        pwPopup.IsOpen = false;
                        return;
                    }
                }
                else
                {
                    //no password entered. Cancel
                    pwEntry.Text = string.Empty;
                    pwPopup.IsOpen = false;
                    return;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(pwEntry.Text))
                {
                    //no password set. Cancel
                    pwEntry.Text = string.Empty;
                    pwPopup.IsOpen = false;
                    return;
                }
                else
                {
                    //set password to lock messages
                    passwordHash = BCrypt.Net.BCrypt.HashPassword(pwEntry.Text);
                    pwEntry.Text = string.Empty;
                    pwPopup.IsOpen = false;
                }
            }

            vm._isPrivate = !vm._isPrivate;
            ToolbarItems.Where(x => x.AutomationId == "Private").FirstOrDefault().Text = vm._isPrivate ? "🔓" : "🔒";
            var privateMessageList = new ObservableCollection<Message>() {
                new Message() { Payload = "Welcome to Cohoist Chat.", Type=MessageTypeEnum.Connected, SenderDisplayName="Info", SenderEmail="Redacted", TimeReceived = DateTime.Now, TimeSent = DateTime.Now },
                new Message() { Payload = "This is a private chat application where you can send secure messages.", Type=MessageTypeEnum.Connected, SenderDisplayName="Info", SenderEmail="Redacted", TimeReceived = DateTime.Now, TimeSent = DateTime.Now },
                new Message() { Payload = "You have no new messages.", Type=MessageTypeEnum.Connected, SenderDisplayName="Info", SenderEmail="Redacted", TimeReceived = DateTime.Now, TimeSent = DateTime.Now },
            };
            MessagesList.ItemsSource = vm._isPrivate ? privateMessageList : vm.Messages;
        }
        private async Task CancelPopup()
        {
            pwEntry.Text = string.Empty;
            pwPopup.IsOpen = false;
        }

        private async Task ClearMessages()
        {
            vm.Messages.Clear();
        }
        private async Task ListUsers()
        {
            await hubConnection.SendAsync("SendMessage", new Message() { Payload = "!users" });
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
            await StopAndDisposeHubConnection();

            var scopes = new string[] { await _dataService.GetSecretAsync("ApiScope") };
            var result = await GetAccessToken(scopes);

            if (result != null)
            {
                _accessToken = result.AccessToken;
                me = CreateUserFromResult(result);

                hubConnection = CreateHubConnection(await _dataService.GetSecretAsync("HubUrl"), _accessToken);

                return await StartHubConnection();
            }

            return false;
        }

        private async Task StopAndDisposeHubConnection()
        {
            if (hubConnection != null && hubConnection.State != HubConnectionState.Disconnected)
            {
                await hubConnection.StopAsync();
                await hubConnection.DisposeAsync();
            }
        }

        private async Task<AuthenticationResult> GetAccessToken(string[] scopes)
        {
            var account = await app.GetAccountsAsync();

            try
            {
                return await app.AcquireTokenSilent(scopes, account.FirstOrDefault()).ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                try
                {
                    return await app.AcquireTokenInteractive(scopes).ExecuteAsync();
                }
                catch (MsalServiceException e)
                {
                    SetStatus(StatusEnum.Disconnected, "Not authorized");
                    return null;
                }
            }
        }

        private User CreateUserFromResult(AuthenticationResult result)
        {
            var emailClaim = result.ClaimsPrincipal.FindFirst("preferred_username");
            var nameClaim = result.ClaimsPrincipal.FindFirst("name");

            return new User()
            {
                Email = emailClaim?.Value,
                DisplayName = nameClaim?.Value
            };
        }

        private HubConnection CreateHubConnection(string url, string accessToken)
        {
            return new HubConnectionBuilder()
                .WithUrl(url, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(accessToken);
                })
                .WithAutomaticReconnect()
                .Build();
        }

        private async Task<bool> StartHubConnection()
        {
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
                    TimeReceived = DateTime.Now,
                    Type = MessageTypeEnum.Connected,
                    Payload = $"{user.Email} ({user.DisplayName}) [{user.ActiveConnections}] connected."
                };
                AddMessage(message);
            });

            hubConnection.On<User>("UserDisconnected", (user) =>
            {
                var message = new Message()
                {
                    SenderDisplayName = "Info",
                    TimeReceived = DateTime.Now,
                    Type = MessageTypeEnum.Disconnected,
                    Payload = $"{user.Email} ({user.DisplayName}) [{user.ActiveConnections}] disconnected."
                };
                AddMessage(message);
            });

            hubConnection.On<MessageDto>("ReceiveMessage", (messageDto) =>
            {
                var message = (Message)cipher.DecryptMessage(messageDto);
                bool isMe = message.SenderEmail == me.Email;
                if (isMe) message.Type = MessageTypeEnum.Me;
                else message.Type = MessageTypeEnum.NotMe;
                AddMessage(message);
            });

            hubConnection.On<MessageDto>("SendSuccess", (messageDto) =>
            {

            });

            hubConnection.On<List<User>>("ListUsers", (users) =>
            {
                var onlineText = users.Count == 1 ? "user online" : "users online";
                var message = new Message()
                {
                    SenderDisplayName = "Info",
                    TimeReceived = DateTime.Now,
                    Type = MessageTypeEnum.Info,
                    Payload = $"{users.Count} {onlineText}"
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
                                vm.StatusText = vm.StatusText.Substring(0, vm.StatusText.Length - j.ToString().Length - tryAgainString.Length); //status text without tryagain string
                            }
                        }
                    }
                }
            }
            //Failed 5 reconnection attempts
            SetStatus(StatusEnum.Disconnected, "Disconnected");
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
        private void OnMultilineToggled(object sender, EventArgs e)
        {
            vm.IsMultiline = !vm.IsMultiline;
        }
        private void DeleteMessage(object sender, EventArgs e)
        {
            //delete individual message based on swipe gesture
            var deleteImage = sender as Image;
            if (deleteImage == null) return;
            var item = deleteImage?.BindingContext as Message;
            var itemIndex = vm.Messages.IndexOf(item);
            if (itemIndex >= 0)
                vm.Messages.RemoveAt(itemIndex);
            //reset swipe
            MessagesList.ResetSwipeItem();
        }
    }
}