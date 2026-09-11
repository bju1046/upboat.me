# Execution Plan: Migrate UpboatMe to .NET 10 + ASP.NET Core + SkiaSharp

## Planning Model & Rationale

- **Selected planning model:** Claude Opus 4.8 (the model backing this GitHub Copilot session).
- **Rationale:** This migration is a cross-cutting, multi-project modernization (SDK-style conversion, full web-stack replacement, and a graphics engine rewrite with strict visual parity). It requires holding a large amount of interdependent context at once — legacy `System.Web` semantics, `System.Drawing` rendering internals, ASP.NET Core equivalents, and Linux/Docker runtime constraints — and reasoning about subtle behavioral parity (text-as-path stroke+fill, auto-fit font sizing, color-name mapping). A high-capability reasoning model is warranted over a faster/cheaper model because incorrect assumptions here (e.g., `System.Drawing.Common` being Windows-only on modern .NET, or missing `Impact`/private fonts on Linux) would silently break the acceptance bar. No web research was required; all decisions derive from the codebase inspection plus well-established .NET platform facts.

---

## Analysis Summary

UpboatMe is a meme-generation web app currently on **.NET Framework 4.8**, **ASP.NET MVC 4 + Web API 4**, **Razor (System.Web.WebPages) v2**, rendered with **System.Drawing (GDI+)**, packaged via legacy `packages.config`, and deployed on **Mono + xsp4** in Docker. It builds three projects plus a test project:

- `UpboatMe` — MVC + Web API web app (controllers, routing, Global.asax, HttpModule, bundling, views).
- `UpboatMe.Imaging` — GDI+ renderer (`Renderer.cs`, `RenderParameters.cs`) that draws text onto meme base images.
- `UpboatMeTests` — MSTest v1 (VS QualityTools) unit tests over meme name/alias resolution.
- `UpboatMe.Imaging` and the models depend on `System.Drawing` types (`Color`, `Rectangle`, `FontStyle`, `StringAlignment`) that flow through `Meme`/`LineConfig`/`RenderParameters`.

The requested target is **.NET 10 + ASP.NET Core + SkiaSharp**. This is a full rewrite of the hosting model and the imaging engine, but the domain logic (meme discovery, alias resolution, URL parsing, slogans) is largely portable.

**Nothing about the target stack already exists** in the repo — this is a greenfield migration of an existing app, not a partial-implementation gap. The plan below scopes the entire delta.

### Core behavior to preserve (acceptance-critical)
1. Root URL `GET /` returns **HTTP 200** with the Home/Index page (primary acceptance bar).
2. Catch-all meme rendering: `GET /{meme}/{line1}/{line2}...` returns a rendered image; extensionless URLs 302-redirect to the same URL + the meme's file extension.
3. Meme text rendering parity: Impact-style outlined text (stroke + fill), auto-shrink-to-fit, bottom-hugging bottom line, watermark image + text, per-meme custom fonts/bounds (batman, resharper, doge, csi), debug boxes.
4. Builder page, Meme List page (thumbnails), JSON list API (`/api/list`, `/api/list/{id}`).
5. Static pages: Terms, Privacy, Pricing, HowTo.

---

## Design & Approach

### Target solution shape (SDK-style, `net10.0`)
- **`UpboatMe.Imaging`** → `net10.0` class library, `PackageReference` to **SkiaSharp** and **SkiaSharp.NativeAssets.Linux** (for containerized Linux). Renderer rewritten against Skia. Introduce framework-neutral value types so models no longer depend on `System.Drawing`.
- **`UpboatMe`** → `Microsoft.NET.Sdk.Web`, `net10.0`, ASP.NET Core MVC (controllers + Razor views). Uses `Program.cs` minimal hosting (no `Startup`, no `Global.asax`, no `Web.config` runtime section).
- **`UpboatMeTests`** → `net10.0`, **MSTest** (modern `MSTest.TestAdapter` + `MSTest.TestFramework` + `Microsoft.NET.Test.Sdk`), or migrate to xUnit. Keep MSTest to minimize test rewrite (attributes are compatible).
- Keep the existing 3-project + test-project topology and namespaces to minimize churn and preserve unrelated user code.

