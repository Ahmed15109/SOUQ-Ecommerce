<div align="center">
  <h1>SOUQ E-Commerce</h1>
  <p><strong>An Arabic-first ASP.NET Core MVC e-commerce application supporting catalog discovery, authenticated ordering, weight-based pricing, pharmacy requests, and role-based administration.</strong></p>
  <p>A backend portfolio project centered on transactional integrity, access control, secure file handling, and relational data consistency.</p>
  <p>
    <img alt=".NET 9" src="https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&amp;logoColor=white">
    <img alt="ASP.NET Core MVC" src="https://img.shields.io/badge/ASP.NET_Core-MVC-512BD4">
    <img alt="Entity Framework Core 9.0.18" src="https://img.shields.io/badge/Entity_Framework_Core-9.0.18-512BD4">
    <img alt="SQL Server" src="https://img.shields.io/badge/SQL_Server-CC2927?logo=microsoftsqlserver&amp;logoColor=white">
    <img alt="xUnit" src="https://img.shields.io/badge/tests-xUnit-5C2D91">
    <a href="https://github.com/Ahmed15109/SOUQ-Ecommerce/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/Ahmed15109/SOUQ-Ecommerce/actions/workflows/ci.yml/badge.svg"></a>
  </p>
</div>

## Project Overview

SOUQ is a server-rendered, right-to-left storefront for everyday products and pharmacy requests. Its public catalog presents categories and featured products, supports product search and category filtering, and distinguishes between normally priced products and products sold by weight.

Customers can:

1. Browse featured products or a paginated catalog, filter by category, and search product names and descriptions.
2. Save favorites in session storage as a guest or in SQL Server as an authenticated user. Guest favorites merge into the user account after registration or sign-in.
3. Sign in, add normal or configured weight-based items to a persistent cart, manage quantities, and review delivery totals.
4. Complete a cash-on-delivery checkout with entered or saved address details. During checkout, the application revalidates products and current prices, creates the order once, clears the cart, and creates user and administrator notifications.
5. Review order history, status, delivery details, and line-item snapshots, then download an Arabic PDF invoice.
6. Submit a pharmacy request as a guest or authenticated user using medicine rows, a prescription attachment, or both. Authenticated users can review their request history while administrators process status changes.

Administrators use a dedicated ASP.NET Core MVC Area to manage the catalog, weight-price tiers, orders, pharmacy requests, notifications, and dashboard summaries. Super administrators can also create administrator accounts and lock or unlock them.

## Key Features

### Storefront and Catalog

- Arabic right-to-left Razor UI with locally hosted Cairo font, Bootstrap RTL, Bootstrap Icons, jQuery, and validation assets.
- Featured-product home page and category cards, including a direct pharmacy-request route for the pharmacy category.
- Paginated product listing with category filtering and name/description search.
- Normal pricing and weight-based pricing with minimum, maximum, and step validation.
- Optional weight-price tiers and an optional cutting service with a stored fee snapshot.
- Product favorites for guests and authenticated users, with session-to-account merging.

### Accounts and Authorization

- ASP.NET Core Identity registration, login, logout, password recovery, email confirmation, and email-token two-factor authentication.
- Optional Google authentication, enabled only when both OAuth credentials are configured.
- `User`, `Admin`, and `SuperAdmin` roles with role-specific navigation and controller authorization.
- Unique emails, password rules, failed-login lockout, and optional confirmed-email enforcement.
- Super administrator workflow for creating administrator accounts and controlling administrator lockout state.

### Cart, Checkout, and Orders

- Database-backed cart per authenticated user with separate uniqueness rules for normal and weighted cart lines.
- Quantity limits, transactional cart updates, price snapshots, and duplicate-cart protection.
- Saved delivery addresses with one default address per user.
- Cash-on-delivery checkout with server-side product and price revalidation.
- Serializable checkout transaction, checked decimal arithmetic, and user-scoped idempotency keys to prevent duplicate orders.
- Order history, details, controlled status transitions, and optimistic concurrency through row-version columns.
- User and administrator order notifications with per-administrator read tracking.
- Arabic PDF invoices generated with QuestPDF and the packaged Cairo font.

### Pharmacy Requests

