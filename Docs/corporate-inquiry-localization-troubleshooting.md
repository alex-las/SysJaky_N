# Corporate inquiry localization troubleshooting

The new corporate inquiry wizard reads most strings from the strongly-typed `CorporateInquiryResources` helper and from the Razor page resource file (`Pages.CorporateInquiry.cshtml.*.resx`). If the UI still renders raw resource keys such as `ServiceType_CustomTraining` instead of localized text, check the scenarios below.

## 1. Resource files were not rebuilt or deployed

`SysJaky_N.csproj` disables the default wildcard embedding of `.resx` files, so the project relies on the explicit `<EmbeddedResource Include="Resources/**/*.resx" />` item to ship localization data. When publishing, make sure the build output contains the updated `SysJaky_N.resources.dll` satellite assemblies. A stale deployment that still includes the removed per-partial resource files will continue to return only the key names. Run `dotnet clean` followed by `dotnet publish` on the deployment machine to force a rebuild that includes the consolidated resources.【F:SysJaky_N.csproj†L6-L36】

## 2. Running workers without the updated Razor views

The partial views now pull display strings from `CorporateInquiryResources`. If the web worker is still serving old precompiled views (for example, because the container image was not rebuilt or the application pool was not restarted), the rendered HTML will reference the old localizer keys. Verify that `/Pages/CorporateInquiry/Partials/_StepServiceType.cshtml` and `_StepTraining.cshtml` on the server match the current repository version that calls `CorporateInquiryResources.*` helpers.【F:Pages/CorporateInquiry/Partials/_StepServiceType.cshtml†L1-L19】【F:Pages/CorporateInquiry/Partials/_StepTraining.cshtml†L1-L41】

## 3. Request culture not matching the available resources

Localization relies on the request culture resolved by ASP.NET Core. The application currently supports `cs` and `en` (`Program.cs` configures both as supported cultures and sets Czech as default). If the browser explicitly requests a different culture (e.g., `sk`), the framework will fall back to the key name because no resource exists for that culture. Use the `?culture=cs` or `?culture=en` query parameter (or clear the `.AspNetCore.Culture` cookie) to confirm the culture matches a provided resource set.【F:Program.cs†L159-L194】

## 4. Missing keys in the Razor page resource file

JavaScript labels in `Pages/CorporateInquiry.cshtml` still come from the page-local `IViewLocalizer`. Ensure the consolidated `Pages.CorporateInquiry.cshtml.resx` (and `.en.resx`) files contain every `ServiceType_*`, `ISO*`, and `TrainingLevel_*` entry; otherwise, any missing key will surface as the untranslated token in the front-end wizard.【F:Pages/CorporateInquiry.cshtml†L10-L58】【F:Resources/Pages.CorporateInquiry.cshtml.resx†L1-L40】【F:Resources/Pages.CorporateInquiry.cshtml.en.resx†L1-L120】

## 5. Browser caching of the wizard script bundle

The wizard loads localized labels from `data-*` attributes that are rendered server-side. If the browser keeps an outdated `corporateInquiry.js` bundle from before the localization change, cached logic may still expect the old format (e.g., calling `textContent` on `label.dataset.resourceKey`). Perform a hard refresh (Ctrl+F5) or clear the site cache to guarantee the latest script that reads the pre-localized strings is executed.【F:Pages/CorporateInquiry.cshtml†L59-L200】

By walking through these checks you can isolate whether the issue stems from deployment, configuration, or client-side caching rather than from the resource definitions themselves.
