using Microsoft.AspNetCore.Mvc;
using TicTacToeApi.Services;
using TicTacToeApi.Models;

namespace TicTacToeApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameController : ControllerBase
    {
        private readonly GameService _gameService;

        public GameController(GameService gameService)
        {
            _gameService = gameService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateGame()
        {
            var game = await _gameService.CreateGameAsync();
            return Ok(game);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetGame(string id)
        {
            var game = await _gameService.GetGameAsync(id);
            if (game == null) return NotFound();
            return Ok(game);
        }

        [HttpPost("{id}/moves")]
        public async Task<IActionResult> MakeMove(string id, [FromBody] Move move)
        {
            if (move.Position < 0 || move.Position > 8)
            {
                return BadRequest("Invalid move: Position must be between 0 and 8.");
            }
            if (move.Player != "X" && move.Player != "O")
            {
                return BadRequest("Invalid move: Player must be X or O.");
            }
            var game = await _gameService.MakeMoveAsync(id, move.Position, move.Player);
            if (game == null) return BadRequest("Invalid move or game not found.");
            return Ok(game);
        }
    }
}