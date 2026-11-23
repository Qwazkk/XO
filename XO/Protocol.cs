using System.Collections.Generic;
using System.Text.Json;

namespace XO
{
    public enum MsgType
    {
        Login, CreateSession, JoinSession, Move, Chat,
        StateSnapshot, Error, GameOver, ProfileUpdate,
        RoomListRequest, RoomListResponse, StartGameCountdown, LeaveSession
    }

    public class NetMessage
    {
        public MsgType Type { get; set; }
        public string SessionId { get; set; }
        public string Nickname { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Symbol { get; set; }
        public string Text { get; set; }
        public int[] Board { get; set; }
        public int Winner { get; set; }
        public string Message { get; set; }
        public int BoardSize { get; set; }
        public int WinLength { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public List<RoomInfo> Rooms { get; set; }
        public int CountdownSeconds { get; set; }

        public string OpponentName { get; set; }
        public int OpponentWins { get; set; }
        public int OpponentLosses { get; set; }

        public string ToJson() => JsonSerializer.Serialize(this);
        public static NetMessage FromJson(string json) => JsonSerializer.Deserialize<NetMessage>(json);
    }

    public class RoomInfo
    {
        public string Id { get; set; }
        public int BoardSize { get; set; }
        public int PlayersCount { get; set; }
        public bool IsFull => PlayersCount >= 2;
    }
}