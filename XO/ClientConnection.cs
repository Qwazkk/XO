using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace XO
{
    public class ClientConnection
    {
        private readonly TcpClient _tcp;
        private readonly NetworkStream _stream;

        public int PlayerSymbol { get; set; }
        public string CurrentSessionId { get; set; }
        public PlayerProfile? Profile { get; private set; }

        public ClientConnection(TcpClient tcp)
        {
            _tcp = tcp;
            _stream = tcp.GetStream();
        }

        public void UpdateProfileStats(int winnerSymbol)
        {
            if (Profile == null) return;
            if (winnerSymbol == 0) return;

            if (winnerSymbol == PlayerSymbol) Profile.Wins++;
            else Profile.Losses++;

            ServerManager.SaveProfiles();
        }

        public async Task HandleAsync()
        {
            try
            {
                while (true)
                {
                    byte[] lenBuf = new byte[4];
                    int bytesRead = 0;
                    while (bytesRead < 4)
                    {
                        int r = await _stream.ReadAsync(lenBuf, bytesRead, 4 - bytesRead);
                        if (r == 0) return;
                        bytesRead += r;
                    }

                    int len = BitConverter.ToInt32(lenBuf, 0);
                    if (len <= 0 || len > 1024 * 1024 * 10) return;

                    byte[] dataBuf = new byte[len];
                    bytesRead = 0;
                    while (bytesRead < len)
                    {
                        int r = await _stream.ReadAsync(dataBuf, bytesRead, len - bytesRead);
                        if (r == 0) return;
                        bytesRead += r;
                    }

                    var json = Encoding.UTF8.GetString(dataBuf);
                    var msg = NetMessage.FromJson(json);

                    if (msg != null) await ProcessAsync(msg);
                }
            }
            catch (Exception) { }
            finally
            {
                _tcp.Close();
                if (CurrentSessionId != null && ServerManager.Sessions.ContainsKey(CurrentSessionId))
                {
                    ServerManager.Sessions[CurrentSessionId].Leave(this);
                }
            }
        }

        private async Task ProcessAsync(NetMessage msg)
        {
            switch (msg.Type)
            {
                case MsgType.Login:
                    if (!ServerManager.Profiles.ContainsKey(msg.Nickname))
                    {
                        ServerManager.Profiles[msg.Nickname] = new PlayerProfile { Nickname = msg.Nickname, Wins = 0, Losses = 0 };
                        ServerManager.SaveProfiles();
                    }
                    Profile = ServerManager.Profiles[msg.Nickname];

                    await SendAsync(new NetMessage
                    {
                        Type = MsgType.ProfileUpdate,
                        Nickname = Profile.Nickname,
                        Wins = Profile.Wins,
                        Losses = Profile.Losses
                    });
                    break;

                case MsgType.RoomListRequest:
                    var deadRooms = ServerManager.Sessions
                        .Where(x => x.Value.GetInfo().PlayersCount == 0)
                        .Select(x => x.Key)
                        .ToList();
                    foreach (var dr in deadRooms)
                        ServerManager.Sessions.Remove(dr);

                    var rooms = ServerManager.Sessions.Values.Select(s => s.GetInfo()).ToList();
                    await SendAsync(new NetMessage
                    {
                        Type = MsgType.RoomListResponse,
                        Rooms = rooms
                    });
                    break;

                case MsgType.CreateSession:
                    if (CurrentSessionId != null && ServerManager.Sessions.ContainsKey(CurrentSessionId))
                    {
                        ServerManager.Sessions[CurrentSessionId].Leave(this);
                    }
                    CurrentSessionId = null;

                    if (ServerManager.Sessions.ContainsKey(msg.SessionId))
                    {
                        await SendAsync(new NetMessage { Type = MsgType.Error, Message = "Комната уже существует!" });
                        break;
                    }

                    CurrentSessionId = msg.SessionId;
                    var session = new GameSession(msg.SessionId, msg.BoardSize, msg.WinLength);
                    ServerManager.Sessions[msg.SessionId] = session;
                    session.Join(this);
                    break;

                case MsgType.JoinSession:
                    if (CurrentSessionId != null)
                    {
                        if (ServerManager.Sessions.ContainsKey(CurrentSessionId))
                        {
                            ServerManager.Sessions[CurrentSessionId].Leave(this);
                        }
                        CurrentSessionId = null;
                    }

                    if (!ServerManager.Sessions.ContainsKey(msg.SessionId))
                    {
                        await SendAsync(new NetMessage { Type = MsgType.Error, Message = "Сессия не найдена." });
                        break;
                    }

                    var targetSession = ServerManager.Sessions[msg.SessionId];
                    if (targetSession.GetInfo().IsFull)
                    {
                        await SendAsync(new NetMessage { Type = MsgType.Error, Message = "Комната заполнена." });
                        break;
                    }

                    CurrentSessionId = msg.SessionId;
                    targetSession.Join(this);
                    break;

                case MsgType.Move:
                    if (CurrentSessionId != null && ServerManager.Sessions.ContainsKey(CurrentSessionId))
                    {
                        await ServerManager.Sessions[CurrentSessionId].TryMoveAsync(this, msg.X, msg.Y);
                    }
                    break;

                case MsgType.Chat:
                    if (CurrentSessionId != null && ServerManager.Sessions.ContainsKey(CurrentSessionId))
                    {
                        if (Profile == null) return;

                        var filteredText = ChatFilter.Filter(msg.Text);
                        await ServerManager.Sessions[CurrentSessionId].BroadcastAsync(new NetMessage
                        {
                            Type = MsgType.Chat,
                            Text = $"{Profile.Nickname}: {filteredText}"
                        });
                    }
                    break;

                case MsgType.LeaveSession:
                    if (CurrentSessionId != null && ServerManager.Sessions.ContainsKey(CurrentSessionId))
                    {
                        ServerManager.Sessions[CurrentSessionId].Leave(this);
                    }
                    CurrentSessionId = null;

                    if (Profile != null)
                    {
                        await SendAsync(new NetMessage
                        {
                            Type = MsgType.ProfileUpdate,
                            Nickname = Profile.Nickname,
                            Wins = Profile.Wins,
                            Losses = Profile.Losses
                        });
                    }

                    var roomsAfterLeave = ServerManager.Sessions.Values.Select(s => s.GetInfo()).ToList();
                    await SendAsync(new NetMessage { Type = MsgType.RoomListResponse, Rooms = roomsAfterLeave });
                    break;
            }
        }

        public async Task SendAsync(NetMessage msg)
        {
            try
            {
                var data = Encoding.UTF8.GetBytes(msg.ToJson());
                var lenBytes = BitConverter.GetBytes(data.Length);

                await _stream.WriteAsync(lenBytes, 0, lenBytes.Length);
                await _stream.WriteAsync(data, 0, data.Length);
            }
            catch { }
        }
    }
}