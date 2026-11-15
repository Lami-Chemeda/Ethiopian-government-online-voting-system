# One-time database setup on a new PC

Cloning this repository does not copy the SQL Server database. This application
uses the local SQL Server instance configured in `appsettings.json`:

```text
Server=localhost;Database=VotingDB;User Id=sa;Password=VotingSystem@2024!
```

Run these commands from a normal terminal. They need your Linux password only
to start SQL Server; they do not remove or overwrite any existing data.

```bash
sudo systemctl enable --now mssql-server
sudo systemctl status mssql-server --no-pager
cd "/home/lami/all code/EGOVS/EGOVS/EGOVS/VotingSystem"
/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'VotingSystem@2024!' -C -i DatabaseSetup.sql
```

The last command must be run only once for a fresh PC. It creates `VotingDB`
and all registration tables. Do not run it again if `VotingDB` already exists.

Verify the database before starting the web application:

```bash
/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'VotingSystem@2024!' -C -d VotingDB -Q "SELECT name FROM sys.tables WHERE name IN ('Voters','Admins','Managers','Supervisors','Candidates');"
dotnet run
```

If SQL Server reports `Login failed for user 'sa'`, its SA password differs
from the project configuration. Update the password in both `appsettings.json`
and `appsettings.Development.json` to the SA password configured on this PC,
then repeat the verification command with that same password.
