namespace TicTacToeApi.Models;

public class MoveRequest
{
    public int Position { get; set; }
    public required string Player { get; set; }
}