# ADR-001: Use Clean Architecture instead of Traditional Layered Architecture

| Field    | Value                        |
|----------|------------------------------|
| Status   | Accepted                     |
| Date     | 2026-03-13                   |
| Authors  | GarageSpace Team             |

---

## Context

GarageSpace.StorageService is a focused microservice responsible for file upload, multipart
upload orchestration, and S3 object management within the wider GarageSpace platform.

When starting the project we evaluated two widely-used structural patterns:

* **Traditional (N-tier) Layered Architecture** – Presentation → Business Logic → Data Access
* **Clean Architecture** (also known as Onion / Hexagonal Architecture) – domain at the centre,
  application layer around it, infrastructure and UI on the outside

Both patterns separate concerns into layers. The key difference lies in **which direction
dependencies flow**.

---

## Decision

We chose **Clean Architecture**.

The four projects map directly to the four concentric rings:

```
┌─────────────────────────────────────────────────────┐
│  API  (Presentation)                                │
│  ┌───────────────────────────────────────────────┐  │
│  │  Infrastructure  (Adapters / Frameworks)      │  │
│  │  ┌─────────────────────────────────────────┐  │  │
│  │  │  Application  (Use Cases)               │  │  │
│  │  │  ┌───────────────────────────────────┐  │  │  │
│  │  │  │  Domain  (Entities / Rules)       │  │  │  │
│  │  │  └───────────────────────────────────┘  │  │  │
│  │  └─────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

**Dependency rule:** every arrow points *inward only*.

| Project                      | Ring           | Depends on                              |
|------------------------------|----------------|-----------------------------------------|
| `*.Domain`                   | Core           | nothing (zero external dependencies)    |
| `*.Application`              | Use Cases      | `Domain` only                           |
| `*.Infrastructure`           | Adapters       | `Domain` + `Application` (implements interfaces defined there) |
| `*.API`                      | Entry point    | `Application` interfaces (+ DI wires up `Infrastructure`) |

---

## Why not Traditional Layered Architecture?

In a classic N-tier layered architecture dependencies flow **top-down**:

```
Presentation  →  Business Logic  →  Data Access
```

This means the Business Logic layer *knows about* and *depends on* the Data Access layer.
There is no way to run or test the business logic without dragging along the database driver,
the ORM, or (in our case) the AWS SDK.

### Concrete problems that would arise here

| Problem | Impact in this service |
|---|---|
| **Infrastructure bleeds into business logic** | `IUploadService` and domain entities would need to reference `IAmazonS3`, EF Core types, or Serilog directly. Every unit test would need a real (or mocked) S3 client. |
| **Coupling to a specific database / storage provider** | Changing from SQL Server to PostgreSQL, or from AWS S3 to Azure Blob Storage, would require touching the business-logic layer. |
| **Hard to test in isolation** | There is no seam to inject a fake repository or a fake S3 client without restructuring the code. |
| **Violates Dependency Inversion Principle** | High-level policy (use cases) depends on low-level implementation details (EF Core, AWSSDK.S3). |

### How Clean Architecture solves each problem

| Problem | Clean Architecture solution |
|---|---|
| **Infrastructure bleeds into business logic** | `IS3Service`, `IFileMetadataRepository`, `IUploadSessionRepository` are declared in `Application`/`Domain`. `Infrastructure` *implements* them. Business logic never references AWSSDK or EF Core. |
| **Coupling to a specific storage provider** | Swapping AWS S3 for Azure Blob Storage means writing a new `AzureBlobS3Service : IS3Service` in `Infrastructure`. Domain and Application are untouched. |
| **Hard to test in isolation** | Any test can create a mock/stub of `IS3Service` or `IFileMetadataRepository` without starting a real database or AWS session. |
| **Dependency Inversion Principle** | High-level Application layer defines the interfaces; low-level Infrastructure layer satisfies them. Arrows point inward. |

---

## Additional reasons specific to a microservice context

* **Single Responsibility at the service level** – this service only handles storage; it must be
  replaceable with minimal impact on other services. Clean Architecture's seams make that easier.
* **Testability is critical in CI/CD pipelines** – unit tests run without any cloud credentials or
  database connections.
* **Explicit boundaries** – in a microservices ecosystem, each service is effectively its own
  mini-application. Having a proper domain model inside each service discourages the temptation
  to share a giant database layer across services.

---

## Trade-offs accepted

Clean Architecture introduces more projects and more boilerplate (interfaces, DTOs, mapping)
compared to a simple three-layer app. For a CRUD-heavy, short-lived side project this overhead
might not be worth it. For a production storage service that will evolve independently and must
be reliably testable, the additional structure pays for itself quickly.

---

## Consequences

* All business rules live in `Domain` and `Application` — no framework dependencies there.
* `Infrastructure` is the only place that references AWSSDK.S3, EF Core, and Serilog.
* New storage backends or database providers can be added by implementing the existing interfaces
  without modifying application logic.
* Unit tests for service logic mock `IS3Service` / repository interfaces — no cloud credentials needed.
