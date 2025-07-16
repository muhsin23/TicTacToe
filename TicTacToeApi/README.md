Tic-Tac-Toe API

A RESTful API for playing Tic-Tac-Toe, built with ASP.NET Core 8.0, using PostgreSQL in production (Docker) and SQLite for local runs and tests, with unit tests using xUnit.

Features
- Create a new game (`POST /api/Game`)
- Retrieve a game by ID (`GET /api/Game/{id}`)
- Make a move (`POST /api/Game/{id}/moves`)
- Health check endpoint (`GET /health`)
- ETag-based optimistic concurrency
- Swagger UI for API documentation

Prerequisites
- .NET 8.0 SDK
- Docker and Docker Compose
- Git

Setup
1. Clone the repository:
  
   git clone https://github.com/<your-username>/TicTacToeApi.git
   cd TicTacToeApi

- Tests achieve high code coverage (>80% for GameService.cs and GameController.cs, see `coverage.opencover.xml`) with 21 unit tests.