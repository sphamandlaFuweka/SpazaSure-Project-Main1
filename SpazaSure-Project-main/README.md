# SpazaSure Platform

B2B2C digital ecosystem connecting **Suppliers**, **Spaza Shops**, and **Consumers** in South Africa.

---

## Project Structure

```
Spaza Project/
├── docs/                          # Architecture & business documents
├── spazasure_app/                 # Spaza Shop Mobile App (Flutter) ✅ EXISTING
├── supplier-portal/               # Supplier Web Portal (React.js + TypeScript) ✅ NEW
└── consumer_app/                  # Consumer Verification App (Flutter) ✅ NEW
```

---

## Local Setup (from scratch, on any machine)

### Prerequisites
- .NET SDK 9 (covers the net8.0 services and the net9.0 Gateway)
- PostgreSQL 16 (or any recent Postgres)
- Node.js + npm
- Flutter SDK (Dart ≥3.11 — any Flutter release from the last several months)

### 1. Database
#### Option A: Docker (recommended)
```bash
cd SpazaSure.Backend
docker compose -f docker-compose.local.yml up -d
```
This starts PostgreSQL on `localhost:5432`, applies `migrate.sql`, and loads
the demo data from `seed_data.sql`. To stop the database while keeping its
data, run `docker compose -f docker-compose.local.yml down`.

#### Option B: Existing PostgreSQL installation
```bash
# Create the database (matches the default connection string in each
# service's appsettings.json — change both if you use different credentials)
createdb -U postgres spazasure   # password: admin

cd SpazaSure.Backend
psql -U postgres -d spazasure -f migrate.sql     # schema + baseline roles/categories
psql -U postgres -d spazasure -f seed_data.sql    # demo users, supplier, products, orders
```
`seed_data.sql` is safe to re-run — every statement is idempotent. It creates:

| Role | Email | Password |
|------|-------|----------|
| Admin | `admin@spazasure.co.za` | `Password123!` |
| Supplier | `thabo@freshfoods.co.za` | `Password123!` |
| Spaza shop owner | `sipho@cornershop.co.za` | `Password123!` |

...plus a fully compliant demo supplier (documents pre-approved, so the
Supplier Portal's Products/Orders pages aren't gated), a product catalog,
a few sample orders in different statuses, all four subscription tiers
(Basic/Bronze/Silver/Gold — the Upgrade page was otherwise empty on a fresh
DB), and one active group buy with a participant.

> Previously `seed_data.sql` only `UPDATE`d a supplier row assumed to
> already exist — on a fresh database it silently did nothing (or failed on
> FK constraints for the product inserts). It's now self-contained.

### 2. Backend services
```bash
cd SpazaSure.Backend
# Windows:
./StartAll.ps1
# macOS/Linux:
./start-all.sh
```
Both scripts resolve paths relative to their own location now, so they work
regardless of where you've cloned the repo (previously `StartAll.ps1` had a
Windows path from the original developer's machine hardcoded into it).
Gateway Swagger UI: http://localhost:5181/swagger

### 3. Supplier Portal
```bash
cd supplier-portal
npm install
npm run dev        # http://localhost:3000
```
`.env.development` now points at your local Gateway (`http://localhost:5181/api`)
by default rather than a shared external QA server.

### 4. Spaza Shop App (Flutter)
```bash
cd spazasure_app
flutter pub get
flutter run --dart-define=API_URL=http://10.0.2.2:5181/api    # Android emulator
flutter run --dart-define=API_URL=http://localhost:5181/api   # iOS simulator/desktop/web
```
The API URL now defaults sensibly per platform instead of being hardcoded to
one external QA server — see `spazasure_app/BUILD_GUIDE.md` for all the
`--dart-define` options, including pointing back at the shared QA server.

---

## 1. Supplier Web Portal (`supplier-portal/`)

**Stack:** React 18 + TypeScript + Vite + Tailwind CSS + Zustand + Recharts

### Features
- Login / Register with form validation (Zod + React Hook Form)
- Dashboard with KPI cards and revenue/orders charts
- Product catalog management (add, edit, toggle availability, delete)
- Order management with status workflow (pending → processing → dispatched → delivered)
- Analytics with revenue trends, order distribution, top products
- Supplier profile & compliance document management

### Setup
```bash
cd supplier-portal
cp .env.example .env
npm install
npm run dev        # http://localhost:3000
npm run build      # Production build
```

### API Integration
Replace mock data in `src/services/api.ts` with your C# microservice endpoints.
The API base URL is configured via `VITE_API_URL` in `.env`.

---

## 2. Consumer Verification App (`consumer_app/`)

**Stack:** Flutter + Provider + mobile_scanner + shared_preferences

### Features
- Onboarding flow (3 screens)
- QR code scanner with custom overlay
- Instant verification result (Authentic ✅ / Warning ⚠️ / Counterfeit ❌ / Not Found)
- Product details: brand, supplier, certifications, batch/expiry info
- Safety tips for counterfeit/warning results
- Report counterfeit form
- Scan history (persisted locally)
- Manual code entry fallback

### Setup
```bash
cd consumer_app
flutter pub get
flutter run
```

### API Integration
Set `API_URL` via `--dart-define`:
```bash
flutter run --dart-define=API_URL=http://your-api.com/api
```

---

## 3. Spaza Shop App (`spazasure_app/`)

**Stack:** Flutter (existing)

Already built with marketplace, ordering, cart, delivery tracking, compliance, and notifications.

---

## 4. C# Microservices Backend

### Required Endpoints for Supplier Portal
| Method | Endpoint | Service |
|--------|----------|---------|
| POST | `/api/supplier/auth/login` | Auth Service |
| POST | `/api/supplier/auth/register` | Auth Service |
| GET/POST/PUT/DELETE | `/api/supplier/products` | Product Service |
| GET/PATCH | `/api/supplier/orders` | Order Service |
| GET | `/api/supplier/analytics/summary` | Analytics Service |
| GET | `/api/supplier/analytics/revenue` | Analytics Service |
| GET/PUT | `/api/supplier/profile` | User Service |
| POST | `/api/supplier/profile/documents` | Compliance Service |

### Required Endpoints for Consumer App
| Method | Endpoint | Service |
|--------|----------|---------|
| GET | `/api/verify/{qrCode}` | Verification Service |
| POST | `/api/verify/report` | Compliance Service |

---

## Architecture Alignment

| Component | Technology | Status |
|-----------|-----------|--------|
| Supplier Web Portal | React.js + TypeScript | ✅ Built |
| Spaza Shop App | Flutter | ✅ Existing |
| Consumer App | Flutter | ✅ Built |
| Admin Dashboard | React.js + TypeScript | 🔜 Phase 2 |
| API Gateway | AWS API Gateway / Kong | 🔜 Backend |
| Auth Service | C# .NET | 🔜 Backend |
| Product Service | C# .NET | 🔜 Backend |
| Order Service | C# .NET | 🔜 Backend |
| Payment Service | C# .NET | 🔜 Backend |
| Verification Service | C# .NET | 🔜 Backend |
| Analytics Service | C# .NET | 🔜 Backend |

---

## Phase Roadmap

- **Phase 1 (MVP):** Supplier portal + Spaza shop app + basic admin + EFT payments ✅
- **Phase 2:** Consumer app + group buying + wallet + premium subscriptions ✅
- **Phase 3:** Advanced analytics + financial services + advertising platform
