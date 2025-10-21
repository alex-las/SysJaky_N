# Pohoda SQL Secrets Configuration

The application expects the **PohodaSql** configuration section to be populated with the
connection details for the Pohoda database. The section is defined in
`appsettings.json`, but the actual credentials must be stored outside of source
control.

## Required settings

At minimum, the following keys need to be provided:

- `PohodaSql:ConnectionString`
- `PohodaSql:Server`
- `PohodaSql:Database`
- `PohodaSql:Username`
- `PohodaSql:Password`

The application can use either the raw connection string or the individual
properties, depending on the client implementation.

## Using .NET user secrets (development)

When running locally, store the sensitive values with the
[Secret Manager tool](https://learn.microsoft.com/aspnet/core/security/app-secrets).
Run the following commands from the project directory:

```bash
dotnet user-secrets set "PohodaSql:ConnectionString" "Server=YOUR_SERVER;Database=YOUR_DB;User=YOUR_USER;Password=YOUR_PASSWORD;"
dotnet user-secrets set "PohodaSql:Server" "YOUR_SERVER"
dotnet user-secrets set "PohodaSql:Database" "YOUR_DB"
dotnet user-secrets set "PohodaSql:Username" "YOUR_USER"
dotnet user-secrets set "PohodaSql:Password" "YOUR_PASSWORD"
```

Replace the placeholder values with the real credentials used in your
environment. Once configured, the application will automatically bind the
settings to the `PohodaSqlOptions` class.

## Deployment

For production deployments, configure the same keys via environment variables,
Key Vault, or your preferred secrets provider. Ensure the deployment environment
is able to supply the configuration section under `PohodaSql` before starting
the application.
