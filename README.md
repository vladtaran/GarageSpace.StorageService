# GarageSpace.StorageService

Storage and file upload service for the GarageSpace microservices ecosystem.  
Handles direct uploads, presigned URL generation, and S3 multipart upload orchestration.

---

## Architecture

The service is structured using **Clean Architecture** (also called Onion / Hexagonal Architecture).
Dependencies always point *inward* — outer layers know about inner layers, but never the reverse.

```
┌──────────────────────────────────────────────────────────┐
│  API             (entry point, controllers, DI wiring)   │
│  ┌────────────────────────────────────────────────────┐  │
│  │  Infrastructure  (EF Core, AWSSDK.S3, Serilog)    │  │
│  │  ┌──────────────────────────────────────────────┐  │  │
│  │  │  Application  (use-case services, DTOs)      │  │  │
│  │  │  ┌──────────────────────────────────────┐    │  │  │
│  │  │  │  Domain  (entities, enums, repo interfaces) │  │  │
│  │  │  └──────────────────────────────────────┘    │  │  │
│  │  └──────────────────────────────────────────────┘  │  │
│  └────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────┘
```

| Project | Ring | Responsibility |
|---|---|---|
| `*.Domain` | Core | Entities, enums, repository interfaces — zero external dependencies |
| `*.Application` | Use Cases | Service interfaces (`IS3Service`, `IUploadService`), DTOs — depends on `Domain` only |
| `*.Infrastructure` | Adapters | Implements service/repository interfaces using EF Core and AWSSDK.S3 |
| `*.API` | Entry point | ASP.NET Core controllers; wires concrete implementations via DI |

### Why Clean Architecture and not traditional Layered (N-tier)?

In a classic N-tier architecture the Business Logic layer depends on the Data Access layer,
which means framework details (ORM, cloud SDK) leak into business logic — making it harder to
test and harder to swap providers.

Clean Architecture **inverts** that dependency: the `Application` layer declares *interfaces*
(`IS3Service`, `IFileMetadataRepository`) and `Infrastructure` *implements* them.  
As a result:

* **Business logic is framework-free** — `Domain` and `Application` have no references to EF Core,
  AWSSDK.S3, or any HTTP library.
* **Easy to unit-test** — mock `IS3Service` or `IFileMetadataRepository` without a real database
  or AWS credentials.
* **Provider-swappable** — replacing AWS S3 with Azure Blob Storage only requires a new
  `Infrastructure` implementation; no business-logic code changes.

For the full reasoning see [docs/adr/ADR-001-clean-architecture.md](docs/adr/ADR-001-clean-architecture.md).

---

## Getting started

```bash
# restore & build
dotnet restore
dotnet build

# run the API (configure appsettings.json or environment variables first)
dotnet run --project src/GarageSpace.StorageService.API
```

### Required configuration (`appsettings.json` / environment variables)

```json
{
  "S3": {
    "BucketName": "<your-bucket>",
    "Region": "eu-central-1",
    "AccessKey": "",   // leave empty to use IAM role / env-var credential chain
    "SecretKey": "",
    "ServiceUrl": ""   // optional: set to http://localhost:4566 for LocalStack
  },
  "ConnectionStrings": {
    "Default": "<SQL Server connection string>"
  }
}
```
