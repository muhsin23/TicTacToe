using Microsoft.EntityFrameworkCore;
using TicTacToeApi.Data;
using TicTacToeApi.Models;

namespace TicTacToeApi.Services
{
    public class GameService
    {
        private readonly TicTacToeContext _context;

        public GameService(TicTacToeContext context)
        {
            _context = context;
        }

        public async Task<Game> CreateGameAsync()
        {
            var game = new Game
            {
                Id = Guid.NewGuid().ToString(),
                Board = "         ",
                CurrentPlayer = "X",
                Status = "Active",
                ETag = Guid.NewGuid().ToString()
            };
            _context.Games.Add(game);
            await _context.SaveChangesAsync();
            return game;
        }

        public async Task<Game?> GetGameAsync(string id)
        {
            return await _context.Games.FindAsync(id);
        }

        public async Task<Game?> MakeMoveAsync(string id, int position, string player)
        {
            var game = await _context.Games.FindAsync(id);
            if (game == null || game.Status != "Active")
            {
                return null;
            }
            if (game.Board[position] != ' ' || player != game.CurrentPlayer)
            {
                return null;
            }
            char[] board = game.Board.ToCharArray();
            board[position] = player[0];
            game.Board = new string(board);

            // Check win conditions before updating CurrentPlayer
            int[][] winConditions = new[] {
                new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 }, // Rows
                new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 }, // Columns
                new[] { 0, 4, 8 }, new[] { 2, 4, 6 } // Diagonals
            };
            foreach (var condition in winConditions)
            {
                if (board[condition[0]] != ' ' && board[condition[0]] == board[condition[1]] && board[condition[1]] == board[condition[2]])
                {
                    game.Status = "Won";
                    game.CurrentPlayer = player; // Keep winner as CurrentPlayer
                    break;
                }
            }
            // Check draw
            if (game.Status == "Active" && !board.Contains(' '))
            {
                game.Status = "Draw";
            }
            // Update CurrentPlayer only if game is still active
            if (game.Status == "Active")
            {
                game.CurrentPlayer = player == "X" ? "O" : "X";
            }
            game.ETag = Guid.NewGuid().ToString();
            await _context.SaveChangesAsync();
            return game;
        }
    }
}