# Expense Manager

Expense Manager is a full-stack personal finance app for recording expenses and incomes, tracking recurring income, attaching receipts, and reviewing monthly cash-flow summaries.

The project contains:

- **Frontend:** React 19 and Vite
- **Backend:** ASP.NET Core 9 Web API with JWT authentication
- **Database:** MySQL 8.4, managed through Entity Framework Core migrations
- **Runtime:** Docker Compose support for the complete stack

## Features

- Register and sign in with secure access and refresh tokens.
- Add, edit, and archive expenses and income entries.
- Create recurring income sources, such as salary or freelance work.
- Upload attachments to expenses, for example receipts and screenshots.
- Review monthly balances, daily allowances, and recent activity.
- Switch between light and dark themes.
- Keep each user's financial records separate.

## Quick start with Docker

This is the easiest way to download and run the project locally. You need [Git](https://git-scm.com/downloads) and [Docker Desktop](https://www.docker.com/products/docker-desktop/) installed.

1. Clone the repository and enter its folder:

   ```bash
   git clone https://github.com/BluEDr/ExpenseProject.git
   cd ExpenseProject
   ```

   Alternatively, use **Code -> Download ZIP** on the repository page, extract the archive, and open a terminal in the extracted `ExpenseProject` folder.

2. Start the frontend, API, and database:

   ```bash
   docker compose up --build
   ```

3. Open the application at [http://localhost:5173](http://localhost:5173).

4. Select **Register**, create an account, then start adding income and expenses.

The API is available at `http://localhost:5000`. Swagger API documentation is available at [http://localhost:5000/swagger](http://localhost:5000/swagger) while the API runs in the Development environment.

To stop the application, press `Ctrl+C`. To run it again in the background:

```bash
docker compose up -d
```

## Deploying prebuilt images

For server deployments, use the registry-backed compose file instead of building locally.

1. Create and push a release tag, for example:

   ```bash
   git tag v1.1.3
   git push origin v1.1.3
   ```

2. Wait for the Azure Pipeline to publish these public images:

   - `ghcr.io/bluedr/expenseproject-api:v1.1.3`
   - `ghcr.io/bluedr/expenseproject-web:v1.1.3`

3. Clone the repository on the server:

   ```bash
   git clone https://github.com/BluEDr/ExpenseProject.git
   cd ExpenseProject
   ```

4. Create a `.env` file next to `docker-compose.prod.yml`:

   ```bash
   IMAGE_TAG=v1.1.3
   JWT_SECRET_KEY=CHANGE_ME_TO_A_LONG_RANDOM_SECRET_KEY_32_CHARS_MIN
   MYSQL_ROOT_PASSWORD=rootpass
   MYSQL_DATABASE=expenses
   MYSQL_USER=appuser
   MYSQL_PASSWORD=apppass
   TZ=Europe/Athens
   ```

5. Pull and start the tagged images:

   ```bash
   docker compose -f docker-compose.prod.yml pull
   docker compose -f docker-compose.prod.yml up -d
   ```

6. Check that the services are running:

   ```bash
   docker compose -f docker-compose.prod.yml ps
   docker compose -f docker-compose.prod.yml logs api --tail=100
   docker compose -f docker-compose.prod.yml logs web --tail=100
   ```

This keeps local development and server deployment separate:

- `docker-compose.yml` builds local images from source
- `docker-compose.prod.yml` pulls prebuilt images from GHCR

## Local development setup

### Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 22+](https://nodejs.org/)
- MySQL 8.4, or Docker Desktop to run only the database

### 1. Start MySQL

From the project root, start the database service:

```bash
docker compose up mysql -d
```

This starts MySQL on port `3306` using the development connection details already configured in `api/Expenses.Api/appsettings.json`.

### 2. Start the API

Open a terminal in the project root and run:

```bash
dotnet run --project api/Expenses.Api
```

The API starts at `http://localhost:5091`. Database migrations run automatically when it starts.

### 3. Start the frontend

Open a second terminal and run:

```bash
cd web
npm install
$env:VITE_API_BASE_URL = "http://localhost:5091"
npm run dev
```

Then visit the URL printed by Vite, normally [http://localhost:5173](http://localhost:5173).

On macOS or Linux, set the API URL with:

```bash
VITE_API_BASE_URL=http://localhost:5091 npm run dev
```

## Configuration

The following settings are used by the API. For development, they are in `api/Expenses.Api/appsettings.json`; Docker Compose supplies equivalent values through environment variables.

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings__Default` | MySQL connection string |
| `Jwt__SecretKey` | JWT signing key; set this in `.env` or environment variables for deployments |
| `Jwt__Issuer` / `Jwt__Audience` | JWT issuer and audience values |
| `Storage__AttachmentsPath` | Folder where uploaded receipt files are stored |
| `Cors__AllowedOrigins` | Comma-separated permitted frontend origins for deployed environments |
| `VITE_API_BASE_URL` | Frontend API base URL; useful when the API does not run on port 5000 |

> **Security note:** The checked-in credentials and JWT key are development defaults only. Change them before exposing the application or database to a network.

## Useful commands

Run frontend linting:

```bash
cd web
npm run lint
```

Create a production frontend build:

```bash
cd web
npm run build
```

Build the API:

```bash
dotnet build api/Expenses.Api/Expenses.Api.csproj
```

View running Docker services:

```bash
docker compose ps
```

View service logs:

```bash
docker compose logs -f
```

## Project structure

```text
ExpenseProject/
|-- api/Expenses.Api/       # ASP.NET Core API, authentication, data models, and migrations
|-- web/                    # React/Vite user interface
|-- uploads/                # Local receipt attachments (mounted into the API Docker container)
|-- docker-compose.yml      # Frontend, API, and MySQL services for local development
|-- docker-compose.prod.yml # Registry-backed deployment services
`-- ExpenseProject.sln      # .NET solution
```

## Data and reset

Docker keeps MySQL data in the named `mysql_data` volume and stores attachments in the local `uploads` folder. Stopping containers does not remove this data.

To remove the Docker database volume and start with a clean database, run:

```bash
docker compose down -v
```

This permanently deletes the database stored by Docker. Remove files in `uploads` separately only if you also want to delete saved attachments.
