# Tests

Two projects, split by what they need to run.

| Project | Needs | Run time |
|---|---|---|
| `Bookshelf.UnitTests` | nothing | under a second |
| `Bookshelf.E2ETests` | the running application, a database and Chrome | a few minutes |

That split is the reason they are separate projects rather than folders: `dotnet test` on the unit
tests has to work on a machine where nothing is started.

## Unit tests

Pure functions from the application layer — the streak calculation and the reading position rule.
No database, no mocks, no HTTP.

```bash
dotnet test tests/Bookshelf.UnitTests
```

## End-to-end tests

Selenium drives Chrome through five flows: register and log in, search Open Library and add a
book, change a book's status, log reading progress, and the recommendation strip on the shelf.
Each test registers its own account, so runs do not interfere with each other and the suite can be
repeated as often as you like.

### Before running

1. **Database** — `docker compose up -d db`
2. **Seed corpus** — `docker compose --profile seed run --rm seed`
   Only the recommendation tests need it; without it they report *inconclusive* rather than
   failing, because an empty corpus is a setup problem and not a defect.
3. **API** — `dotnet run` in `backend/Bookshelf.Api` (listens on `http://localhost:8080`)
4. **Frontend** — `npm start` in `frontend` (serves `http://localhost:4200` and proxies `/api`)

The embedding service is *not* required. Adding a book and asking for recommendations both treat
an unreachable embedding service as a book without a vector rather than as an error, so the flows
above work without it.

### Running

```bash
dotnet test tests/Bookshelf.E2ETests
```

Everything at once, from the solution:

```bash
dotnet test backend/Bookshelf.sln                        # unit + E2E
dotnet test backend/Bookshelf.sln --filter "Category!=E2E"  # unit only
```

The E2E fixtures are all marked `[Category("E2E")]` so the second command works on a machine where
the application is not running.

One wrinkle with the solution-wide command: it builds `Bookshelf.Api` too, and a running API holds
its own DLLs open on Windows. That is fine as long as the API has not been edited since it was
started — MSBuild has nothing to copy — but after changing API code you have to stop it before the
build can replace the files. Running the two test projects directly avoids the question.

### Settings

All optional; the defaults match the setup above.

| Variable | Default | Meaning |
|---|---|---|
| `E2E_BASE_URL` | `http://localhost:4200` | Where the application is served |
| `E2E_HEADLESS` | `false` | `true` hides the browser |
| `E2E_TIMEOUT_SECONDS` | `15` | Wait for anything the application renders |
| `E2E_NETWORK_TIMEOUT_SECONDS` | `30` | Wait for the one flow that calls Open Library |
| `E2E_SCREENSHOT_DIR` | test output folder | Where failure screenshots are written |

```bash
E2E_HEADLESS=true dotnet test tests/Bookshelf.E2ETests
```

### If a test fails

A failing test writes a screenshot of the browser at the moment it gave up, and prints the address
it was on. Between them they usually separate "the application broke" from "the test looked in the
wrong place".

Two failures are worth reading differently:

- **Everything fails with one message about a process not answering.** The suite checks that the
  frontend and `/api/health` are reachable before it opens a browser, so this means something in
  the list above is not running.
- **Only the search flow fails.** It is the one test that reaches Open Library over the internet.
  Their search is occasionally slow or rate limited, and that is a failure of the network rather
  than of this code.

### Test data

Accounts are named `e2e-<flow>-<timestamp>-<random>@bookshelf.test` and are never cleaned up.
Deleting a reader would mean building an endpoint that exists only to serve its own tests; the
`bookshelf.test` domain makes the leftovers easy to spot and remove by hand.

### Locators

Tests address the page through `data-testid` attributes, never through CSS class paths, so
restyling a component does not break them. `Support/Find.cs` is the only place that knows how a
locator is spelled — including the PrimeNG wrinkle that an attribute on `<p-button>` sits on the
host element while the real `<button>` is rendered inside it.
