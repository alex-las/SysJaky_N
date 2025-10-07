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

3. Run `dotnet build`/`dotnet run` as usual. MSBuild automatically executes the `BundleStaticAssets` target (hooked before the static-web-asset manifest is generated and during publish) unless you disable it with `-p:NpmSkipStaticAssets=true` – handy for CI agents that already provide pre-built bundles.

## CI / publish pipelines

* Ensure Node.js is installed before invoking `dotnet publish` (the build restores npm dependencies on demand via the `EnsureNodeModules` target, caching installs with `node_modules/.install-stamp`).
* No extra steps are needed – the MSBuild target executes `npm run build:assets --silent` automatically before static web asset discovery/publish.
* The generated files live under `wwwroot/dist` and are deployed like any other static asset. Nothing tries to mutate the web root at runtime.

## Read-only hosting environments

Because bundling happens during build, the application can boot with a read-only `wwwroot`. To smoke test this locally:

```bash
npm run build:assets
chmod -R a-w wwwroot
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build
```

After the test remember to restore permissions (`chmod -R u+w wwwroot`).