### Key stack replacements
| Legacy (System.Web / .NET FX) | ASP.NET Core / .NET 10 replacement |
|---|---|
| `Global.asax` + `Application_Start` | `Program.cs` startup + hosted init |
| `System.Web.Mvc.Controller` | `Microsoft.AspNetCore.Mvc.Controller` |
| `System.Web.Http.ApiController` (`ListController`) | `[ApiController]` + `ControllerBase` returning `IActionResult`/typed results |
| `RouteConfig` + `WebApiConfig` | Endpoint routing in `Program.cs` + `[Route]`/conventional routes |
| `[OutputCache]` | Output Caching middleware (`AddOutputCache`/`CacheOutput`) + `[ResponseCache]` headers |
| `HttpModule` (`HideServerHeaderModule`, `PreSendRequestHeaders`) | Custom middleware that sets `Server` header |
| `System.Web.Optimization` bundles | Static files served from `wwwroot`; drop runtime bundling (CDN links already used) |
| `HttpContext.Server.MapPath` | `IWebHostEnvironment.WebRootPath`/`ContentRootPath` |
| `HttpContext.Current` (static) | Inject `IHttpContextAccessor` / pass context explicitly |
| `Request.ServerVariables["HTTP_URL"]` | Reconstruct absolute URL from `HttpRequest` |
| `Web.config` transforms + `appSettings` | `appsettings.json` + `appsettings.{Environment}.json` + env vars |
| `System.Drawing` (GDI+) rendering | **SkiaSharp** |
| `WorldWideWat.SpriteThumbs` (sprite thumbnails + `thumbs.axd` handler) | **Removed** — replaced with direct `<img>` thumbnails (see Gap G1) |
| `GoogleAnalyticsTracker` (net40) | Removed server-side; keep client-side GA snippet in layout (config-gated) |
| `NewRelic.Azure.WebSites` agent + content | Removed from build; optional re-add later via env-based agent |
| `Newtonsoft.Json` camelCase formatter | `System.Text.Json` (camelCase is default) |
| MSTest v1 (QualityTools) | MSTest SDK (`Microsoft.NET.Test.Sdk`) |

### Imaging: System.Drawing → SkiaSharp mapping
The renderer is the highest-risk parity work. Mapping:

| GDI+ concept | SkiaSharp equivalent / approach |
|---|---|
| `Image.FromFile` / `Graphics.FromImage` | Decode into `SKBitmap`/`SKImage`; draw on `SKCanvas` over an `SKBitmap` copy |
| `image.RawFormat` on save | Track source format from file extension; encode via `SKImage.Encode(SKEncodedImageFormat.Jpeg/Png, quality)` |
| `GraphicsPath.AddString` + `DrawPath(pen)` + `FillPath(brush)` (outlined text) | `SKFont.GetTextPath` / `SKTextBlob` → `SKPath`; draw path twice: `SKPaint{Style=Stroke, StrokeJoin=Round, StrokeWidth=w, Color=stroke}` then `SKPaint{Style=Fill, Color=fill}` |
| `Graphics.MeasureString` + auto-shrink loop | `SKFont.MeasureText` / measure path bounds; keep the same "shrink by 2 until fits height, min 10" loop |
| `StringFormat.Alignment` (Near/Center/Far) | Compute x-offset from measured width within bounds; `SKTextAlign` for simple cases |
| `StringAlignment.Far` + `HugBottom` | Compute y so text baseline sits at bottom of bounds/image |
| `Font(FontFamily, emSize, FontStyle)` | `SKTypeface.FromFamilyName(name, weight, width, slant)` or `SKTypeface.FromFile` (private fonts); `SKFont` with size derived from `DpiY * fontSize / 72` (Skia is device-pixel; use `fontSize` directly at 96dpi ≈ multiply by 96/72 to match) |
| `PrivateFontCollection.AddFontFile` | `SKTypeface.FromFile(ttfPath)` cached in an `SKFontManager`/dictionary keyed by family name |
| `Color.FromName("black")` etc. | Map .NET color-name strings → `SKColor` via a lookup table (see Gap G2) |
| `Color.FromArgb(150, c)` (watermark alpha) | `color.WithAlpha(150)` |
| `SystemFonts.DefaultFont` (debug boxes) | A bundled fallback typeface at fixed size |
| `CompositingMode.SourceOver/SourceCopy` | `SKBlendMode` on paint (default SrcOver); watermark/debug overlays use SrcOver |
| `SmoothingMode.HighQuality` | `SKPaint.IsAntialias = true` |

