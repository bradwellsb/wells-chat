using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Identity.Client.Extensions.Msal;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using CohoistChat.Shared;

namespace CohoistChat.Clientconsole
{
    class Program
    {
        static string _accessToken = string.Empty;
        static IPublicClientApplication app = null;
        static SecretClient secretClient = null;
        static HubConnection hubConnection = null;
        static Aes256Cipher cipher = null;
        static User me = null;        
        static async Task Main(string[] args) {
            Console.WriteLine("Authenticating...");

            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            var credentials = new DefaultAzureCredential(true);
            secretClient = new SecretClient(new Uri(config.GetValue<string>("VaultUri")), credentials);
            Console.SetCursorPosition(0, Console.CursorTop - 1);            

            cipher = new Aes256Cipher(Convert.FromBase64String(secretClient.GetSecret("Key").Value.Value));

            Console.WriteLine("Authenticated    ");
            app = BuildApp();
            if (app == null) return;
            await RegisterCache();
            Console.WriteLine("Connecting...");
            if (await EstablishConnection()) {
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.WriteLine("Connected    ");
                while (true)
                {
                    Console.Write("> ");
                    MessageDto message = new() { 
                        Payload = Console.ReadLine(),
                        SenderEmail = me.Email,
                        SenderDisplayName = me.DisplayName,
                        TimeSent = DateTime.UtcNow.ToString()
                    };

                    if (message.Payload.ToLower() == "exit")
                    {
                        await hubConnection.DisposeAsync();
                        Environment.Exit(0);
                    }
                    else if (message.Payload.ToLower() == "!reconnect")
                    {
                        //Command will only work if in disconnected state
                        if (hubConnection.State == HubConnectionState.Disconnected)
                        {
                            Console.WriteLine("Connecting...");
                            if (await EstablishConnection())
                            {
                                Console.SetCursorPosition(0, Console.CursorTop - 1);
                                Console.WriteLine("Connected    ");
                            }
                            else
                            {
                                Console.SetCursorPosition(0, Console.CursorTop - 1);
                                Console.WriteLine("Connection failed. Enter !reconnect to try again.");
                            }
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(message.Payload))
                        {
                            if (message.Payload.ToLower() != "!users") //do not encrypt command messages
                            {
                                
                                message = cipher.EncryptMessage(message);
                            }

                            try
                            {
                                await hubConnection.SendAsync("SendMessage", message);
                            }
                            catch
                            {
                                Console.WriteLine("Message not sent");
                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Error connecting to server.");
                Console.ReadLine();
                Environment.Exit(0);
            }
        }

        private static async Task RegisterCache()
        {
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
        }

        private static IPublicClientApplication BuildApp()
        {
            return PublicClientApplicationBuilder.Create(secretClient.GetSecret("ClientId").Value.Value)
                .WithDefaultRedirectUri()
                .WithAuthority(AadAuthorityAudience.AzureAdMyOrg)
                .WithTenantId(secretClient.GetSecret("TenantId").Value.Value)
                .Build();
        }

        private static async Task<bool> EstablishConnection()
        {
            if (hubConnection != null && hubConnection.State != HubConnectionState.Disconnected)
            {
                await hubConnection.StopAsync();
                await hubConnection.DisposeAsync();
            }

            AuthenticationResult result;
            var account = await app.GetAccountsAsync();
            var scopes = new string[] { secretClient.GetSecret("ApiScope").Value.Value };

            try
            {
                result = await app.AcquireTokenSilent(scopes,
                    account.FirstOrDefault()).ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                try
                {
                    result = await app.AcquireTokenInteractive(scopes).ExecuteAsync();
                }
                catch (MsalServiceException e)
                {
                    Console.WriteLine("Not authorized");
                    result = null;
                    Console.ReadLine();
                    Environment.Exit(0);
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
                    .WithUrl(secretClient.GetSecret("HubUrl").Value.Value, options =>
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
        private static void AddHandlers()
        {
            hubConnection.On<User>("UserConnected", (user) =>
            {
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.BackgroundColor = ConsoleColor.DarkCyan;
                Console.Write($"{DateTime.Now.ToString("g")} | {user.Email} ({user.DisplayName}) [{user.ActiveConnections}] connected.");
                Console.ResetColor();
                Console.WriteLine();
                Console.Write("> ");
            });

            hubConnection.On<User>("UserDisconnected", (user) =>
            {
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.BackgroundColor = ConsoleColor.DarkGray;
                Console.Write($"{DateTime.Now.ToString("g")} | {user.Email} ({user.DisplayName}) [{user.ActiveConnections}] disconnected.");
                Console.ResetColor();
                Console.WriteLine();
                Console.Write("> ");
            });

            hubConnection.On<MessageDto>("ReceiveMessage", (messageDto) =>
            {
                var message = (Message)cipher.DecryptMessage(messageDto);
                bool isMe = message.SenderEmail == me.Email;
                var receivedMsg = $"{DateTime.Now.ToString("g")} | {message.SenderDisplayName}: {message.Payload}";                
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.BackgroundColor = isMe ? ConsoleColor.DarkGreen : ConsoleColor.DarkBlue;
                Console.Write(receivedMsg);
                Console.ResetColor();
                Console.WriteLine(isMe ? null : "\a");
                Console.Write("> ");
            });

            hubConnection.On<MessageDto>("SendSuccess", (messageDto) =>
            {
                var message = (Message)cipher.DecryptMessage(messageDto);
                Console.SetCursorPosition(0, Console.CursorTop - 1 - (int)((message.Payload.Length + 2) / Console.BufferWidth)); //calculate number of rows this message spans
            });

            hubConnection.On<List<User>>("ListUsers", (users) =>
            {
                var onlineText = users.Count == 1 ? "user online" : "users online";
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.WriteLine($"{users.Count} {onlineText}");
                foreach (var user in users)
                {
                    Console.WriteLine($"{user.Email} ({user.DisplayName}) [{user.ActiveConnections}]");
                }
                Console.Write("> ");
            });

            hubConnection.Closed += HubConnection_Closed;
            hubConnection.Reconnected += HubConnection_Reconnected;
            hubConnection.Reconnecting += HubConnection_Reconnecting;
        }

        private static Task HubConnection_Reconnecting(Exception? arg)
        {
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write($"Connection lost. Reconnecting... ");
            return Task.CompletedTask;
        }

        private static Task HubConnection_Reconnected(string? arg)
        {
            Console.WriteLine($"Connected");
            Console.Write("> ");
            return Task.CompletedTask;
        }

        private static async Task HubConnection_Closed(Exception? arg)
        {
            if (arg == null) return; //if client deliberately disconnects, do nothing
            var tryAgainString = " Try again in ";
            Console.WriteLine($"Timed out.");
            for (int i = 1; i <= 5; i++) //attempt to reconnect 5 times
            {                
                Console.Write($"Attempting to reconnect {i}/5... ");
                if(await EstablishConnection())
                {
                    Console.WriteLine("Connected    ");
                    return;
                }
                else
                {               
                    //Connection failed
                    if (i < 5)
                    {
                        Console.BackgroundColor = ConsoleColor.DarkRed;
                        Console.Write($"FAILED.");
                        Console.ResetColor();
                        Console.Write(tryAgainString);
                        for (int j = i * 5; j > 0; j--) //wait i * 5 seconds before trying again 
                        {
                            Console.Write($"{j}");
                            await Task.Delay(1000);
                            //erase current countdown number, get ready to write new number
                            if(j > 1)
                            {
                                Console.SetCursorPosition(Console.CursorLeft - j.ToString().Length, Console.CursorTop);
                                for (int k = 0; k < j.ToString().Length; k++)
                                    Console.Write(" ");
                                Console.SetCursorPosition(Console.CursorLeft - j.ToString().Length, Console.CursorTop);
                            }
                            else //if countdown complete
                            {
                                Console.SetCursorPosition(Console.CursorLeft - j.ToString().Length - tryAgainString.Length, Console.CursorTop);
                                for (int k = 0; k < (j.ToString().Length + tryAgainString.Length); k++)
                                    Console.Write(" ");
                                Console.SetCursorPosition(Console.CursorLeft - j.ToString().Length - tryAgainString.Length, Console.CursorTop);
                            }                            
                        }
                        Console.WriteLine();
                    }
                }
            }
            //Failed 5 reconnection attempts
            Console.BackgroundColor = ConsoleColor.DarkRed;
            Console.Write($"FAILED.");
            Console.ResetColor();
            Console.WriteLine($" Enter !reconnect to try again.");
            Console.Write("> ");       
            return;
        }
    }
}



