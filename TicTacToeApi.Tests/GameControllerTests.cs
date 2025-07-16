using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using TicTacToeApi.Data;
using TicTacToeApi.Models;
using TicTacToeApi.Services;
using Microsoft.Data.Sqlite;
using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Routing;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using System.Text.Json;

namespace TicTacToeApi.Tests
{
    public class GameControllerTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory _factory;
        private readonly SqliteConnection _connection;

        public GameControllerTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            _client = factory.CreateClient();

            // Initialize database
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TicTacToeContext>();
            dbContext.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _connection.Close();
            _connection.Dispose();
            _client.Dispose();
        }

        private async Task<Game> CreateNewGame()
        {
            var response = await _client.PostAsync("/api/Game", null);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"CreateNewGame Status: {response.StatusCode}, Content: {content}");
            response.EnsureSuccessStatusCode();
            var game = await response.Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(game);
            return game!;
        }

        [Fact]
        public async Task HealthCheck_ReturnsOk()
        {
            var response = await _client.GetAsync("/health");
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"HealthCheck Status: {response.StatusCode}, Content: {content}");
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("OK", content);
        }

        [Fact]
        public async Task GetGame_NonExistentId_ReturnsNotFound()
        {
            var response = await _client.GetAsync("/api/Game/nonexistent-id");
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"GetGame NonExistent Status: {response.StatusCode}, Content: {content}");
            Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreateAndGetGame_WorksCorrectly()
        {
            var game = await CreateNewGame();
            var response = await _client.GetAsync($"/api/Game/{game.Id}");
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"GetGame Status: {response.StatusCode}, Content: {content}");
            response.EnsureSuccessStatusCode();
            var fetchedGame = await response.Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(fetchedGame);
            Assert.Equal(game.Id, fetchedGame!.Id);
            Assert.Equal("         ", fetchedGame.Board);
            Assert.Equal("X", fetchedGame.CurrentPlayer);
            Assert.Equal("Active", fetchedGame.Status);
        }

        [Fact]
        public async Task MakeMove_ValidMove_UpdatesGame()
        {
            var game = await CreateNewGame();
            var move = new { position = 0, player = "X" };
            var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"MakeMove Valid Status: {response.StatusCode}, Content: {content}");
            response.EnsureSuccessStatusCode();
            var updatedGame = await response.Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(updatedGame);
            Assert.Equal("X        ", updatedGame!.Board);
            Assert.Equal("O", updatedGame.CurrentPlayer);
            Assert.Equal("Active", updatedGame.Status);
        }

        [Fact]
        public async Task MakeMove_InvalidPosition_ReturnsBadRequest()
        {
            var game = await CreateNewGame();
            var move = new { position = -1, player = "X" };
            var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"MakeMove Invalid Position Status: {response.StatusCode}, Content: {content}");
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Position must be between 0 and 8", content);
        }

        [Theory]
        [InlineData(0, "Z", "Player must be X or O")]
        [InlineData(0, "", "The Player field is required")]
        public async Task MakeMove_InvalidPlayer_ReturnsBadRequest(int position, string player, string expectedError)
        {
            var game = await CreateNewGame();
            var move = new { position, player };
            var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"MakeMove Invalid Player Status: {response.StatusCode}, Content: {content}");
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            var problemDetails = JsonSerializer.Deserialize<JsonElement>(content);
            var errors = problemDetails.GetProperty("errors");
            Assert.Contains("Player", errors.EnumerateObject().Select(e => e.Name));
            Assert.Contains(expectedError, errors.GetProperty("Player").EnumerateArray().Select(e => e.GetString()));
        }

        [Fact]
        public async Task MakeMove_MalformedInput_ReturnsBadRequest()
        {
            var game = await CreateNewGame();
            var malformedMove = new { position = 0 };
            var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", malformedMove);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"MakeMove Malformed Input Status: {response.StatusCode}, Content: {content}");
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            var problemDetails = JsonSerializer.Deserialize<JsonElement>(content);
            var errors = problemDetails.GetProperty("errors");
            Assert.Contains("Player", errors.EnumerateObject().Select(e => e.Name));
            Assert.Contains("The Player field is required", errors.GetProperty("Player").EnumerateArray().Select(e => e.GetString()));
        }

        [Fact]
        public async Task MakeMove_OutOfTurn_ReturnsBadRequest()
        {
            var game = await CreateNewGame();
            var move = new { position = 0, player = "O" }; // Wrong player
            var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"MakeMove OutOfTurn Status: {response.StatusCode}, Content: {content}");
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Invalid move", content);
        }

        [Fact]
        public async Task MakeMove_OnOccupiedPosition_ReturnsBadRequest()
        {
            var game = await CreateNewGame();
            var move1 = new { position = 0, player = "X" };
            await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move1);
            var move2 = new { position = 0, player = "O" }; // Same position
            var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move2);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"MakeMove Occupied Status: {response.StatusCode}, Content: {content}");
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Invalid move", content);
        }

        [Fact]
        public async Task MakeMove_AfterGameEnd_ReturnsBadRequest()
        {
            var game = await CreateNewGame();
            var moves = new[] {
                new { position = 0, player = "X" },
                new { position = 1, player = "O" },
                new { position = 2, player = "X" },
                new { position = 4, player = "O" },
                new { position = 3, player = "X" },
                new { position = 5, player = "O" },
                new { position = 7, player = "X" },
                new { position = 6, player = "O" },
                new { position = 8, player = "X" }
            };
            foreach (var move in moves)
            {
                var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
                Console.WriteLine($"MakeMove Draw Status: {response.StatusCode}, Content: {await response.Content.ReadAsStringAsync()}");
                response.EnsureSuccessStatusCode();
            }
            var moveAfterDraw = new { position = 0, player = "O" };
            var responseAfter = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", moveAfterDraw);
            var content = await responseAfter.Content.ReadAsStringAsync();
            Console.WriteLine($"MakeMove After Draw Status: {responseAfter.StatusCode}, Content: {content}");
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, responseAfter.StatusCode);
            Assert.Contains("Invalid move", content);
        }

        [Theory]
        [InlineData(new[] { 0, 3, 1, 4, 2 }, "X")] // Horizontal win (0,1,2)
        [InlineData(new[] { 0, 1, 3, 4, 6 }, "X")] // Vertical win (0,3,6)
        [InlineData(new[] { 0, 1, 4, 2, 8 }, "X")] // Diagonal win (0,4,8)
        [InlineData(new[] { 3, 0, 4, 1, 5 }, "X")] // Horizontal win (3,4,5)
        public async Task Game_EndsWithWin_WhenThreeInRow(int[] positions, string expectedWinner)
        {
            var game = await CreateNewGame();
            var players = new[] { "X", "O" };
            for (int i = 0; i < positions.Length; i++)
            {
                var move = new { position = positions[i], player = players[i % 2] };
                var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"MakeMove Win Status: {response.StatusCode}, Content: {content}");
                response.EnsureSuccessStatusCode();
            }
            var finalGame = await (await _client.GetAsync($"/api/Game/{game.Id}")).Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(finalGame);
            Assert.Equal("Won", finalGame!.Status);
            Assert.Equal(expectedWinner, finalGame.CurrentPlayer);
        }

        [Fact]
        public async Task MakeMove_VerticalWinCondition_ReturnsGameWon()
        {
            var game = await CreateNewGame();
            var moves = new[] {
                new { position = 0, player = "X" }, // Top-left
                new { position = 1, player = "O" }, // Top-center
                new { position = 3, player = "X" }, // Middle-left
                new { position = 4, player = "O" }, // Middle-center
                new { position = 6, player = "X" }  // Bottom-left (vertical win)
            };
            foreach (var move in moves)
            {
                var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"MakeMove Vertical Win Status: {response.StatusCode}, Content: {content}");
                response.EnsureSuccessStatusCode();
            }
            var finalGame = await (await _client.GetAsync($"/api/Game/{game.Id}")).Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(finalGame);
            Assert.Equal("Won", finalGame!.Status);
            Assert.Equal("X", finalGame.CurrentPlayer);
        }

        [Fact]
        public async Task MakeMove_DiagonalWinCondition_ReturnsGameWon()
        {
            var game = await CreateNewGame();
            var moves = new[] {
                new { position = 0, player = "X" }, // Top-left
                new { position = 1, player = "O" }, // Top-center
                new { position = 4, player = "X" }, // Center
                new { position = 2, player = "O" }, // Top-right
                new { position = 8, player = "X" }  // Bottom-right (diagonal win)
            };
            foreach (var move in moves)
            {
                var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"MakeMove Diagonal Win Status: {response.StatusCode}, Content: {content}");
                response.EnsureSuccessStatusCode();
            }
            var finalGame = await (await _client.GetAsync($"/api/Game/{game.Id}")).Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(finalGame);
            Assert.Equal("Won", finalGame!.Status);
            Assert.Equal("X", finalGame.CurrentPlayer);
        }

        [Fact]
        public async Task Game_EndsWithDraw_WhenBoardFull()
        {
            var game = await CreateNewGame();
            var moves = new[] {
                new { position = 0, player = "X" },
                new { position = 1, player = "O" },
                new { position = 2, player = "X" },
                new { position = 4, player = "O" },
                new { position = 3, player = "X" },
                new { position = 5, player = "O" },
                new { position = 7, player = "X" },
                new { position = 6, player = "O" },
                new { position = 8, player = "X" }
            };
            foreach (var move in moves)
            {
                var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"MakeMove Draw Status: {response.StatusCode}, Content: {content}");
                response.EnsureSuccessStatusCode();
            }
            var finalGame = await (await _client.GetAsync($"/api/Game/{game.Id}")).Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(finalGame);
            Assert.Equal("Draw", finalGame!.Status);
        }

        [Fact]
        public async Task GameState_PersistsBetweenRequests()
        {
            var game = await CreateNewGame();
            var move = new { position = 0, player = "X" };
            await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
            var response = await _client.GetAsync($"/api/Game/{game.Id}");
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"GameState Persists Status: {response.StatusCode}, Content: {content}");
            response.EnsureSuccessStatusCode();
            var fetchedGame = await response.Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(fetchedGame);
            Assert.Equal("X        ", fetchedGame!.Board);
            Assert.Equal("O", fetchedGame.CurrentPlayer);
        }

        [Fact]
        public async Task Database_CanBeInitialized()
        {
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TicTacToeContext>();
            var game = new Game
            {
                Id = Guid.NewGuid().ToString(),
                Board = "         ",
                CurrentPlayer = "X",
                Status = "Active",
                ETag = Guid.NewGuid().ToString()
            };
            dbContext.Games.Add(game);
            await dbContext.SaveChangesAsync();
            var savedGame = await dbContext.Games.FindAsync(game.Id);
            Assert.NotNull(savedGame);
            Console.WriteLine($"Database test: Saved game ID {savedGame?.Id}");
        }

        [Fact]
        public void CheckControllerAvailability()
        {
            using var scope = _factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            var controller = services.GetService<TicTacToeApi.Controllers.GameController>();
            Assert.NotNull(controller);
            Console.WriteLine("Controller GameController is registered.");
        }
    }

    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        public SqliteConnection Connection { get; } = new SqliteConnection("Data Source=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<TicTacToeContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddDbContext<TicTacToeContext>(options =>
                    options.UseSqlite(Connection));
                services.AddControllers()
                    .AddApplicationPart(typeof(TicTacToeApi.Controllers.GameController).Assembly);
                services.AddScoped<GameService>();
                services.AddScoped<TicTacToeApi.Controllers.GameController>();
                services.AddEndpointsApiExplorer();
                services.AddSwaggerGen();
            });

            builder.Configure(app =>
            {
                Connection.Open();
                using var scope = app.ApplicationServices.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TicTacToeContext>();
                try
                {
                    dbContext.Database.EnsureCreated();
                    Console.WriteLine("Database schema created successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Database creation failed: {ex.Message}");
                    throw;
                }

                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();
                    endpoints.MapGet("/health", () => "OK");
                });

                if (app.ApplicationServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Connection.Close();
                Connection.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}