### Framework-neutral geometry/style types (decouple models from System.Drawing)
`Meme`, `LineConfig`, `RenderParameters`, `LineParameters`, and `MemeConfig` currently use `System.Drawing.{Color, Rectangle, FontStyle, StringAlignment}`. On modern .NET, `System.Drawing.Common` is **Windows-only and unsupported on Linux** — it cannot be used in the Linux/Docker target. Decision:
- Introduce small POCO types in `UpboatMe.Imaging` (or a shared `UpboatMe.Imaging.Primitives` namespace): `RgbaColor`, `RectangleI` (x,y,w,h), `enum TextAlign {Near,Center,Far}`, `enum FontSlant {Regular,Italic,Bold,BoldItalic}`.
- Replace all `System.Drawing` usages in models, `MemeConfig`, and the renderer with these types.
- Provide a `Colors` helper with named-color constants + `FromName(string)` to preserve `Color.FromName("HotPink")`, `Color.WhiteSmoke`, `Color.FromArgb(255,63,63,63)` semantics used in `MemeConfig`.
- **Rationale:** SkiaSharp does not expose GDI-compatible geometry types, and models must stay platform-neutral so the test project (which constructs `Meme`/`LineConfig`) compiles on Linux CI.

---

## Dependencies & Prerequisites

- **.NET SDK 10** installed locally and in the Docker build image (`mcr.microsoft.com/dotnet/sdk:10.0`), runtime `mcr.microsoft.com/dotnet/aspnet:10.0`.
- **SkiaSharp** NuGet: `SkiaSharp`, `SkiaSharp.NativeAssets.Linux.NoDependencies` (or `SkiaSharp.NativeAssets.Linux` + install `libfontconfig1`) for container rendering. macOS/Windows dev needs no extra native packages.
- **Fonts on Linux** (critical — see Gap G3): `Impact`, `Arial`, `Comic Sans MS`, `Segoe UI` are **not present** on a stock Linux container. Must bundle substitute TTFs into the app and register them via `SKTypeface.FromFile`, or install `ttf-mscorefonts-installer` (license-gated) / `fonts-liberation` + a metrically-compatible Impact clone (e.g., "Anton" or "Impacted"). The two ShyFoundry `SFActionMan*` TTFs already ship in `UpboatMe/Fonts/`.
- Base meme images (`UpboatMe/Images/*.jpg`) and `Content/UpBoatWatermark.png` must be reachable at runtime (content root, not necessarily `wwwroot`).
- No database, secrets, or external services are required for the acceptance bar. GA and NewRelic are optional and config-gated.

---

## Implementation Steps

> Ordering favors "compile the domain first, then the web host, then rendering, then Docker." Each step lists files and verification.

### Phase 0 — Baseline & safety
1. **Create a migration branch and snapshot build.**
   - Why: preserve current state; allow diffing visual output later.
   - Changes: none to source; record current behavior (screenshots of a few rendered memes if a running instance is available; otherwise note expected outputs).
   - Testing: n/a.

### Phase 1 — SDK-style project conversion (compile the domain)
2. **Convert `UpboatMe.Imaging.csproj` to SDK-style `net10.0`.**
   - Files: `UpboatMe.Imaging/UpboatMe.Imaging.csproj` (rewrite), delete `Properties/AssemblyInfo.cs` (or keep with `GenerateAssemblyInfo=false`).
   - Changes: `<Project Sdk="Microsoft.NET.Sdk">`, `<TargetFramework>net10.0</TargetFramework>`, add `PackageReference` SkiaSharp + `SkiaSharp.NativeAssets.Linux.NoDependencies`.
   - Testing: `dotnet build UpboatMe.Imaging` (will fail until Step 6 rewrites renderer — acceptable interim).
3. **Introduce framework-neutral primitives** (`RgbaColor`, `RectangleI`, `TextAlign`, `FontSlant`, `Colors.FromName`).
   - Files: new `UpboatMe.Imaging/Primitives/*.cs`.
   - Testing: builds in isolation.
4. **Convert `UpboatMeTests.csproj` to SDK-style `net10.0` MSTest.**
   - Files: `UpboatMeTests/UpboatMeTests.csproj` (rewrite) — `Microsoft.NET.Test.Sdk`, `MSTest.TestAdapter`, `MSTest.TestFramework`; `ProjectReference` to `UpboatMe`. Remove `System.Drawing` reference. Delete `app.config` usage if not needed.
   - Testing: compiles after models are ported (Step 5).
