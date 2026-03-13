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
2. Open each file and run it (**F5** or the **Execute** toolbar button):
   ```
   01_create_database.sql
   02_create_user.sql
   ```

### Option B – Azure Data Studio

Azure Data Studio is a lightweight, cross-platform alternative to SSMS.

1. Connect to `localhost` using the **New Connection** panel.
2. **File → Open File** and select each script in order.
3. Click **Run** (or press **F5**).

### Option C – VS Code with the mssql extension

1. Install the [SQL Server (mssql)](https://marketplace.visualstudio.com/items?itemName=ms-mssql.mssql) extension.
2. Open a script file, then click **Run Query** in the top-right toolbar (or **Ctrl+Shift+E**).
3. When prompted, select or create a connection profile pointing to `localhost`.

### Option D – `sqlcmd` (command line, Windows / Linux / macOS)

Run from the repository root:

```bash
sqlcmd -S localhost -U sa -P "<sa_password>" -i db/scripts/01_create_database.sql
sqlcmd -S localhost -U sa -P "<sa_password>" -i db/scripts/02_create_user.sql
```

`sqlcmd` is available standalone via the
[mssql-tools](https://learn.microsoft.com/sql/linux/sql-server-linux-setup-tools) package or
is bundled with SQL Server on Windows.

### Option E – PowerShell (`Invoke-Sqlcmd`)

`Invoke-Sqlcmd` is part of the **SqlServer** PowerShell module (works on Windows, macOS, and Linux).

```powershell
# Install the module once (if not already installed)
Install-Module SqlServer -Scope CurrentUser

Invoke-Sqlcmd -ServerInstance localhost -Username sa -Password "<sa_password>" `
    -InputFile "db/scripts/01_create_database.sql"

Invoke-Sqlcmd -ServerInstance localhost -Username sa -Password "<sa_password>" `
    -InputFile "db/scripts/02_create_user.sql"
```

### Option F – Docker exec (SQL Server running in a container)

If your local SQL Server runs in Docker, copy the scripts into the container and use the
bundled `sqlcmd`:

```bash
docker cp db/scripts/01_create_database.sql <container_name>:/tmp/
docker cp db/scripts/02_create_user.sql      <container_name>:/tmp/

docker exec -it <container_name> \
    /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "<sa_password>" \
    -i /tmp/01_create_database.sql

docker exec -it <container_name> \
    /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "<sa_password>" \
    -i /tmp/02_create_user.sql
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
