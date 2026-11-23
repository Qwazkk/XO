using System;
using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace XO
{
    public partial class MainWindow : Window
    {
        private TcpClient _tcp;
        private NetworkStream _stream;
        private string _myNickname;
        private GameWindow _activeGameWindow;
        private bool _isInRoom = false;

        private DispatcherTimer _countdownTimer;
        private int _secondsLeft;

        public ObservableCollection<RoomItem> RoomsCollection { get; set; } = new ObservableCollection<RoomItem>();

        public MainWindow(string nickname)
        {
            InitializeComponent();
            _myNickname = nickname;
            RoomsList.ItemsSource = RoomsCollection;

            _countdownTimer = new DispatcherTimer();
            _countdownTimer.Interval = TimeSpan.FromSeconds(1);
            _countdownTimer.Tick += CountdownTimer_Tick;

            ServerManager.StartServer();
            Loaded += async (s, e) => await ConnectAsync();
        }

        private async Task ConnectAsync()
        {
            try
            {
                _tcp = new TcpClient();
                await Task.Delay(500);
                await _tcp.ConnectAsync("127.0.0.1", 5555);
                _stream = _tcp.GetStream();

                _ = Task.Run(ReceiveLoopAsync);

                await SendAsync(new NetMessage { Type = MsgType.Login, Nickname = _myNickname });
                await SendAsync(new NetMessage { Type = MsgType.RoomListRequest });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка подключения: " + ex.Message);
            }
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                while (_tcp.Connected)
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

                    var msg = NetMessage.FromJson(Encoding.UTF8.GetString(dataBuf));
                    if (msg != null) Dispatcher.Invoke(() => HandleMessage(msg));
                }
            }
            catch { }
        }

        private void HandleMessage(NetMessage msg)
        {
            if (_activeGameWindow != null && _activeGameWindow.IsVisible)
            {
                _activeGameWindow.HandleServerMessage(msg);
                return;
            }

            switch (msg.Type)
            {
                case MsgType.ProfileUpdate:
                    LobbyProfileName.Text = $"Имя: {msg.Nickname}";
                    LobbyProfileStats.Text = $"Побед: {msg.Wins} | Поражений: {msg.Losses}";
                    break;

                case MsgType.RoomListResponse:
                    if (!string.IsNullOrEmpty(msg.Message) && msg.Message.Contains("Ожидание"))
                    {
                        _isInRoom = true;
                        RoomListPanel.Visibility = Visibility.Collapsed;
                        CountdownPanel.Visibility = Visibility.Visible;
                        CountdownText.Text = "Ждем...";
                        CancelBtn.Visibility = Visibility.Visible;
                        return;
                    }

                    RoomsCollection.Clear();
                    if (msg.Rooms != null)
                    {
                        foreach (var r in msg.Rooms)
                        {
                            RoomsCollection.Add(new RoomItem
                            {
                                Id = r.Id,
                                DisplayText = $"Комната: {r.Id} | Поле: {r.BoardSize}x{r.BoardSize} | Игроков: {r.PlayersCount}/2"
                            });
                        }
                    }
                    break;

                case MsgType.StartGameCountdown:
                    RoomListPanel.Visibility = Visibility.Collapsed;
                    CountdownPanel.Visibility = Visibility.Visible;
                    CancelBtn.Visibility = Visibility.Collapsed;
                    _secondsLeft = 5;
                    CountdownText.Text = _secondsLeft.ToString();
                    _countdownTimer.Start();
                    break;

                case MsgType.StateSnapshot:
                    try
                    {
                        if (_activeGameWindow == null || !_activeGameWindow.IsVisible)
                        {
                            _countdownTimer.Stop();
                            _isInRoom = true;

                            _activeGameWindow = new GameWindow(_myNickname);
                            _activeGameWindow.OnSendMessage += async (m) => await SendAsync(m);

                            _activeGameWindow.Closed += async (s, ev) =>
                            {
                                _activeGameWindow = null;
                                _isInRoom = false;
                                this.Show();
                                RoomListPanel.Visibility = Visibility.Visible;
                                CountdownPanel.Visibility = Visibility.Collapsed;
                                CancelBtn.Visibility = Visibility.Collapsed;

                                await SendAsync(new NetMessage { Type = MsgType.LeaveSession });
                                await SendAsync(new NetMessage { Type = MsgType.RoomListRequest });
                            };
                            _activeGameWindow.Show();
                            this.Hide();
                        }
                        _activeGameWindow.HandleServerMessage(msg);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка запуска окна игры: " + ex.Message);
                    }
                    break;

                case MsgType.Error:
                    if (msg.Message != "Противник покинул игру.")
                    {
                        MessageBox.Show(msg.Message, "Ошибка");
                        _isInRoom = false;
                        _countdownTimer.Stop();
                        RoomListPanel.Visibility = Visibility.Visible;
                        CountdownPanel.Visibility = Visibility.Collapsed;
                        CancelBtn.Visibility = Visibility.Collapsed;
                        _ = SendAsync(new NetMessage { Type = MsgType.RoomListRequest });
                    }
                    break;
            }
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            _secondsLeft--;
            if (_secondsLeft > 0)
            {
                CountdownText.Text = _secondsLeft.ToString();
            }
            else
            {
                _countdownTimer.Stop();
                CountdownText.Text = "0";
            }
        }

        private async void OnCreateRoom(object s, RoutedEventArgs e)
        {
            if (_stream == null) { MessageBox.Show("Нет подключения."); return; }

            int size = ModeBox.SelectedIndex == 0 ? 3 : 25;
            int winLen = ModeBox.SelectedIndex == 0 ? 3 : 5;

            string roomId = "Room_" + _myNickname;

            await SendAsync(new NetMessage
            {
                Type = MsgType.CreateSession,
                SessionId = roomId,
                BoardSize = size,
                WinLength = winLen
            });

            await SendAsync(new NetMessage { Type = MsgType.RoomListRequest });
        }

        private async void OnRefreshRooms(object s, RoutedEventArgs e)
        {
            if (_stream != null)
                await SendAsync(new NetMessage { Type = MsgType.RoomListRequest });
        }

        private async void OnRoomDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (RoomsList.SelectedItem is RoomItem room && _stream != null)
            {
                await SendAsync(new NetMessage { Type = MsgType.JoinSession, SessionId = room.Id });
            }
        }

        private async void OnLeaveRoom(object s, RoutedEventArgs e)
        {
            if (_stream == null) return;

            _isInRoom = false;
            RoomListPanel.Visibility = Visibility.Visible;
            CountdownPanel.Visibility = Visibility.Collapsed;
            CancelBtn.Visibility = Visibility.Collapsed;

            await SendAsync(new NetMessage { Type = MsgType.LeaveSession });
        }

        private async Task SendAsync(NetMessage msg)
        {
            if (_stream == null) return;
            try
            {
                var data = Encoding.UTF8.GetBytes(msg.ToJson());
                var lenBytes = BitConverter.GetBytes(data.Length);

                await _stream.WriteAsync(lenBytes, 0, lenBytes.Length);
                await _stream.WriteAsync(data, 0, data.Length);
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            _tcp?.Close();
            ServerManager.StopServer();
            base.OnClosed(e);
            Application.Current.Shutdown();
        }
    }

    public class RoomItem
    {
        public string Id { get; set; }
        public string DisplayText { get; set; }
    }
}