5. **Convert `UpboatMe.csproj` to `Microsoft.NET.Sdk.Web`, `net10.0`.**
   - Files: `UpboatMe/UpboatMe.csproj` (full rewrite). Remove all `<Reference>`/`packages.config`; add `PackageReference` for `SkiaSharp` (transitively via Imaging), plus `ProjectReference` to `UpboatMe.Imaging`. Delete `packages.config`, `Web.config`, `Web.Debug.config`, `Web.Release.config`, `Web.ProdDeploy.config`, `Global.asax`, `Global.asax.cs`. Move static assets (`Content/`, `Scripts/`, `favicon.ico`, `robots.txt`, `Fonts/`, social icons) into `wwwroot/` (see Step 12); keep `Images/` and `App_Data/` under content root (referenced by server code). Delete `newrelic/` content and NewRelic references. Remove `Foundation_readme.txt` from build items (leave file on disk).
   - Why: SDK-web projects use implicit globbing; explicit `<Compile>`/`<Content>` lists are removed.
   - Testing: `dotnet build` after web host is written (Steps 7–11).

### Phase 2 — Port models & utilities to neutral types
6. **Rewrite `UpboatMe.Imaging/Renderer.cs` + `RenderParameters.cs` against SkiaSharp** using the mapping table above.
   - Files: `UpboatMe.Imaging/Renderer.cs`, `RenderParameters.cs`.
   - Key parity points: outlined text (stroke path then fill path), auto-shrink loop (`while size.Height > bounds.Height && fontSize > 10: fontSize -= 2`), `HugBottom` far alignment, watermark image draw + alpha text, debug boxes, encode to source format (jpg quality ~90 / png).
   - Testing: unit-level render smoke test (render one meme to bytes; assert non-empty + valid JPEG/PNG magic bytes). Add a new test in `UpboatMeTests`.
7. **Port `Meme`, `LineConfig`, `MemeConfiguration`, `GlobalMemeConfiguration`, `RecentMeme`, `IndexViewModel`, `MemeDebugViewModel`** to neutral primitives.
   - Files: `UpboatMe/Models/*.cs`. Replace `System.Drawing` types with `RgbaColor`/`RectangleI`/`TextAlign`/`FontSlant`. `LineConfig` ctor keeps string color names via `Colors.FromName`.
   - Testing: `UpboatMeTests` (existing name/alias tests) compile and pass unchanged in logic.
8. **Port `App_Start/MemeConfig.cs`** (batman/resharper/doge/csi overrides) to neutral types; keep all bounds and colors identical.
   - Files: `UpboatMe/App_Start/MemeConfig.cs` (or relocate to `Configuration/MemeConfig.cs`). Replace `HttpRuntime.AppDomainAppPath` with injected content root; register private font file path (`Fonts/SFActionManExtended.ttf`) with the renderer's typeface cache.
   - Testing: covered by config + render smoke tests.
9. **Port string/URL/meme utilities** removing `System.Web`/`HttpContext.Current`.
   - Files: `Utilities/MemeUtilities.cs` (drop `HttpContext.Current.Request.ApplicationPath` — pass base path or assume `/`), `Utilities/SloganUtilities.cs` (read `App_Data/slogans.txt` via content root; cache), `Utilities/StringExtensionMethods.cs` (mostly portable), `Utilities/CollectionExtensions.cs`, `Utilities/ThreadRandom.cs`. `Models/MemeRequest.cs`: replace `HttpServerUtilityBase.UrlDecode` with `System.Net.WebUtility`/`Uri.UnescapeDataString`.
   - Testing: extend `UpboatMeTests` where practical.

### Phase 3 — ASP.NET Core web host
10. **Create `Program.cs`** (minimal hosting): `AddControllersWithViews()`, `AddOutputCache()`, `AddHttpContextAccessor()`, register singletons (`MemeConfiguration`, renderer, typeface cache, slogans), run meme auto-registration at startup (replaces `Application_Start`), map routes, add static files, add the Server-header middleware, `app.Run()`.
    - Files: new `UpboatMe/Program.cs`.
    - Testing: `dotnet run`; `curl -i http://localhost:5xxx/` → **200** (acceptance bar).
