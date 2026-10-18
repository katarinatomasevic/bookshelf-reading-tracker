# Bookshelf

A web application for personal reading tracking. The user searches Open Library, adds books to
their shelf, tracks daily progress and sees statistics about their reading — streak, activity
chart, books read per month and most common genres. On top of that they get personalized book
recommendations, based on vector similarity to the titles they have already rated.


---

## Features

**Users** — registration and login with a JWT token, protected routes and a profile page for
changing name and password; a guest can search and view book details, but cannot add to the shelf.

**Book search and adding** — Open Library search through a .NET proxy, with a details page
(description, authors, aggregate rating) and manual entry for a book Open Library does not have.

**Personal shelf** — books in three statuses (`Want to read` / `Reading` / `Read`), with a modal for
changing status, rating, note, dates and start page, and search within the active tab.

**Progress tracking and dashboard** — daily entry of pages read, and on top of it a streak, an
activity chart for the last 26 weeks, statistics cards and charts of books per month and top genres.

**AI recommendations** — personalized recommendations from a corpus of ~15,000 books, based on
vector similarity to the titles the user rated highly, with an explanation of which book each
recommendation comes from.

---

## Technologies

| Layer | Technology | Why |
|---|---|---|
| Backend | ASP.NET Core 9 (C#) + EF Core 9 | |
| Frontend | Angular 20 + PrimeNG | Standalone components and signals, no separate state library |
| Database | PostgreSQL 16 + **pgvector** | Vector similarity lives in the same database as the data, no separate vector store |
| AI service | Python + FastAPI + sentence-transformers | The models worth using live in the Python ecosystem; .NET calls it over HTTP |
| Authentication | JWT in `localStorage` | Stateless — no token table and no database query per request |
| External API | Open Library | No API key and no registration |
| Tests | NUnit + Selenium WebDriver | |

---

## Architecture

### Backend — four .NET projects

```
Bookshelf.Api  ──────►  Bookshelf.Application  ──────►  Bookshelf.Domain
      │                          ▲                             ▲
      └──────────────────────────┴─────────────────────────────┘
                       Bookshelf.Infrastructure
```

- **`Bookshelf.Domain`** — entities (`User`, `Book`, `UserBook`, `ReadingLog`) and enums. No
  dependencies on anything else in the solution.
- **`Bookshelf.Application`** — services, DTOs, repository interfaces, exceptions and pure
  business-rule functions (`StreakCalculator`, `ReadingPosition`, `PasswordPolicy`).
- **`Bookshelf.Infrastructure`** — `AppDbContext`, EF Fluent API configuration, migrations,
  repository implementations, clients for Open Library and the embedding service.
- **`Bookshelf.Api`** — controllers, JWT setup, global exception middleware, DI.

### Frontend — `core` / `features` / `shared`

- **`core`** — services, guards, interceptors, models, validators, utils (once per application)
- **`features`** — `auth`, `books`, `shelf`, `dashboard`, `recommendations`, `profile`; routes are
  lazy-loaded through `loadComponent`
- **`shared`** — components used by more than one feature (`book-card`, progress entry)

### Repository structure

```
bookshelf-reading-tracker/
├─ docker-compose.yml          database (db) + embedding service + seed job
├─ .env.example                list of required variables
├─ corpus.json                 harvested seed corpus (~15,000 books)
├─ backend/
│  ├─ Bookshelf.sln
│  ├─ Bookshelf.Api/           controllers, Program.cs, demo seed flag
│  ├─ Bookshelf.Application/   services, DTOs, business rules
│  ├─ Bookshelf.Domain/        entities and enums
│  └─ Bookshelf.Infrastructure/
│     ├─ ExternalServices/     Open Library and embedding clients
│     └─ Persistence/
│        ├─ Configurations/    EF Fluent API
│        ├─ Migrations/
│        ├─ Repositories/
│        └─ DemoData/          demo account seed for the defense
├─ frontend/                   Angular project
├─ embedding/
│  ├─ app/                     FastAPI service (/embed, /embed/batch, /health)
│  └─ seed/                    harvest.py and seed.py
└─ tests/
   ├─ Bookshelf.UnitTests/     pure functions, no database
   └─ Bookshelf.E2ETests/      Selenium, five flows
```

---

## Prerequisites

| Tool | Version used in development |
|---|---|
| .NET SDK | 9.0.301 |
| Node.js | 22.14.0 (+ npm) |
| Docker Desktop | for the database and the embedding service |
| Google Chrome | only for E2E tests |

---

## Running

**The order matters.** The backend applies EF migrations on startup (`Database.Migrate()` in
`Program.cs`), so it crashes on startup if the database is not running. Recommendations, on the
other hand, work without errors even before the corpus is seeded — they just return an empty list
because they have nothing to choose from.

### 1. Configuration

```bash
cp .env.example .env
```

Fill in the values (see [Configuration](#configuration) below). For running the backend locally,
`backend/Bookshelf.Api/appsettings.Development.json` is enough as well.

### 2. Database and embedding service (Docker)

```bash
docker compose up db embedding -d
docker compose ps        # both should be "Up (healthy)"
```

The database is on host port **5433** (not 5432, so it does not clash with a local Postgres
installation). The embedding service builds its image the first time and bakes the model into it,
so that build takes a few minutes.

### 3. Migrations

They are not run manually. `Program.cs` calls `Database.Migrate()` on startup, so the schema is
applied automatically the first time the backend runs (next step).

### 4. Corpus seed

```bash
docker compose --profile seed run --rm seed
```

Reads `corpus.json` from the repository, writes the books into the `Book` table and fills in their
vectors. Takes a couple of minutes. The script is idempotent — a second run writes nothing.

> `harvest.py` (which produces `corpus.json` from Open Library) **does not need to be run** — the
> result is already in the repository. The harvest takes 20–40 minutes and depends on the network.

### 5. Backend

```bash
cd backend/Bookshelf.Api
dotnet run
```

Listens on `http://localhost:8080`. Applies migrations on the first run.

### 6. Frontend

```bash
cd frontend
npm install      # first time only
npm start
```

Listens on `http://localhost:4200`, with a proxy `/api → http://localhost:8080`.

### 7. Demo data

```bash
dotnet run --project backend/Bookshelf.Api -- --seed-demo
```

Creates a demo account with 14 books in all three statuses, ratings, notes and reading history going
back 26 weeks (an active streak and a filled activity chart). Requires step 4 to be done already,
because it picks books from the seeded corpus. When finished, it prints a data consistency check.

**Login credentials:**

```
email:    katarina@bookshelf.com
password: Citam2026!
```

If the account already exists, the script does nothing. The reading history is generated relative
to **the day it is run**, so an account seeded a week ago shows a broken streak. To refresh it:

```bash
dotnet run --project backend/Bookshelf.Api -- --seed-demo --refresh
```

This deletes and recreates **only that account** — no other account and no book is touched.

The application is then at `http://localhost:4200`.

---

## Configuration

Configuration is env-driven: values are read from `appsettings.json` / `appsettings.Development.json`,
and environment variables override them. A double underscore (`__`) in a variable name corresponds
to a colon in the configuration key (`Jwt__Secret` → `Jwt:Secret`).

| Variable | Read by | Meaning |
|---|---|---|
| `POSTGRES_USER` | `docker-compose.yml` | Database user |
| `POSTGRES_PASSWORD` | `docker-compose.yml` | Database password |
| `POSTGRES_DB` | `docker-compose.yml` | Database name |
| `ConnectionStrings__Default` | backend | Connection string; the port is **5433** because that is what is mapped to the host |
| `Jwt__Secret` | backend | Key for signing the JWT (HMAC SHA256). The only real secret |
| `OpenLibrary__UserAgent` | backend | Contact string that Open Library asks for; without it you get rate limited |
| `Embedding__BaseUrl` | backend | Address of the Python embedding service (`http://localhost:8000` from the host) |
| `HARVEST_USER_AGENT` | `harvest.py` | Same as above, for the harvest script |

`Jwt:Issuer`, `Jwt:Audience` and `Jwt:ExpiryDays` are not secrets and live in `appsettings.json`.

`.env` is gitignored; `.env.example` is committed as documentation of which variables exist.
`appsettings.json` deliberately has **no** connection string and no `Jwt:Secret` — without the
development file the application crashes on startup instead of silently using the wrong database.

---

## Running the tests

Two projects, split by what they need in order to run.

### Unit tests — need nothing

```bash
dotnet test tests/Bookshelf.UnitTests
```

They cover two pure functions: `StreakCalculator` (streak calculation) and `ReadingPosition`
(`StartPage + SUM(PagesRead)`, skipping the "want to read" status, the page count limit). No
database, no mocks, no HTTP — they run in under a second.

### E2E tests — need the running application

```bash
dotnet test tests/Bookshelf.E2ETests
```

Selenium drives Chrome through five flows: registration and login, searching and adding a book,
changing status, entering progress, and the recommendations strip.

**Prerequisites, both required:**

1. **The application must be running** — database, backend (`:8080`) and frontend (`:4200`). A
   check before the browser opens reports in one sentence what is missing.
2. **The database must have the seeded corpus** (step 4 above). The recommendations test draws from
   the corpus; without it the test does not fail but reports *inconclusive*, with the command in the
   message.

The tests do not depend on each other — each one registers its own account, so they can be run in
any order and as many times as needed. They do not need the demo account.

### Unit tests only, without the application running

```bash
dotnet test backend/Bookshelf.sln --filter "Category!=E2E"
```

Every E2E fixture carries `[Category("E2E")]` precisely for this.

### Headless browser

```bash
E2E_HEADLESS=true dotnet test tests/Bookshelf.E2ETests
```

By default the browser is visible.

More on the individual tests: [`tests/README.md`](tests/README.md).

---

## Notes

**`corpus.json` is in the repository** (7.7 MB as a file, about 2 MB in the repository itself,
because git compresses it). It is the result of the Open Library harvest: ~15,000 books with title,
author, genres, page count, popularity rank and the topic they came from. It is committed on
purpose — without it, anyone cloning the repository would have to repeat the 20–40 minute harvest,
which depends on the network and can fail halfway. This way the database is reproducible in two
minutes. The harvest itself is separate from the embedding for exactly that reason: the expensive,
fragile part is done once.

**Known limitations:**

- Open Library keeps **several records for the same novel** (e.g. *Pride and Prejudice* exists
  under two work keys). Deduplication on insert is done strictly by `OpenLibraryId`, so two such
  records legitimately exist separately. Recommendations additionally compare the normalized title
  and author, but conservatively — by exact match — so an edition with a different title (omnibus,
  collection) can occasionally slip through as a separate recommendation.
- The corpus is a sample of 50 topics, not a catalog. A well-known book can be missing, and among
  the popular titles there are also editions in other languages — that is the data Open Library
  returns.
- The recommendation attribution ("Because you liked X") is a nearest-neighbor explanation: it is
  known **that** the candidate is close to the source, not **why**.
- The book details cache is in process memory — it disappears when the backend restarts and is not
  shared between instances.
- Progress entry accepts dates up to 30 days back, to prevent a mistyped year. Correcting and
  deleting an existing entry have no such limit.
