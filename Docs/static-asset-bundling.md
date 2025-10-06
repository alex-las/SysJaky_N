# Static asset bundling

The application now ships production CSS/JS bundles from `wwwroot/dist`. Bundles are built at compile/publish time – no runtime concatenation is attempted anymore, so the web root can stay read-only during app startup.

## Local development

1. Install Node.js 20+.
2. Restore npm packages and build bundles:

   ```bash
   npm install
   npm run build:assets
   ```

   This produces `wwwroot/dist/styles.min.css` and `wwwroot/dist/scripts.min.js` from Bootstrap, jQuery and the local `site.css`/`site.js` sources via [esbuild](https://esbuild.github.io/).

3. Run `dotnet build`/`dotnet run` as usual. MSBuild automatically executes the `BuildStaticAssets` target unless you disable it with `-p:NpmSkipStaticAssets=true` (useful for CI agents without Node).

## CI / publish pipelines

* Ensure Node.js is installed before invoking `dotnet publish`.
* No extra steps are needed – the MSBuild target restores npm packages (`npm install --no-audit --no-fund --silent`) and executes `npm run build:assets --silent` before the managed build.
* The generated files live under `wwwroot/dist` and are deployed like any other static asset. Nothing tries to mutate the web root at runtime.

## Read-only hosting environments

Because bundling happens during build, the application can boot with a read-only `wwwroot`. To smoke test this locally:

```bash
npm run build:assets
chmod -R a-w wwwroot
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build
```

After the test remember to restore permissions (`chmod -R u+w wwwroot`).