11. **Port controllers to ASP.NET Core.**
    - `Controllers/HomeController.cs`: `System.Web.Mvc` → `Microsoft.AspNetCore.Mvc`; `[OutputCache]`→`[OutputCache]` (ASP.NET Core) or `CacheOutput`; `HostingEnvironment.ApplicationVirtualPath` → base path from config (default `/`). Return `View(viewModel)`.
    - `Controllers/MemeController.cs`: replace `Request.ServerVariables["HTTP_URL"]` with reconstructed URL from `HttpRequest`; `HttpContext.Server.MapPath` → content-root path joins; `FileContentResult(bytes, mime)` → `File(bytes, mime)`; keep extension-redirect logic; `Analytics.TrackMeme` removed or replaced with no-op/logging.
    - `Controllers/ListController.cs`: `ApiController` → `[ApiController] [Route("api/list")] ControllerBase`; return `Ok(result)` with `[ResponseCache]`; camelCase via System.Text.Json default. Preserve `/api/list` and `/api/list/{id}`.
    - Files: the three controllers.
    - Testing: `curl` each route; assert 200/302/JSON shape.
12. **Static assets + `wwwroot`.**
    - Move `Content/`, `Scripts/`, `favicon.ico`, `robots.txt`, social icons into `wwwroot/`. Add `app.UseStaticFiles()`. Set long cache headers for `wwwroot/Content` (replaces `<clientCache>` in Web.config) via `StaticFileOptions`.
    - Replace `System.Web.Optimization` bundle references in views with direct `<link>`/`<script>` tags (CDN URLs already present; local fallbacks via static files). Remove `BundleConfig.cs`.
    - Files: `wwwroot/*`, `Program.cs`, views (Step 13), delete `App_Start/BundleConfig.cs`.
    - Testing: page loads CSS/JS; 200 on `/Content/styles.css`.

### Phase 4 — Razor views
13. **Migrate views to ASP.NET Core Razor.**
    - Files: `Views/_ViewStart.cshtml`, add `Views/_ViewImports.cshtml` (namespaces + `@addTagHelper`), `Views/Shared/_Foundation.cshtml` (rename usage stays), all `Home/*` and `Meme/*` and `Shared/*` partials. Delete `Views/Web.config`.
    - Replace: `@Styles.Render`/`@Scripts.Render` → static `<link>`/`<script>`; `@Html.Partial` → `<partial>`/`await Html.PartialAsync`; `Request.Url.Scheme`/`Request.QueryString` → `Context.Request`; `System.Configuration.ConfigurationManager.AppSettings` → injected `IConfiguration`; `Html.ActionLink`/`Url.Action` remain (Core equivalents).
    - Port custom helpers: `HtmlHelperExtensions.TopNavLink`, `LastUpdated`, and **`ThumbImage`** (SpriteThumbs) — see Gap G1; `UrlHelperExtensionMethods.AbsoluteAction`/`VersionedContent` (reimplement with `IUrlHelper` + `IWebHostEnvironment`). `BuilderViewModel.GetPreviewUrl(UrlHelper)` → Core `IUrlHelper`; drop `HttpContext.Current`.
    - Testing: `/`, `/Builder`, `/List`, `/Terms`, `/Privacy`, `/Pricing` all return 200 and render.

### Phase 5 — Routing parity
14. **Recreate the custom route table** in `Program.cs` endpoint routing:
    - Ignore `*.axd`/`robots.txt` (now static/absent).
    - `StaticPages`: `{action}` constrained to `Index|About|Terms|Privacy|Pricing` → Home.
    - `MemePages`: `{action}/{*url}` constrained to `Builder|Debug|List` → Meme.
    - `Debug/{top}/{bottom}` → Meme/Debug.
    - **Catch-all** `{*url}` → Meme/Make with the negative-lookahead constraint `^(?!bundles|content|scripts).*` (adjust excluded prefixes to `content|scripts|images|api|favicon.ico`). Use a regex route constraint.
    - `api/{controller}/{id?}` for the list API.
    - Files: `Program.cs`.
    - Testing: `GET /sk/hello/world` renders; `GET /sk/hello/world` (no ext) 302→`.jpg`; `GET /Builder` works; `GET /api/list` JSON.

### Phase 6 — Cross-cutting middleware & config
15. **Server-header middleware** replacing `HideServerHeaderModule` (set `Server: Memeverse/1.0`, strip `X-Powered-By`).
    - Files: new `Middleware/ServerHeaderMiddleware.cs` + registration.
