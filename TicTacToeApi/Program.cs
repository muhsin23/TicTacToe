using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicTacToeApi.Data;
using TicTacToeApi.Services;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers();
        builder.Services.AddDbContext<TicTacToeContext>(options =>
        {
            if (builder.Environment.IsDevelopment())
            {
                options.UseSqlite("Data Source=tictactoe.db");
            }
            else
            {
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
            }
        });
        builder.Services.AddScoped<GameService>();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Ensure database is created
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TicTacToeContext>();
            context.Database.EnsureCreated();
        }

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthorization();
        app.MapControllers();
        app.MapGet("/health", () => "OK");

        app.Run();
    }
}