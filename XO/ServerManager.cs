using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace XO
{
    public static class ServerManager
    {
        public static Dictionary<string, GameSession> Sessions { get; } = new Dictionary<string, GameSession>();
        public static Dictionary<string, PlayerProfile> Profiles { get; private set; } = new Dictionary<string, PlayerProfile>();

        private static string _statsFilePath = "stats.json";
        private static TcpListener _serverListener;
        private static bool _isServerRunning = false;

        public static void StartServer()
        {
            if (_isServerRunning) return;
            try
            {
                LoadProfiles();
                _serverListener = new TcpListener(IPAddress.Loopback, 5555);
                _serverListener.Start();
                _isServerRunning = true;

                Task.Run(async () =>
                {
                    while (_isServerRunning)
                    {
                        try
                        {
                            var tcp = await _serverListener.AcceptTcpClientAsync();
                            var conn = new ClientConnection(tcp);
                            _ = conn.HandleAsync();
                        }
                        catch { break; }
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка сервера: " + ex.Message);
            }
        }

        public static void StopServer()
        {
            if (!_isServerRunning) return;
            _isServerRunning = false;
            _serverListener?.Stop();
            _serverListener = null;
            Sessions.Clear();
        }

        public static void SaveProfiles()
        {
            try
            {
                var json = JsonSerializer.Serialize(Profiles);
                File.WriteAllText(_statsFilePath, json);
            }
            catch { }
        }

        private static void LoadProfiles()
        {
            if (File.Exists(_statsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_statsFilePath);
                    Profiles = JsonSerializer.Deserialize<Dictionary<string, PlayerProfile>>(json) ?? new();
                }
                catch { Profiles = new(); }
            }
        }
    }
}