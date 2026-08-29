# Bookshelf

Veb aplikacija za lično praćenje čitanja. Korisnik pretražuje Open Library, dodaje knjige na svoju
policu, prati napredak po danima i vidi statistiku svog čitanja — streak, grafik aktivnosti,
pročitane knjige po mesecima i najčešće žanrove. Uz to dobija personalizovane preporuke knjiga,
zasnovane na vektorskoj sličnosti sa naslovima koje je već ocenio.


---

## Funkcionalnosti

**Korisnici** — registracija i prijava sa JWT tokenom, zaštićene rute i profil sa promenom imena i
lozinke; neprijavljen korisnik može da pretražuje i gleda detalje, ali ne i da dodaje na policu.

**Pretraga i dodavanje knjiga** — pretraga Open Library-ja kroz .NET proxy, sa stranicom detalja
(opis, autori, agregatna ocena) i mogućnošću ručnog unosa knjige koju Open Library nema.

**Lična polica** — knjige u tri statusa (`Want to read` / `Reading` / `Read`), sa modalom u kom se
menjaju status, ocena, beleška, datumi i početna strana, i pretragom unutar aktivnog taba.

**Praćenje napretka i dashboard** — dnevni unos pročitanih strana, a nad njim streak, grafik
aktivnosti za poslednjih 26 nedelja, kartice sa statistikom i grafici knjiga po mesecu i top žanrova.

**AI preporuke** — personalizovane preporuke iz korpusa od ~15.000 knjiga, na osnovu vektorske
sličnosti sa naslovima koje je korisnik visoko ocenio, sa objašnjenjem iz koje knjige svaka
preporuka dolazi.

---

## Tehnologije

