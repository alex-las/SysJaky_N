# Pohoda XML Configuration

The application communicates with the Pohoda mServer over the XML interface. Configuration for
this integration is stored in the **PohodaXml** section of the application settings
(`appsettings.json`, environment variables, user secrets, etc.).

## Required settings

The `Enabled` flag controls whether the application talks to the Pohoda mServer. It defaults to
`true`. When set to `false`, the integration is disabled and export files are generated locally (see
[Offline exports](#offline-exports)).

When the integration is enabled the following keys must be provided:

- `BaseUrl` – Base address of the Pohoda mServer (e.g. `https://pohoda.example.com`).
- `Username` and `Password` – Credentials for the Pohoda XML API.
- `Application` – Identifier of the calling application used in the XML data pack header.
- `Instance` – Optional instance name; leave empty if not required by the server.
- `Company` – Optional company identifier forwarded to Pohoda in the HTTP headers.
- `CheckDuplicity` – Whether the server should prevent duplicate imports (`true`/`false`).
- `EncodingName` – Text encoding used for requests (default `windows-1250`).
- `TimeoutSeconds` – HTTP timeout configured for the Pohoda XML client.
- `RetryCount` – Number of times the HTTP call is retried by the client factory.

In addition to the connection settings, the section also controls the behaviour of the
background export worker:

- `ExportWorkerInterval` – How often the export worker wakes up.
- `ExportWorkerBatchSize` – Maximum number of jobs processed in a single run.
- `MaxRetryAttempts` – Maximum number of retries per job.
- `RetryBaseDelay` / `RetryMaxDelay` – Exponential backoff configuration for retries.
- `ExportDirectory` – Target directory for locally generated exports when the integration is disabled
  (defaults to `/temp`, which resolves to `temp/` under the application root).

## Local development

When running locally you can configure the settings via
[.NET user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets).

```bash
dotnet user-secrets set "PohodaXml:BaseUrl" "https://localhost:8443"
dotnet user-secrets set "PohodaXml:Username" "your-username"
dotnet user-secrets set "PohodaXml:Password" "your-password"
```

Provide the remaining keys as needed. The worker configuration is optional – defaults are applied
when the values are omitted or invalid.

## Offline exports

When `PohodaXml:Enabled` is set to `false` no HTTP calls are made to the Pohoda mServer. Instead,
the background worker saves the XML data packs for each processed order to the directory specified
by `PohodaXml:ExportDirectory`. Relative paths (including values that start with `/` or `\`) are
resolved against the application content root, so the default value `/temp` produces files in
`<app-root>/temp`. This allows testing the export pipeline without
valid credentials. The files use the Windows-1250 encoding and are named using the pattern
`pohoda-order-<orderId>-<timestamp>.xml`.

The HTTP-related configuration (`EncodingName`, `TimeoutSeconds`, `RetryCount`) is validated on
application start. When an invalid value is provided the application fails fast, allowing you to fix
the configuration before any background jobs run.

## Deployment

For production deployments configure the same keys via environment variables, Azure Key Vault or
another secrets provider. Make sure the `PohodaXml` section is available before the application
starts so that the `PohodaXmlOptions` binding succeeds.
