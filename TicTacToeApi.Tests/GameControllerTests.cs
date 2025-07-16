using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using TicTacToeApi.Data;
using TicTacToeApi.Models;
using TicTacToeApi.Services;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using System;
using System.Linq;
using Microsoft.Data.Sqlite;

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
        }

        [Fact]
        public async Task HealthCheck_ReturnsOk()
        {
            var response = await _client.GetAsync("/health");
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Status: {response.StatusCode}, Content: {content}");
            response.EnsureSuccessStatusCode();
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
        public async Task MakeMove_GameNotActive_ReturnsBadRequest()
        {
            var createResponse = await _client.PostAsync("/api/Game", null);
            createResponse.EnsureSuccessStatusCode();
            var game = await createResponse.Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(game);
            if (game != null)
            {
                // Simulate a win to make game inactive
                var moves = new[] {
                    new { position = 0, player = "X" },
                    new { position = 3, player = "O" },
                    new { position = 1, player = "X" },
                    new { position = 4, player = "O" },
                    new { position = 2, player = "X" }
                };
                foreach (var move in moves)
                {
                    await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
                }
                // Try to make a move on a won game
                var move = new { position = 5, player = "O" };
                var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"MakeMove Inactive Status: {response.StatusCode}, Content: {content}");
                Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("Invalid move", content);
            }
        }

        [Fact]
        public async Task MakeMove_WrongPlayer_ReturnsBadRequest()
        {
            var createResponse = await _client.PostAsync("/api/Game", null);
            createResponse.EnsureSuccessStatusCode();
            var game = await createResponse.Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(game);
            if (game != null)
            {
                var move = new { position = 0, player = "O" }; // Wrong player (should be X)
                var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"MakeMove Wrong Player Status: {response.StatusCode}, Content: {content}");
                Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("Invalid move", content);
            }
        }

        [Fact]
        public async Task MakeMove_OccupiedPosition_ReturnsBadRequest()
        {
            var createResponse = await _client.PostAsync("/api/Game", null);
            createResponse.EnsureSuccessStatusCode();
            var game = await createResponse.Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(game);
            if (game != null)
            {
                var move1 = new { position = 0, player = "X" };
                await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move1);
                var move2 = new { position = 0, player = "O" }; // Same position
                var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move2);
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"MakeMove Occupied Status: {response.StatusCode}, Content: {content}");
                Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("Invalid move", content);
            }
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
        public async Task CreateGame_ReturnsNewGame()
        {
            var response = await _client.PostAsync("/api/Game", null);
            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"CreateGame Status: {response.StatusCode}, Content: {content}, Headers: {response.Headers}");
            response.EnsureSuccessStatusCode();
            var game = await response.Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(game);
            if (game != null)
            {
                Assert.Equal("         ", game.Board);
                Assert.Equal("X", game.CurrentPlayer);
                Assert.Equal("Active", game.Status);
            }
        }

        [Fact]
        public async Task GetGame_ReturnsGame()
        {
            var createResponse = await _client.PostAsync("/api/Game", null);
            var createContent = await createResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"GetGame Create Status: {createResponse.StatusCode}, Content: {createContent}, Headers: {createResponse.Headers}");
            createResponse.EnsureSuccessStatusCode();
            var game = await createResponse.Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(game);
            if (game != null)
            {
                var response = await _client.GetAsync($"/api/Game/{game.Id}");
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"GetGame Status: {response.StatusCode}, Content: {content}, Headers: {response.Headers}");
                response.EnsureSuccessStatusCode();
                var fetchedGame = await response.Content.ReadFromJsonAsync<Game>();
                Assert.NotNull(fetchedGame);
                if (fetchedGame != null)
                {
                    Assert.Equal(game.Id, fetchedGame.Id);
                }
            }
        }

        [Fact]
        public async Task MakeMove_ValidMove_UpdatesGame()
        {
            var createResponse = await _client.PostAsync("/api/Game", null);
            var createContent = await createResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"MakeMove Create Status: {createResponse.StatusCode}, Content: {createContent}, Headers: {createResponse.Headers}");
            createResponse.EnsureSuccessStatusCode();
            var game = await createResponse.Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(game);
            if (game != null)
            {
                var move = new { position = 0, player = "X" };
                var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"MakeMove Status: {response.StatusCode}, Content: {content}, Headers: {response.Headers}");
                response.EnsureSuccessStatusCode();
                var updatedGame = await response.Content.ReadFromJsonAsync<Game>();
                Assert.NotNull(updatedGame);
                if (updatedGame != null)
                {
                    Assert.Equal("X        ", updatedGame.Board);
                    Assert.Equal("O", updatedGame.CurrentPlayer);
                }
            }
        }

        [Fact]
        public async Task MakeMove_InvalidPosition_ReturnsBadRequest()
        {
            var createResponse = await _client.PostAsync("/api/Game", null);
            createResponse.EnsureSuccessStatusCode();
            var game = await createResponse.Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(game);
            if (game != null)
            {
                var move = new { position = -1, player = "X" };
                var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"MakeMove Invalid Status: {response.StatusCode}, Content: {content}");
                Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("Invalid move", content);
            }
        }

        [Fact]
        public async Task MakeMove_WinCondition_ReturnsGameWon()
        {
            var createResponse = await _client.PostAsync("/api/Game", null);
            createResponse.EnsureSuccessStatusCode();
            var game = await createResponse.Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(game);
            if (game != null)
            {
                var moves = new[] {
                    new { position = 0, player = "X" },
                    new { position = 3, player = "O" },
                    new { position = 1, player = "X" },
                    new { position = 4, player = "O" },
                    new { position = 2, player = "X" }
                };
                foreach (var move in moves)
                {
                    var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"MakeMove Win Status: {response.StatusCode}, Content: {content}");
                    response.EnsureSuccessStatusCode();
                }
                var finalGame = await (await _client.GetAsync($"/api/Game/{game.Id}")).Content.ReadFromJsonAsync<Game>();
                Assert.NotNull(finalGame);
                if (finalGame != null)
                {
                    Assert.Equal("Won", finalGame.Status);
                    Assert.Equal("X", finalGame.CurrentPlayer);
                }
            }
        }

        [Fact]
        public async Task MakeMove_DrawCondition_ReturnsGameDraw()
        {
            var createResponse = await _client.PostAsync("/api/Game", null);
            createResponse.EnsureSuccessStatusCode();
            var game = await createResponse.Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(game);
            if (game != null)
            {
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
                if (finalGame != null)
                {
                    Assert.Equal("Draw", finalGame.Status);
                }
            }
        }

        [Fact]
        public async Task MakeMove_InvalidPlayer_ReturnsBadRequest()
        {
            var createResponse = await _client.PostAsync("/api/Game", null);
            createResponse.EnsureSuccessStatusCode();
            var game = await createResponse.Content.ReadFromJsonAsync<Game>();
            Assert.NotNull(game);
            if (game != null)
            {
                var move = new { position = 0, player = "Z" };
                var response = await _client.PostAsJsonAsync($"/api/Game/{game.Id}/moves", move);
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"MakeMove Invalid Player Status: {response.StatusCode}, Content: {content}");
                Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("Invalid move", content);
            }
        }

        [Fact]
        public void CheckControllerDiscovery()
        {
            using var scope = _factory.Server.Services.CreateScope();
            var endpointDataSource = _factory.Server.Services.GetRequiredService<EndpointDataSource>();
            var endpoints = endpointDataSource.Endpoints;
            var controllerActions = endpoints
                .Select(e => new
                {
                    Descriptor = e.Metadata.GetMetadata<ControllerActionDescriptor>(),
                    HttpMethod = e.Metadata.GetMetadata<HttpMethodActionConstraint>()
                })
                .Where(e => e.Descriptor != null)
                .Select(e => $"{e.Descriptor!.ControllerName}/{e.Descriptor!.ActionName} ({(e.HttpMethod?.HttpMethods?.FirstOrDefault() ?? "UNKNOWN")})");
            var routes = string.Join(", ", controllerActions);
            Console.WriteLine($"Discovered controller actions: {routes}");
            Assert.Contains("Game/CreateGame (POST)", controllerActions);
            Assert.Contains("Game/GetGame (GET)", controllerActions);
            Assert.Contains("Game/MakeMove (POST)", controllerActions);
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