- Guest and authenticated request submission.
- Up to 25 medicine rows, or a prescription attachment when medicine names are not entered.
- JPEG, PNG, WebP, and PDF attachment support with private storage outside `wwwroot`.
- User-scoped submission tokens to handle repeated authenticated submissions safely.
- Authenticated request history, protected attachment access, administrator review, and controlled status transitions.
- Administrator and user status notifications.

### Administration and Operations

- Dashboard totals plus recent orders and products.
- Category creation, editing, details, and deletion rules; seeded core categories cannot be deleted or have their visual identity changed.
- Product CRUD, featured-product selection, validated image uploads, and weight-configuration management.
- Non-overlapping weight-price tier management.
- Order filtering and status management.
- Pharmacy-request review and status management.
- Database readiness endpoint at `/health/ready`.
- Optional Redis-backed distributed session storage, with distributed in-memory storage as the default fallback.
- SQL Server retry-on-failure configuration and explicit startup options for migrations and ASP.NET Core Identity seeding.

## Technology Stack

| Area | Implementation |
| :--- | :--- |
| Runtime | .NET SDK 9.0.305; application and tests target `net9.0` |
| Framework | ASP.NET Core MVC with Razor Views, Areas, dependency injection, localization, sessions, rate limiting, and health checks |
| ORM | Entity Framework Core 9.0.18 with code-first migrations and the SQL Server provider |
| Database | Microsoft SQL Server |
| Authentication | ASP.NET Core Identity with Entity Framework Core stores; optional Google OAuth 9.0.18; SMTP account email delivery |
| Frontend | Razor, Bootstrap RTL 5.3.3, Bootstrap Icons 1.11.3, jQuery 3.7.1, jQuery Validation 1.21.0, jQuery Validation Unobtrusive |
| Caching and Session | `Microsoft.Extensions.Caching.StackExchangeRedis` 9.0.18 when Redis is configured; distributed memory fallback |
| PDF | QuestPDF 2026.7.1 with a locally packaged Cairo font |
| Testing | xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, ASP.NET Core MVC Testing 9.0.18, Entity Framework Core InMemory 9.0.18 |
| CI/CD | GitHub Actions for locked restore, whitespace validation, Release build, a solution-level `dotnet test` invocation, and publish verification; the test project is not included in the current solution, and no deployment workflow is included |
| Other | Data Annotations, custom decimal model binding, SMTP through `System.Net.Mail`, Dependabot for NuGet and GitHub Actions |

## Architecture

The repository contains one deployable ASP.NET Core MVC application and a separate test project. The current `EcommerceApp.sln` includes only the web application. The code follows the standard ASP.NET Core MVC request flow, with reusable business and infrastructure behavior extracted into services.

- `Program.cs` is the composition root. It registers ASP.NET Core Identity, Entity Framework Core, application services, options validation, optional Google authentication, optional Redis, sessions, localization, rate limits, security middleware, health checks, and routes.
- Public controllers coordinate storefront, account, cart, checkout, order, favorite, notification, address, and pharmacy workflows.
- `Areas/Admin` isolates role-protected administration controllers, views, and its administrator creation view model.
- Services provide reusable cross-controller behavior for product pricing, favorites, cart counts, notification state, email delivery, PDF generation, database health, claims creation, and file storage and scanning.
- `AppDbContext` defines ASP.NET Core Identity and commerce persistence, relationships, unique and filtered indexes, check constraints, seeded core categories, and delete behavior.
- Domain models define persisted data and validation rules; view models define form- and page-specific input/output contracts.
- Entity Framework Core migrations describe schema evolution. The `scripts` folder contains a read-only hardening preflight and a dry-run-first legacy pharmacy-attachment migration utility.

## Security

The repository implements the following controls:

