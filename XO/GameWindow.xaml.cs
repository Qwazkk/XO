using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XO
{
    public partial class GameWindow : Window
    {
        private Button[,] _buttons;
        private int _mySymbol = 0;
        private int _currentBoardSize = 3;
        private bool _isGameEnded = false;
        private bool _isClosing = false;
        public event Action<NetMessage> OnSendMessage;

        public GameWindow(string myNickname)
        {
            InitializeComponent();
            YourNameText.Text = $"Вы: {myNickname}";
        }

        public void HandleServerMessage(NetMessage msg)
        {
            switch (msg.Type)
            {
                case MsgType.StateSnapshot:
                    _mySymbol = msg.Symbol;
                    if (_currentBoardSize != msg.BoardSize || _buttons == null)
                    {
                        _currentBoardSize = msg.BoardSize;
                        BuildBoard(_currentBoardSize);
                    }
                    RenderBoard(msg.Board);

                    GameStatus.Text = $"Комната: {msg.SessionId} | Вы за: {(_mySymbol == 1 ? "X" : "O")}";
                    YourStatsText.Text = $"Побед: {msg.Wins} | Поражений: {msg.Losses}";
                    OpponentNameText.Text = $"Противник: {msg.OpponentName}";
                    OpponentStatsText.Text = $"Побед: {msg.OpponentWins} | Поражений: {msg.OpponentLosses}";

                    if (!_isGameEnded)
                    {
                        StatusMessage.Text = "";
                    }
                    break;

                case MsgType.Chat:
                    LogChat(msg.Text);
                    break;

                case MsgType.Error:
                    if (msg.Message == "Противник покинул игру.")
                    {
                        _isGameEnded = true;
                        StatusMessage.Text = "Противник покинул игру";
                        StatusMessage.Foreground = Brushes.Red;
                        if (_buttons != null)
                            foreach (var btn in _buttons) btn.IsEnabled = false;
                    }
                    else
                    {
                        StatusMessage.Text = msg.Message;
                        StatusMessage.Foreground = Brushes.Red;
                    }
                    break;

                case MsgType.GameOver:
                    _isGameEnded = true;
                    if (msg.Winner == 0)
                    {
                        StatusMessage.Text = "Ничья!";
                        StatusMessage.Foreground = Brushes.Blue;
                    }
                    else if (msg.Winner == _mySymbol)
                    {
                        StatusMessage.Text = "Вы победили!";
                        StatusMessage.Foreground = Brushes.Green;
                    }
                    else
                    {
                        StatusMessage.Text = "Вы проиграли...";
                        StatusMessage.Foreground = Brushes.Red;
                    }

                    YourStatsText.Text = $"Побед: {msg.Wins} | Поражений: {msg.Losses}";

                    if (msg.Board != null) RenderBoard(msg.Board);
                    if (_buttons != null)
                        foreach (var btn in _buttons) btn.IsEnabled = false;
                    break;
            }
        }

        private void BuildBoard(int size)
        {
            BoardGrid.Children.Clear();
            BoardGrid.Rows = size;
            BoardGrid.Columns = size;

            if (size == 3) { BoardGrid.Width = 400; BoardGrid.Height = 400; }
            else { BoardGrid.Width = 750; BoardGrid.Height = 750; }

            _buttons = new Button[size, size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var b = new Button { Tag = (x, y), FontSize = size == 3 ? 40 : 12, Content = "", Margin = new Thickness(1) };
                    b.Click += OnCellClick;
                    _buttons[x, y] = b;
                    BoardGrid.Children.Add(b);
                }
        }

        private void RenderBoard(int[] cells)
        {
            if (cells == null) return;
            int size = _currentBoardSize;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = y * size + x;
                    var b = _buttons[x, y];
                    b.Content = cells[index] == 1 ? "X" : cells[index] == 2 ? "O" : "";
                    b.Foreground = cells[index] == 1 ? Brushes.Red : Brushes.Blue;
                    b.FontWeight = FontWeights.Bold;
                }
            }
        }

        private void OnCellClick(object sender, RoutedEventArgs e)
        {
            if (_mySymbol == 0 || _isGameEnded) return;
            var (x, y) = ((int, int))((Button)sender).Tag;
            OnSendMessage?.Invoke(new NetMessage { Type = MsgType.Move, X = x, Y = y });
        }

        private void OnSendChat(object s, RoutedEventArgs e) => SendChat();
        private void OnChatKey(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter) SendChat();
        }

        private void SendChat()
        {
            if (string.IsNullOrWhiteSpace(ChatBox.Text)) return;
            OnSendMessage?.Invoke(new NetMessage { Type = MsgType.Chat, Text = ChatBox.Text });
            ChatBox.Clear();
        }

        private void LogChat(string text)
        {
            ChatList.Items.Add(text);
            ChatList.ScrollIntoView(ChatList.Items[ChatList.Items.Count - 1]);
        }

        private void OnLeaveGame(object s, RoutedEventArgs e)
        {
            _isClosing = true;
            OnSendMessage?.Invoke(new NetMessage { Type = MsgType.LeaveSession });
            System.Threading.Thread.Sleep(100);
            this.Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isClosing)
            {
                _isClosing = true;
                OnSendMessage?.Invoke(new NetMessage { Type = MsgType.LeaveSession });
                System.Threading.Thread.Sleep(100);
            }
            base.OnClosing(e);
        }
    }
}