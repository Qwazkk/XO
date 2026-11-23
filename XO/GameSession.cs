using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace XO
{
    public class GameSession
    {
        public string Id { get; }
        private Board _board;
        private List<ClientConnection> _players = new List<ClientConnection>();
        private int CurrentSymbol = 1;
        private bool IsOver = false;
        private bool _isStarting = false;

        public GameSession(string id, int size, int winLength)
        {
            Id = id;
            _board = new Board(size, winLength);
        }

        public void Join(ClientConnection conn)
        {
            if (_players.Count >= 2 || _players.Contains(conn)) return;

            conn.PlayerSymbol = _players.Count == 0 ? 1 : 2;
            conn.CurrentSessionId = Id;
            _players.Add(conn);

            if (_players.Count == 1)
            {
                _ = Task.Run(async () => await conn.SendAsync(new NetMessage
                {
                    Type = MsgType.RoomListResponse,
                    Message = "Ожидание второго игрока...",
                    SessionId = Id
                }));
            }
            else if (_players.Count == 2 && !_isStarting)
            {
                _isStarting = true;
                _ = Task.Run(async () => await StartCountdownAsync());
            }
        }

        public void Leave(ClientConnection conn)
        {
            if (_players.Contains(conn))
            {
                _players.Remove(conn);
                conn.CurrentSessionId = null;

                if (!IsOver)
                {
                    IsOver = true;
                    _ = Task.Run(async () => await BroadcastAsync(new NetMessage
                    {
                        Type = MsgType.Error,
                        Message = "Противник покинул игру."
                    }));
                }

                if (_players.Count == 0)
                {
                    ServerManager.Sessions.Remove(Id);
                }
            }
        }

        private async Task StartCountdownAsync()
        {
            try
            {
                await BroadcastAsync(new NetMessage { Type = MsgType.StartGameCountdown });
                await Task.Delay(5000);
                await BroadcastStateAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[СЕРВЕР] Ошибка старта: " + ex.Message);
            }
        }

        public RoomInfo GetInfo()
        {
            return new RoomInfo { Id = this.Id, BoardSize = _board.Size, PlayersCount = _players.Count };
        }

        public async Task TryMoveAsync(ClientConnection conn, int x, int y)
        {
            if (IsOver) return;

            if (conn.PlayerSymbol != CurrentSymbol)
            {
                await conn.SendAsync(new NetMessage { Type = MsgType.Error, Message = "Сейчас не ваш ход!" });
                return;
            }
            if (!_board.IsCellEmpty(x, y))
            {
                await conn.SendAsync(new NetMessage { Type = MsgType.Error, Message = "Клетка занята!" });
                return;
            }

            _board.SetCell(x, y, conn.PlayerSymbol);

            if (_board.CheckLine(x, y, conn.PlayerSymbol))
            {
                IsOver = true;
                await ProcessGameOverAsync(conn.PlayerSymbol);
                return;
            }

            if (_board.IsFull())
            {
                IsOver = true;
                await ProcessGameOverAsync(0);
                return;
            }

            CurrentSymbol = CurrentSymbol == 1 ? 2 : 1;
            await BroadcastStateAsync();
        }

        private async Task ProcessGameOverAsync(int winnerSymbol)
        {
            foreach (var p in _players)
            {
                p.UpdateProfileStats(winnerSymbol);
                p.CurrentSessionId = null;

                await p.SendAsync(new NetMessage
                {
                    Type = MsgType.GameOver,
                    Winner = winnerSymbol,
                    Wins = p.Profile?.Wins ?? 0,
                    Losses = p.Profile?.Losses ?? 0,
                    Board = _board.Cells
                });
            }

            ServerManager.Sessions.Remove(Id);
        }

        public async Task BroadcastStateAsync()
        {
            foreach (var p in _players)
            {
                var opponent = _players.Find(x => x != p);
                await p.SendAsync(new NetMessage
                {
                    Type = MsgType.StateSnapshot,
                    SessionId = Id,
                    Board = _board.Cells,
                    Symbol = p.PlayerSymbol,
                    BoardSize = _board.Size,
                    WinLength = _board.WinLength,
                    Nickname = p.Profile?.Nickname ?? "Вы",
                    Wins = p.Profile?.Wins ?? 0,
                    Losses = p.Profile?.Losses ?? 0,
                    OpponentName = opponent?.Profile?.Nickname ?? "Ожидание...",
                    OpponentWins = opponent?.Profile?.Wins ?? 0,
                    OpponentLosses = opponent?.Profile?.Losses ?? 0
                });
            }
        }

        public async Task BroadcastAsync(NetMessage msg)
        {
            foreach (var p in _players) await p.SendAsync(msg);
        }
    }
}