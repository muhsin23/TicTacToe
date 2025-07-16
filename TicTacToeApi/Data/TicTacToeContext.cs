using Microsoft.EntityFrameworkCore;
using TicTacToeApi.Models;

namespace TicTacToeApi.Data
{
    public class TicTacToeContext : DbContext
    {
        public TicTacToeContext(DbContextOptions<TicTacToeContext> options)
            : base(options)
        {
            Console.WriteLine("TicTacToeContext initialized.");
        }

        public DbSet<Game> Games { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Game>().ToTable("Games");
            Console.WriteLine("OnModelCreating called.");
        }
    }
}