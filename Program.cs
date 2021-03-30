using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RestSharp;
using TSLib;
using TSLib.Query;
using JsonSerializer = System.Text.Json.JsonSerializer;


namespace TF47_Teamspeak_AntiVpn
{
    class Program
    {
        private static TsQueryClient _client;

        private static IConfiguration _configuration;
        private static readonly Queue<(string, DateTime)> Messages = new Queue<(string, DateTime)>();
        private static List<string> _whitelistedUser = new List<string>();
        private static List<string> _whitelistedProvider = new List<string>();
        private static readonly string LogDestination = Path.Combine(Environment.CurrentDirectory, "log.txt");
        private static FileSystemWatcher _fileSystemWatcher;

        static void Main(string[] args)
        {
            Main().Wait();
        }
        
        
        static async Task Main()
        {
            WriteLog("Starting up!");

            Task.Run(async () =>
            {
                LogBackgroundWorker();
            });
            
            var builder = new ConfigurationBuilder()
                .SetBasePath(Environment.CurrentDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            _configuration = builder.Build();

            var ipAddress = string.Empty;
            try
            {
                ipAddress = (await Dns.GetHostAddressesAsync(_configuration["ServerAddress"]))[0].ToString();
            }
            catch
            {
                WriteLog("Failed to parse hostname! Exiting...");
                Environment.Exit(-1);
            }

            _whitelistedProvider = _configuration.GetSection("ExcludedProviderList").Get<string[]>().ToList();

            _client = new TsQueryClient();
            _client.Connect(new ConnectionData
            {
                Address = ipAddress
            });
            
            WriteLog("Login in...");
            _client.Login(_configuration["User"], _configuration["Password"]);
            WriteLog("Logged in!");
            _client.UseServer(int.Parse(_configuration["ServerId"]));
            _client.RegisterNotificationServer();
            
            _client.OnClientEnterView += Client_OnClientEnterView;

            LoadWhitelist(true);

            Console.WriteLine("Listening for new connections, press any key to stop");

            while (true)
            {
                _client.WhoAmI();
                Thread.Sleep(5000);
            }
            Console.ReadKey();
            //MainAsync().Wait();
        }

        private static void Client_OnClientEnterView(object sender, IEnumerable<TSLib.Messages.ClientEnterView> e)
        {
            foreach (var clientEnterView in e)
            {
                var details= _client.ClientInfo(clientEnterView.ClientId).Value;

                WriteLog($"Client {details.Name} joined! IP: {details.Ip}, Connected at: {details.LastConnected.ToLongTimeString()}");

                if (_whitelistedUser.Contains(details.Uid.Value) || _whitelistedUser.Contains(details.MyTeamSpeakId))
                {
                    WriteLog("> User is whitelisted!");
                    return;
                }

                if (CheckIsBlacklist(details.Ip).GetAwaiter().GetResult())
                {
                    WriteLog($"> WARNING USER CONNECTED WITH VPN! Kicking...");
                    _client.KickClientFromServer(clientEnterView.ClientId, "VPN service is not allowed on this server!");
                }
                else
                {
                    WriteLog("> Client clear");
                }
            }
        }

        private static async Task<bool> CheckIsBlacklist(string ip)
        {
            var client = new RestClient("https://ip.teoh.io/api/vpn/");
            var request = new RestRequest(ip);
            try
            {
                var response = await client.ExecuteGetAsync(request);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var responseData = JsonSerializer.Deserialize<VpnTestResponse>(response.Content);

                    if (responseData == null)
                    {
                        WriteLog("Error VPN Api did not respond");
                        return false;
                    }
                    
                    if (_whitelistedProvider.All(x => x != responseData.organization) && (responseData.vpn_or_proxy == "yes" || responseData.risk == "high"))
                    {
                        WriteLog($"IP: {ip} using proxy! Type: {responseData.type}, organization: {responseData.organization}");
                        return true;
                    }
                }
                else
                {
                    WriteLog($"Query for ip {ip} failed! Response {response.StatusCode} {response.Content}");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"Query for ip {ip} failed! {ex.Message}");
            }

            return false;
        }

        private static void LogBackgroundWorker()
        {
            while (true)
            {
                if (Messages.Count > 0)
                {
                    var messageToWrite = Messages.Dequeue();
                    File.AppendAllLines(LogDestination, new List<string>
                    {
                        $"[{messageToWrite.Item2}] {messageToWrite.Item1}"
                    }, Encoding.UTF8);
                }
                else
                {
                    Thread.Sleep(1000 * 5);
                }
            }
        }

        private static void WriteLog(string message, bool isError = false)
        {
            Messages.Enqueue((message, DateTime.Now));
            if (isError)
                Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[{DateTime.Now}] {message}");

            Console.ForegroundColor = ConsoleColor.White;
        }

        private static void LoadWhitelist(bool refreshOnChange)
        {
            var file = Path.Combine(Environment.CurrentDirectory, "whitelist.txt");
            
            UpdateWhitelist(file);

            if (refreshOnChange)
            {
                _fileSystemWatcher = new FileSystemWatcher(file);
                _fileSystemWatcher.Changed += (sender, args) => { UpdateWhitelist(args.FullPath); };
            }
        }

        private static void UpdateWhitelist(string file)
        {
            if (File.Exists(file))
            {
                _whitelistedUser = File.ReadAllLines(file).ToList();
                WriteLog("Whitelist loaded!");
            }
            else
            {
                WriteLog("No whitelist found, created empty file!");
                File.Create(file);
            }
        }

        private class VpnTestResponse
        {
            public string ip { get; set; }
            public string organization { get; set; }
            public string asn { get; set; }
            public string type { get; set; }
            public string risk { get; set; }
            public string is_hosting { get; set; }
            public string vpn_or_proxy { get; set; }
        }
    }
}