- ASP.NET Core Identity password policy, unique-email enforcement, lockout after repeated failed sign-ins, optional confirmed email, email-token two-factor authentication, and verified-email checks for new Google accounts.
- Role-based authorization for administration and super-administration.
- Ownership checks on addresses, carts, order details, invoices, user notifications, pharmacy history, and pharmacy attachments. Unauthorized pharmacy attachment requests return `404` rather than disclosing resource existence.
- Anti-forgery validation on state-changing MVC actions and an anti-forgery header for AJAX cart and favorite requests.
- Secure `__Host-` authentication and session cookies with `HttpOnly`, `SameSite=Lax`, and `Secure` policies.
- Global, authentication, and upload rate limits partitioned by authenticated user or client IP.
- HTTPS redirection, production HSTS, configured forwarded-header trust, per-response Content Security Policy nonces, and restrictive content type, referrer, permissions, framing, object, and form policies.
- Data Annotation validation, explicit model-binding allowlists, length/range limits, Egyptian phone validation, bounded pagination, and normalized decimal input.
- File-size, extension, and file-signature checks; PDF trailer validation; generated file names; path-containment checks; temporary quarantine files; optional external malware scanning; and private pharmacy storage outside the public web root.
- User-scoped idempotency indexes, database uniqueness and check constraints, serializable transactions, price revalidation at checkout, and optimistic concurrency for mutable administrative records.
- Sensitive operational values are supported through User Secrets or environment variables rather than hard-coded credentials.

## Getting Started

### Prerequisites

- .NET SDK 9.0.305 or a compatible 9.0 latest patch selected by `global.json`
- Microsoft SQL Server or SQL Server LocalDB
- A trusted ASP.NET Core development HTTPS certificate
- Optional: Redis, Google OAuth credentials, SMTP credentials, and an external malware scanner

### Installation

```bash
git clone https://github.com/Ahmed15109/SOUQ-Ecommerce.git
cd SOUQ-Ecommerce
dotnet tool restore
dotnet restore EcommerceApp.sln --locked-mode
```

If necessary, trust the local HTTPS certificate:

```bash
dotnet dev-certs https --trust
```

### Configuration and User Secrets

The project declares a `UserSecretsId`. Keep connection strings, OAuth secrets, SMTP passwords, and initial administrator credentials outside committed configuration.

Set the SQL Server connection string for local development:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=<SQL_SERVER>;Database=<DATABASE_NAME>;Trusted_Connection=True;TrustServerCertificate=True"
```

Optional integrations use these configuration keys:

| Configuration | Purpose |
| :--- | :--- |
| `ConnectionStrings:Redis` | Enables Redis-backed distributed session storage |
| `Authentication:Google:ClientId` / `ClientSecret` | Enables the Google sign-in button and callback |
| `Email:Host`, `Port`, `EnableSsl`, `UserName`, `Password`, `FromAddress`, `FromName` | Enables confirmation, recovery, and two-factor email delivery |
| `Authentication:RequireConfirmedEmail` | Requires email confirmation before password sign-in |
| `SuperAdmin:Email` / `Password` | Creates the initial super administrator when identity seeding is enabled |
| `Uploads:RequireMalwareScan`, `MalwareScannerCommand`, `MalwareScannerArguments` | Controls external upload scanning |
| `QuestPDF:License` | Must be `Community`, `Professional`, or `Enterprise`; the checked-in default is `Community` |

Example optional secrets:

```bash
dotnet user-secrets set "Authentication:Google:ClientId" "<GOOGLE_CLIENT_ID>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<GOOGLE_CLIENT_SECRET>"
dotnet user-secrets set "Email:Password" "<SMTP_PASSWORD>"
dotnet user-secrets set "SuperAdmin:Email" "admin@example.com"
dotnet user-secrets set "SuperAdmin:Password" "<STRONG_PASSWORD>"
```

No default administrator credentials are stored in the repository. The Development environment enables identity seeding, which creates the application roles and creates a super administrator only when both super-administrator values are supplied.

### Database

Apply the committed Entity Framework Core migrations:

```bash
dotnet ef database update --project EcommerceApp.csproj --startup-project EcommerceApp.csproj
```

For an existing database created before the hardening migration, review `scripts/migration-preflight.sql` against a restored copy before applying migrations.

### Run

```bash
dotnet run --project EcommerceApp.csproj --launch-profile https
```

Open `https://localhost:7038`. The project also declares an HTTP address, but authentication and session cookies are configured for HTTPS.

## Project Structure