16. **`appsettings.json`** with `EnableGoogleAnalytics` (default false), base path, cache durations; bind via `IConfiguration`.
    - Files: `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json`.
17. **Remove obsolete config/init**: `FilterConfig` (`HandleErrorAttribute`) → `app.UseExceptionHandler`/dev exception page; `SpriteThumbsConfig` deleted (Gap G1); `WebApiConfig`/`RouteConfig` folded into `Program.cs`.

### Phase 7 — Docker & local validation
18. **Rewrite `Dockerfile`** to multi-stage .NET 10:
    - `build` stage: `mcr.microsoft.com/dotnet/sdk:10.0`, `dotnet restore`/`publish -c Release`.
    - `runtime` stage: `mcr.microsoft.com/dotnet/aspnet:10.0`; install `libfontconfig1` (+ `fontconfig`) for SkiaSharp; copy bundled fonts; `EXPOSE 8080`; `ASPNETCORE_URLS=http://+:8080`; `ENTRYPOINT ["dotnet","UpboatMe.dll"]`. Write `App_Data/version.txt` as before.
    - Files: `Dockerfile`.
    - Testing: `docker build` + `docker run -p 8080:8080` → `curl -i http://localhost:8080/` returns **200**; render a meme URL and confirm image bytes.
19. **Update `UpboatMe.sln`** to drop `.nuget` solution folder and `NuGet.exe`/`NuGet.targets`; keep the three projects + tests (SDK GUIDs fine as-is or regenerate via `dotnet sln`).
    - Files: `UpboatMe.sln`.
20. **Final local validation matrix** (acceptance):
    - `dotnet build UpboatMe.sln` → success.
    - `dotnet test` → existing meme tests pass + new render smoke test passes.
    - `dotnet run` → `GET /` **200**; `/Builder`, `/List`, `/Terms`, `/Privacy`, `/Pricing` **200**; `/api/list` **200 JSON**; `/{meme}/{a}/{b}.jpg` returns image; extensionless meme URL **302**.

---

## Risk Assessment & Migration Gaps

- **G1 — `WorldWideWat.SpriteThumbs` has no .NET Core equivalent (blocker for List thumbnails).** The package generates a CSS sprite of all meme thumbnails at startup and serves it via a `thumbs.axd` `IHttpHandler`; `Html.ThumbImage` emits `<div>`s with sprite classes. *Mitigation:* remove the dependency and replace `ThumbImage` with a direct `<img>` that points at a small rendered/base image (e.g., `<img src="/{alias}/.jpg">` or a lightweight thumbnail endpoint that Skia-resizes the base image and caches it). Simplest parity-preserving option: emit `<img>` referencing the base image scaled via CSS. This changes page weight but preserves the List UX and unblocks the build.
- **G2 — Color-name fidelity.** `MemeConfig`/`LineConfig` use `Color.FromName("HotPink"|"ForestGreen"|"WhiteSmoke"|...)` and `Color.FromArgb`. *Mitigation:* build a `Colors.FromName` table covering the specific names used (enumerated from `MemeConfig.cs` + `LineConfig` defaults `black`/`white`); fail fast on unknown names during startup so regressions surface immediately.
- **G3 — Fonts on Linux (visual-parity risk).** `Impact` (default meme font), `Arial`, `Comic Sans MS`, `Segoe UI` are absent from stock Linux images; GDI+ silently substituted, Skia will too but differently. *Mitigation:* bundle metrically-compatible TTFs in the app and register via `SKTypeface.FromFile`; map requested family names to bundled files (e.g., Impact→Anton/Impacted, Arial→Liberation Sans, Comic Sans MS→Comic Neue, Segoe UI→Open Sans). Document that exact glyph rendering will differ from the Windows/GDI original. Private `SF Action Man Extended` already ships and ports directly.
- **G4 — Text rendering parity is approximate.** GDI+ `GraphicsPath.AddString` + `DpiY*fontSize/72` em-size vs. Skia path text metrics differ subtly (kerning, hinting, stroke join). *Mitigation:* replicate the stroke-then-fill path approach and the shrink-to-fit loop; visually compare a sample set (batman, doge, csi, resharper, a default 2-line meme) against pre-migration output; tune em-size scale factor (`96/72`) and stroke width to match.
- **G5 — `image.RawFormat` round-trip.** Skia doesn't preserve arbitrary source encodings. *Mitigation:* infer format from the meme file extension (all base images are `.jpg`; a few outputs are `.png`) and encode accordingly; keep `imageType` MIME derived from extension (existing `image/jpg` quirk noted in code — normalize to `image/jpeg`).
- **G6 — `HttpContext.Current` static access** in `SloganUtilities`, `MemeUtilities`, `BuilderViewModel`. *Mitigation:* inject content root / `IHttpContextAccessor` or refactor signatures to receive needed values; avoids hidden global state.
- **G7 — Output caching semantics differ.** ASP.NET Core output caching is opt-in and behaves differently from `[OutputCache]`. *Mitigation:* use `AddOutputCache` + `CacheOutput`/`[OutputCache]` policies and `[ResponseCache]` headers to approximate the 1-hour meme cache and 60-second page caches. Not acceptance-critical.
- **G8 — Bundling/minification removed.** `System.Web.Optimization` is gone. *Mitigation:* the app already loads Foundation/jQuery from CDNs; serve the few local JS/CSS files statically. Optional future: add a build-time bundler.
- **G9 — GoogleAnalyticsTracker & NewRelic packages are Framework-only.** *Mitigation:* drop server-side GA tracking (keep client-side snippet, config-gated); remove NewRelic agent/content from the build. Optional: re-add NewRelic via the modern env-based .NET agent later.
- **G10 — SkiaSharp native assets in container.** Missing `libfontconfig1`/`libfontconfig` causes runtime crash. *Mitigation:* use `SkiaSharp.NativeAssets.Linux.NoDependencies` or install fontconfig in the runtime image; validate in the Docker run step.
- **G11 — Request validation / URL characters.** Legacy config set `requestPathInvalidCharacters=""` and `relaxedUrlToFileSystemMapping` to allow meme text with slashes/special chars in the path. ASP.NET Core routing handles this differently. *Mitigation:* rely on the catch-all `{*url}` capturing the remainder; test memes with spaces, apostrophes, and `--` (em-dash logic in `SanitizeMemeText`).
- **G12 — MSTest v1 → SDK.** Attributes are compatible; `[TestClass]/[TestMethod]/[TestInitialize]` port directly. Low risk.

