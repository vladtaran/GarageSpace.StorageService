# Database Setup Scripts

This directory contains idempotent SQL scripts that create and configure the local
`GarageSpaceStorage` SQL Server database used by the API in development.

## Prerequisites

- SQL Server 2019+ or SQL Server Express running locally on port **1433**
- A login with `sysadmin` or `dbcreator` + `securityadmin` privileges to run the scripts
  (e.g., the `sa` account, or a Windows login with sysadmin rights)

---

## Running the scripts

Execute the scripts **in order** against your local SQL Server instance.

### Option A – SQL Server Management Studio (SSMS)

1. Open SSMS and connect to `localhost` (or `localhost,1433`).
2. Open each file and run it (F5):
   ```
   01_create_database.sql
   02_create_user.sql
   ```

### Option B – `sqlcmd`

```bash
sqlcmd -S localhost -U sa -P "<sa_password>" -i db/scripts/01_create_database.sql
sqlcmd -S localhost -U sa -P "<sa_password>" -i db/scripts/02_create_user.sql
```

---

## Setting the application user password

`02_create_user.sql` contains a placeholder password:

```sql
WITH PASSWORD = N'<YourStrongPassword!1>'
```

**Replace `<YourStrongPassword!1>` with your own strong password** before running the script.

### Why not hard-code the password in appsettings?

Committing credentials to source control is a security risk.  
ASP.NET Core **User Secrets** keeps the password on your local machine only  
(`~/.microsoft/usersecrets/<id>/secrets.json`) and is never committed to the repository.

### Configuring User Secrets for the API project

After choosing your password, run the following command **once** from the repository root:

```bash
dotnet user-secrets set \
  "ConnectionStrings:DefaultConnection" \
  "Server=localhost;Database=GarageSpaceStorage;User Id=GarageSpaceStorageUser;Password=<YourStrongPassword!1>;TrustServerCertificate=True;" \
  --project src/GarageSpace.StorageService.API
```

This secret overrides the `ConnectionStrings:DefaultConnection` key from
`appsettings.Development.json` at runtime and is **never committed to git**.

---

## Roles granted to `GarageSpaceStorageUser`

| Role | Purpose |
|---|---|
| `db_datareader` | SELECT on all tables |
| `db_datawriter` | INSERT / UPDATE / DELETE on all tables |
| `db_ddladmin`   | CREATE / ALTER / DROP tables & indexes (required for EF Core migrations) |