| Sloj | Tehnologija | Zašto |
|---|---|---|
| Backend | ASP.NET Core 9 (C#) + EF Core 9 | |
| Frontend | Angular 20 + PrimeNG | Standalone komponente i signali, bez zasebne state biblioteke |
| Baza | PostgreSQL 16 + **pgvector** | Vektorska sličnost živi u istoj bazi kao i podaci, bez zasebnog vector store-a |
| AI servis | Python + FastAPI + sentence-transformers | Modeli vredni korišćenja su u Python ekosistemu; .NET ga zove preko HTTP-a |
| Autentikacija | JWT u `localStorage` | Stateless — nema tabele tokena ni upita u bazu po zahtevu |
| Eksterni API | Open Library | Bez API ključa i bez registracije |
| Testovi | NUnit + Selenium WebDriver | |

---

## Arhitektura

### Backend — četiri .NET projekta

```
Bookshelf.Api  ──────►  Bookshelf.Application  ──────►  Bookshelf.Domain
      │                          ▲                             ▲
      └──────────────────────────┴─────────────────────────────┘
                       Bookshelf.Infrastructure
```

- **`Bookshelf.Domain`** — entiteti (`User`, `Book`, `UserBook`, `ReadingLog`) i enumi. Bez
  zavisnosti prema bilo čemu drugom u rešenju.
- **`Bookshelf.Application`** — servisi, DTO-i, interfejsi repozitorijuma, izuzeci i čiste funkcije
  poslovnih pravila (`StreakCalculator`, `ReadingPosition`, `PasswordPolicy`).
- **`Bookshelf.Infrastructure`** — `AppDbContext`, EF Fluent API konfiguracija, migracije,
  implementacije repozitorijuma, klijenti za Open Library i embedding servis.
- **`Bookshelf.Api`** — kontroleri, JWT podešavanje, globalni exception middleware, DI.

### Frontend — `core` / `features` / `shared`

- **`core`** — servisi, guardovi, interceptori, modeli, validatori, utils (jednom po aplikaciji)
- **`features`** — `auth`, `books`, `shelf`, `dashboard`, `recommendations`, `profile`; rute se
  učitavaju lenjo, kroz `loadComponent`
- **`shared`** — komponente koje koristi više feature-a (`book-card`, unos napretka)

### Struktura repozitorijuma

```
bookshelf-reading-tracker/
├─ docker-compose.yml          baza (db) + embedding servis + seed job
├─ .env.example                spisak potrebnih promenljivih
├─ corpus.json                 harvestovan seed korpus (~15.000 knjiga)
├─ backend/
│  ├─ Bookshelf.sln
│  ├─ Bookshelf.Api/           kontroleri, Program.cs, demo seed flag
│  ├─ Bookshelf.Application/   servisi, DTO-i, poslovna pravila
│  ├─ Bookshelf.Domain/        entiteti i enumi
│  └─ Bookshelf.Infrastructure/
│     ├─ ExternalServices/     Open Library i embedding klijenti
│     └─ Persistence/
│        ├─ Configurations/    EF Fluent API
│        ├─ Migrations/
│        ├─ Repositories/
│        └─ DemoData/          seed demo naloga za odbranu
├─ frontend/                   Angular projekat
├─ embedding/
│  ├─ app/                     FastAPI servis (/embed, /embed/batch, /health)
│  └─ seed/                    harvest.py i seed.py
└─ tests/
   ├─ Bookshelf.UnitTests/     čiste funkcije, bez baze
   └─ Bookshelf.E2ETests/      Selenium, pet tokova
```

---

## Preduslovi

| Alat | Verzija korišćena u razvoju |
|---|---|
| .NET SDK | 9.0.301 |
| Node.js | 22.14.0 (+ npm) |
| Docker Desktop | za bazu i embedding servis |
| Google Chrome | samo za E2E testove |

---

## Pokretanje

**Redosled je bitan.** Backend na startu primenjuje EF migracije (`Database.Migrate()` u
`Program.cs`), pa bez pokrenute baze pukne pri pokretanju. Preporuke, sa druge strane, rade bez greške i pre seed-a korpusa, samo vraćaju praznu listu
jer nemaju iz čega da biraju.

### 1. Konfiguracija

```bash
cp .env.example .env
```

Popuniti vrednosti (vidi [Konfiguracija](#konfiguracija) ispod). Za lokalno pokretanje backend-a
dovoljan je i `backend/Bookshelf.Api/appsettings.Development.json`.

### 2. Baza i embedding servis (Docker)

```bash
docker compose up db embedding -d
docker compose ps        # oba treba da budu "Up (healthy)"
```

Baza je na host portu **5433** (ne 5432, da ne kolidira sa lokalnom Postgres instalacijom).
Embedding servis prvi put gradi image i ugrađuje model u njega, pa taj build traje nekoliko minuta.

### 3. Migracije

Ne pokreću se ručno. `Program.cs` zove `Database.Migrate()` pri startu, pa se šema primeni sama pri
prvom pokretanju backend-a (sledeći korak).

### 4. Seed korpusa

```bash
docker compose --profile seed run --rm seed
```

Čita `corpus.json` iz repozitorijuma, upisuje knjige u tabelu `Book` i popunjava im vektore. Traje
par minuta. Skripta je idempotentna — drugo pokretanje ne upisuje ništa.

> `harvest.py` (koji `corpus.json` proizvodi sa Open Library-ja) **ne treba pokretati** — rezultat
> je već u repozitorijumu. Harvest traje 20–40 minuta i zavisi od mreže.

### 5. Backend

```bash
cd backend/Bookshelf.Api
dotnet run
```

Sluša na `http://localhost:8080`. Pri prvom pokretanju primenjuje migracije.

### 6. Frontend

```bash
cd frontend
npm install      # samo prvi put
npm start
```

Sluša na `http://localhost:4200`, sa proxy-jem `/api → http://localhost:8080`.

### 7. Demo podaci

```bash
dotnet run --project backend/Bookshelf.Api -- --seed-demo
```

Pravi demo nalog sa 14 knjiga u sva tri statusa, ocenama, beleškama i istorijom čitanja unazad kroz
26 nedelja (aktivan streak i popunjen grafik aktivnosti). Traži da je korak 4 već urađen, jer knjige
bira iz seedovanog korpusa. Po završetku ispisuje proveru konzistentnosti podataka.

**Podaci za prijavu:**

```
email:    katarina@bookshelf.com
lozinka:  Citam2026!
```

Ako nalog već postoji, skripta ne radi ništa. Istorija čitanja se generiše u odnosu na **dan
pokretanja**, pa nalog seedovan pre nedelju dana pokazuje prekinut streak. Za osvežavanje:

```bash
dotnet run --project backend/Bookshelf.Api -- --seed-demo --refresh
```

Ovo briše i ponovo pravi **samo taj nalog** — nijedan drugi nalog i nijedna knjiga se ne diraju.

Aplikacija je zatim na `http://localhost:4200`.

---

## Konfiguracija

Konfiguracija je env-driven: vrednosti se čitaju iz `appsettings.json` / `appsettings.Development.json`,
a promenljive okruženja ih nadjačavaju. Dvostruka donja crta (`__`) u imenu promenljive odgovara
dvotački u ključu konfiguracije (`Jwt__Secret` → `Jwt:Secret`).

| Promenljiva | Čita je | Značenje |
|---|---|---|
| `POSTGRES_USER` | `docker-compose.yml` | Korisnik baze |
| `POSTGRES_PASSWORD` | `docker-compose.yml` | Lozinka baze |
| `POSTGRES_DB` | `docker-compose.yml` | Ime baze |
| `ConnectionStrings__Default` | backend | Connection string; port je **5433** jer je toliko izmapirano na host |
| `Jwt__Secret` | backend | Ključ za potpisivanje JWT-a (HMAC SHA256). Jedina prava tajna |
| `OpenLibrary__UserAgent` | backend | Kontakt string koji Open Library traži; bez njega sledi rate limit |
| `Embedding__BaseUrl` | backend | Adresa Python embedding servisa (`http://localhost:8000` sa hosta) |
| `HARVEST_USER_AGENT` | `harvest.py` | Isto kao gore, za harvest skriptu |

`Jwt:Issuer`, `Jwt:Audience` i `Jwt:ExpiryDays` nisu tajne i stoje u `appsettings.json`.

`.env` je gitignorovan; `.env.example` se komituje kao dokumentacija koje promenljive postoje.
`appsettings.json` namerno **nema** connection string ni `Jwt:Secret` — bez razvojnog fajla
aplikacija pukne na startu umesto da tiho koristi pogrešnu bazu.

---

## Pokretanje testova

Dva projekta, razdvojena po tome šta im treba da bi radili.

### Unit testovi — ne traže ništa

```bash
dotnet test tests/Bookshelf.UnitTests
```

Pokrivaju dve čiste funkcije: `StreakCalculator` (računanje streak-a) i `ReadingPosition`
(`StartPage + SUM(PagesRead)`, preskakanje statusa „želim", granica broja strana). Bez baze, bez
mokova, bez HTTP-a — traju ispod sekunde.

### E2E testovi — traže pokrenutu aplikaciju

```bash
dotnet test tests/Bookshelf.E2ETests
```

Selenium vodi Chrome kroz pet tokova: registracija i prijava, pretraga i dodavanje knjige, promena
statusa, unos napretka, i traka sa preporukama.

**Preduslovi, oba obavezna:**

1. **Aplikacija mora biti pokrenuta** — baza, backend (`:8080`) i frontend (`:4200`). Provera pre
   otvaranja browsera javlja jednom rečenicom šta nedostaje.
2. **Baza mora imati seedovan korpus** (korak 4 iznad). Test preporuka crta iz korpusa; bez njega ne
   pada nego prijavi *inconclusive* sa komandom u poruci.

Testovi ne zavise jedan od drugog — svaki registruje sopstveni nalog, pa se mogu puštati bilo kojim
redom i koliko god puta. Demo nalog im nije potreban.

### Samo unit testovi, bez upaljene aplikacije

```bash
dotnet test backend/Bookshelf.sln --filter "Category!=E2E"
```

Svaki E2E fixture nosi `[Category("E2E")]` upravo zbog ovoga.

### Browser bez prozora

```bash
E2E_HEADLESS=true dotnet test tests/Bookshelf.E2ETests
```

Podrazumevano je browser vidljiv.

Detaljnije o pojedinačnim testovima: [`tests/README.md`](tests/README.md).

---

## Napomene

**`corpus.json` je u repozitorijumu** (7,7 MB kao fajl, oko 2 MB u samom repozitorijumu, jer ga git
kompresuje). To je rezultat harvesta sa Open Library-ja: ~15.000
knjiga sa naslovom, autorom, žanrovima, brojem strana, pozicijom po popularnosti i temom iz koje su
došle. Komitovan je namerno — bez njega bi svako ko klonira repozitorijum morao da ponovi harvest od
20–40 minuta, koji zavisi od mreže i može da pukne na pola. Ovako je baza reproducibilna za dva
minuta. Sam harvest je odvojen od embedovanja baš zato: skupi, krhki deo se radi jednom.

**Poznata ograničenja:**

- Open Library drži **više zapisa za isti roman** (npr. *Pride and Prejudice* postoji pod dva work
  ključa). Dedup pri upisu ide isključivo po `OpenLibraryId`, pa takva dva zapisa legitimno postoje
  odvojeno. Preporuke dodatno porede normalizovan naslov i autora, ali konzervativno — po tačnom
  poklapanju — pa poneko izdanje sa drugačijim naslovom (omnibus, kolekcija) može da prođe kao
  zasebna preporuka.
- Korpus je uzorak od 50 tema, ne katalog. Poznata knjiga može da izostane, a među popularnim
  naslovima ima i izdanja na drugim jezicima — to su podaci kakve Open Library vraća.
- Atribucija preporuke („Because you liked X") je objašnjenje kroz najbližeg suseda: zna se **da**
  je kandidat blizu izvoru, ne i **zašto**.
- Keš detalja knjige je u memoriji procesa — nestaje pri restartu backend-a i ne deli se između
  instanci.
- Unos napretka prihvata datume unazad do 30 dana, da bi se sprečila pogrešno ukucana godina.
  Ispravka i brisanje postojećeg unosa nemaju to ograničenje.