---

## Testing Strategy

- **Unit tests (port existing):** `MemeConfigAliasTests`, `MemeConfigNameTests`, `MemeUtilitiesTests` must pass unchanged in intent on `net10.0` MSTest.
- **New unit tests:** (a) `Renderer` smoke test — render a default 2-line meme and a private-font meme (batman) to bytes; assert non-empty and correct magic bytes (JPEG `FF D8`, PNG `89 50`). (b) `Colors.FromName` covers every name used in `MemeConfig`/`LineConfig`. (c) `MemeUtilities.GetMemeRequest`/`FromUrl` parsing for edge cases (spaces, apostrophes, extension stripping, `debugMode=true`).
- **Integration (manual `curl`/HTTP):** the validation matrix in Step 20 — root 200, static pages 200, API JSON, meme render, extensionless 302.
- **Visual parity spot-check:** compare rendered output for batman, doge, csi, resharper, and a stock meme against pre-migration images; adjust font mapping/stroke/em-size as needed (G3/G4).
- **Container test:** `docker build` + `docker run` + `curl -i http://localhost:8080/` → 200, plus one meme render.

---

## Rollout Notes

- **Acceptance bar:** migrated app **builds** (`dotnet build`), **runs locally** (`dotnet run`), and **serves root `GET /` as HTTP 200**. All other routes validated per Step 20.
- **Config:** `EnableGoogleAnalytics` defaults false; set via env var in prod. `ASPNETCORE_ENVIRONMENT` controls dev exception page vs. `UseExceptionHandler`.
- **Fonts:** document the font substitution decisions (G3) in the repo README so visual differences from the original are expected, not bugs.
- **Removed features to call out to stakeholders:** server-side GA tracking, NewRelic agent, runtime bundling/minification, and the SpriteThumbs CSS-sprite thumbnails (replaced with `<img>` thumbnails).
- **Observability:** add basic `ILogger` logging around meme resolution failures (the current 404-meme fallback) and render errors; optionally re-introduce NewRelic later via the modern agent.
- **Preserve unrelated user changes:** this plan only touches files required by the migration; do not discard in-progress or unfamiliar files. Perform the conversion on a dedicated branch and review the diff before merge.