```text
SOUQ-Ecommerce/
|-- .config/                 Local dotnet-ef tool manifest
|-- .github/                 CI workflow and Dependabot configuration
|-- Areas/Admin/             Role-protected administration controllers, views, and view models
|-- Assets/Fonts/            Packaged Cairo font used by the UI and PDF invoices
|-- Constants/               Commerce limits and seeded core-category identifiers
|-- Controllers/             Public MVC endpoints and customer workflows
|-- Data/                    Entity Framework Core DbContext and Identity role/super-admin seeding
|-- Extensions/              Reusable paginated-query extension
|-- Helpers/                 Roles, Cairo time, enum display, and decimal model binding
|-- Migrations/              Entity Framework Core schema history and model snapshot
|-- Models/                  Identity, catalog, cart, order, notification, and pharmacy entities
|-- Options/                 Validated shop and email configuration models
|-- scripts/                 Database preflight and legacy attachment migration utilities
|-- Services/                Pricing, favorites, notifications, uploads, email, PDF, and health services
|-- tests/EcommerceApp.Tests/ xUnit unit, model-configuration, PDF, and application smoke tests
|-- ViewModels/              Form and page-specific MVC contracts
|-- Views/                   Public Razor views and shared layout/partials
|-- wwwroot/                 Local CSS, JavaScript, images, product images, and vendor assets
|-- Program.cs               Dependency registration and HTTP pipeline
|-- EcommerceApp.csproj      Web application package references and build settings
`-- EcommerceApp.sln         Web application solution; the test project is currently separate
```

## Testing

`tests/EcommerceApp.Tests` uses xUnit and contains:

- Application smoke tests using `WebApplicationFactory`, including security headers, local assets, private pharmacy paths, optional Google UI, and rate-limit partitioning.
- Product-pricing tests for normal products, weight tiers, cutting fees, and invalid weight selections.
- Entity Framework Core model tests for cart-line uniqueness and owner-scoped idempotency indexes.
- Domain validation and Arabic PDF generation tests.
- Pagination boundary and clamping tests.

The test project is not currently included in `EcommerceApp.sln`, so run it directly:

```bash
dotnet test tests/EcommerceApp.Tests/EcommerceApp.Tests.csproj --configuration Release
```

## GitHub Actions

The `CI` workflow runs on pull requests and pushes to `main` or `master`. It:

1. Checks out the repository.
2. Selects the SDK from `global.json` and enables NuGet caching.
3. Restores both projects in locked mode.
4. Runs `git diff --check`.
5. Builds the solution in Release configuration.
6. Invokes `dotnet test` for `EcommerceApp.sln`. Because the current solution does not include the xUnit project, this step does not execute the repository test suite.
7. Verifies that the web application publishes successfully.

Dependabot checks NuGet packages weekly and GitHub Actions monthly. The repository does not currently contain an automated deployment job.

## Roadmap

- Add the test project to the solution, or target it directly from CI, then expand integration coverage against SQL Server and Redis.
- Add containerized local infrastructure and a documented production deployment path.
- Add structured application metrics and tracing alongside the existing database readiness check.
- Move remaining interface text into localization resources and add automated accessibility and RTL regression checks.

## My Contributions — Ahmed Abdelmonem

SOUQ E-Commerce was independently designed, developed, and maintained by Ahmed Abdelmonem. He was responsible for:

- Designing and implementing the complete ASP.NET Core MVC backend architecture and business logic.
- Implementing all application controllers and every customer-facing and administrative workflow.
- Defining the Entity Framework Core models and relationships, creating migrations, and integrating the application with SQL Server.
- Building the storefront, catalog, cart, checkout, favorites, addresses, orders, notifications, and pharmacy-request workflows.
- Configuring and implementing ASP.NET Core Identity, role-based authorization, Google authentication, email confirmation, password recovery, and email-token two-factor authentication.
- Developing weight-based pricing, pricing tiers, checkout price revalidation, idempotency, concurrency handling, and controlled status transitions.
- Building the complete Admin Area, including administrator-account workflows.
- Hardening application security through ownership validation, anti-forgery protection, secure cookies, security headers, rate limiting, upload validation, and private file storage.
- Integrating PDF invoice generation, Redis, and health checks; creating the automated tests, GitHub Actions CI workflow, migration scripts, and repository documentation.

Bundled third-party libraries, fonts, and other upstream assets retain their original licenses and attribution.

## License

This repository does not currently include a repository-level open-source license. The application source is therefore not offered under an explicit open-source license. Bundled third-party libraries and fonts remain subject to the license files included with those assets.

## Contact

- GitHub: [Ahmed15109](https://github.com/Ahmed15109)
- LinkedIn: [Ahmed Abdelmonem](https://www.linkedin.com/in/ahmed-abdelmonem-2a43b824a)
