namespace TicTacToeApi.Models;

public class ErrorResponse
{
    public required string Type { get; set; }
    public required string Title { get; set; }
    public int Status { get; set; }
    public required string Detail { get; set; }
}