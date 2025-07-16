using System;

namespace TicTacToeApi.Models
{
    public class Game
    {
        public required string Id { get; set; }
        public required string Board { get; set; }
        public required string CurrentPlayer { get; set; }
        public required string Status { get; set; }
        public required string ETag { get; set; }
    }
}