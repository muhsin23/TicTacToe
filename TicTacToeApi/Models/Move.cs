using System.ComponentModel.DataAnnotations;

namespace TicTacToeApi.Models
{
    public class Move
    {
        [Required(ErrorMessage = "The Player field is required")]
        [RegularExpression("^[XO]$", ErrorMessage = "Player must be X or O")]
        public string Player { get; set; } = null!;

        [Range(0, 8, ErrorMessage = "Position must be between 0 and 8")]
        public int Position { get; set; }
    }
}