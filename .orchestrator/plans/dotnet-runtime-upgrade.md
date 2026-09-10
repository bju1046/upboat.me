# Execution Plan: .NET/Mono Runtime Upgrade for upboat.me

**Planning model:** Claude Sonnet 5 — selected because this task requires synthesizing evidence from a legacy multi-project .NET Framework codebase (MSBuild project files, `Web.config`, GDI+ imaging code, third-party binary dependencies) with current external ecosystem facts (ASP.NET Core compatibility, Mono end-of-life status, Docker Hub image freshness) to reach a defensible build-vs-fallback decision, then translate that decision into concrete, file-level Docker/MSBuild edits. This requires deep code comprehension plus multi-source research reconciliation, not just pattern-matching a known template.

## Analysis Summary

`upboat.me` is an **ASP.NET MVC 4 / Web API 1** application on **.NET Framework 4.5**, using old-style (non-SDK) `.csproj` files, `packages.config`, `Global.asax`, and classic `System.Web` pipeline extensibility (`IHttpModule`, `IHttpHandler` registrations in [Web.config](../../UpboatMe/Web.config)). It currently runs cross-platform only via **Mono 6.12 + `xsp4`** in Docker (see [Dockerfile](../../Dockerfile)), with prior portability fixes already applied to [Global.asax.cs](../../UpboatMe/Global.asax.cs) and [MemeConfig.cs](../../UpboatMe/App_Start/MemeConfig.cs) (uncommitted local changes — **not to be discarded**).

**Conclusion: migrating to modern .NET (Core-based .NET 8/9 + ASP.NET Core) is not feasible within the scope of this task.** The fallback path specified by the user — upgrade to the latest supported Mono runtime, including the Docker container — is the correct outcome. Evidence:

| Blocker | Detail | Why it blocks modern .NET |
|---|---|---|
| `System.Web.Mvc` 4 / `System.Web.Http` 1 / Razor 2 / `System.Web.Optimization` + WebGrease bundling | Referenced throughout [UpboatMe.csproj](../../UpboatMe/UpboatMe.csproj), [Global.asax.cs](../../UpboatMe/Global.asax.cs), `App_Start/*` | None of these assemblies exist for .NET 5+. ASP.NET Core has no `System.Web` compatibility shim on Linux/macOS (the Windows-only IIS in-process System.Web adapters do not apply here). Every controller, filter, route table, and bundle registration would need a rewrite against ASP.NET Core MVC. |
| Custom `IHttpModule` (`HideServerHeaderModule`) and `IHttpHandler` (`WorldWideWat.SpriteThumbs.ThumbsHttpHandler`) wired via `<system.webServer><modules>/<handlers>` in [Web.config](../../UpboatMe/Web.config) | Used live in [Views/Shared/_Foundation.cshtml](../../UpboatMe/Views/Shared/_Foundation.cshtml) and [Utilities/HtmlHelperExtensions.cs](../../UpboatMe/Utilities/HtmlHelperExtensions.cs) | `IHttpModule`/`IHttpHandler` do not exist in ASP.NET Core; these would need to become custom middleware, and the sprite-thumbnail feature has no maintained ASP.NET Core equivalent (`WorldWideWat.SpriteThumbs` was last published in 2013 and has no `netstandard`/`net*` target). |
| GDI+ image rendering in [UpboatMe.Imaging/Renderer.cs](../../UpboatMe.Imaging/Renderer.cs) via `System.Drawing` | Core product feature (meme text/watermark rendering) | `System.Drawing.Common` is Windows-only starting with .NET 7 (and was already restricted/opt-in on non-Windows in .NET 6). On Linux/macOS under modern .NET this throws `PlatformNotSupportedException`. Fixing this requires **rewriting the entire imaging engine** against a cross-platform library such as `SixLabors.ImageSharp` or `SkiaSharp` — a significant, high-risk rewrite of font metrics, string measurement, and path-based stroke/fill text rendering, not a "runtime upgrade." |
| Abandoned net45-only NuGet packages: `Foundation4.MVC4`, `Foundation4.Core`, `WorldWideWat.SpriteThumbs`, `GoogleAnalyticsTracker`, `NewRelic.Azure.WebSites` (Windows/IIS agent) | [packages.config](../../UpboatMe/packages.config) | None publish `netstandard2.0`/`net6.0`+ packages; several (SpriteThumbs, GoogleAnalyticsTracker) are unmaintained since ~2013-2015 with no successor package. |
| Old-style `.csproj` + `packages.config` across all 3 projects ([UpboatMe.csproj](../../UpboatMe/UpboatMe.csproj), [UpboatMe.Imaging.csproj](../../UpboatMe.Imaging/UpboatMe.Imaging.csproj), [UpboatMeTests.csproj](../../UpboatMeTests/UpboatMeTests.csproj)) | | Would need conversion to SDK-style projects with `PackageReference`, plus the test project (MSTest via `Microsoft.VisualStudio.QualityTools.UnitTestFramework`, a Visual Studio-only legacy test adapter) rewritten against `MSTest.TestFramework`/`xunit`. |

A genuine ASP.NET Core migration here means: rewriting routing/controllers/filters, replacing bundling with a modern static-asset pipeline, replacing two IIS-pipeline extensions with middleware, replacing the imaging engine's graphics library, dropping/replacing 4 third-party packages, converting 3 projects to SDK-style, and rewriting the test project — effectively a full rewrite, not an upgrade. This is explicitly out of scope for the current task and is documented here as a **future initiative**, not executed now.

### Decision

Proceed with the user's stated fallback: **upgrade to the latest available Mono runtime and refresh the Docker container**, and — separately, as the closest safe analogue to "latest .NET version" for a Framework app that must keep running on Mono — **attempt to raise `TargetFrameworkVersion` from v4.5 to the latest Mono-supported .NET Framework version (v4.8)** across all three projects, with a documented, tested fallback to a lower version if Mono's bundled reference assemblies can't build it.

Mono itself is now end-of-life: Docker Hub marks the `mono` image **"DEPRECATED"**, and the Mono Project's official page states stewardship has moved to WineHQ and that Microsoft/Mono recommend migrating to modern .NET. The last published stable release is **6.12.0.206** (mono-project.com), while the newest Docker Hub-published image is **`mono:6.12.0.182`** (pushed ~2 years ago) — Docker Hub has not published the newer 206 patch. The current Dockerfile floats on the `mono:6.12` tag, which today resolves to `6.12.0.182`.

## Design & Approach

- Treat the Mono/Docker upgrade as the primary, required deliverable (it directly satisfies the user's fallback instruction and the "get a 200 from root" acceptance bar).
- Prefer the lowest-risk path to the newest Mono binary:
  1. **Primary:** Keep the proven-working Docker Hub `mono` base image (avoids swapping base OS/toolchain, which risks breaking `libgdiplus`/`mono-xsp4` compatibility that isn't guaranteed on a bare Debian image), but pin it explicitly to the newest tag Docker Hub actually publishes (`mono:6.12.0.182`) instead of the floating `mono:6.12` tag, for reproducibility. Then layer the official Mono APT repository on top to attempt an in-place package upgrade to the true latest release, **6.12.0.206**.
  2. **Fallback (if the APT-repo upgrade step fails, e.g., due to keyserver network restrictions in the build environment):** Skip the APT upgrade step and ship pinned `mono:6.12.0.182` alone — still strictly newer/more reproducible than the current floating `6.12` tag, and it is the newest image Docker Hub has ever published.
- Replace `xbuild` with `msbuild` in the build step. `xbuild` was deprecated years ago in favor of MSBuild-on-Mono; `msbuild` is the actively supported build driver and is present in the same Mono packages, so this is a safe, low-risk swap that removes a deprecated tool from the critical path.
- Attempt to bump `TargetFrameworkVersion` from `v4.5` → `v4.8` (the newest .NET Framework release) in all three `.csproj` files and `Web.config`/`packages.config`, since Mono 6.12 ships reference assemblies through 4.8. Build inside the container; if MSBuild fails due to missing reference assemblies, step down (4.7.2 → 4.6.1 → revert to 4.5) until the build succeeds, and record the final chosen version and reason.
- Do not touch NuGet package versions (MVC/Web API/Razor/WebGrease/etc.) in this pass — bumping those (e.g., MVC4→MVC5) is a legitimate follow-up but carries real behavioral risk (attribute routing, bundling config changes) that is unnecessary to satisfy the current request and would expand blast radius beyond a "runtime upgrade."
- Preserve all existing uncommitted local changes (Dockerfile, `.dockerignore`, README.md, `Global.asax.cs`, `MemeConfig.cs`) — this plan's edits are additive/refining on top of them, not a revert.
- Validate success empirically: build the image, run the container, and curl `http://localhost:8080/` expecting HTTP 200.

## Dependencies & Prerequisites

- Docker available locally (already assumed, since the project's supported local workflow is Docker per [README.md](../../README.md)).
- Network access from the Docker build context to `download.mono-project.com` and `keyserver.ubuntu.com:80` for the APT-repo upgrade step. If the sandboxed/CI build environment blocks outbound port 80/keyserver traffic, this step will fail fast and the plan falls back to the pinned-image-only path (see above).
- No database or external service dependencies (app is stateless, file-based meme config).

## Implementation Steps

1. **Pin and refresh the Mono base image; upgrade Mono in-container; switch `xbuild` → `msbuild`.**
   File: [Dockerfile](../../Dockerfile)
   - Why: Satisfies "update to the latest Mono version including the Docker container"; removes reliance on a floating tag; removes a deprecated build tool.
   - Changes:
     - `FROM mono:6.12` → `FROM mono:6.12.0.182` (pin to the newest Docker Hub-published tag).
     - After the existing `apt-get` step (which fixes the archived Debian Buster sources), add an APT-repo step that imports the Mono Project's signing key and adds `deb [signed-by=...] https://download.mono-project.com/repo/debian stable-buster main`, then `apt-get update && apt-get install --only-upgrade --no-install-recommends --yes mono-runtime mono-devel mono-xsp4` to pull the actual latest published Mono release (6.12.0.206) on top of the pinned image. Wrap this in a way that a failure is clearly attributable to this optional step (separate `RUN` layer) so it can be dropped without touching the rest of the file if it can't reach the network.
     - Change the build command from `xbuild UpboatMe/UpboatMe.csproj ...` to `msbuild UpboatMe/UpboatMe.csproj ...` (same arguments).
     - Keep `EXPOSE 8080` / `CMD ["xsp4", ...]` unchanged.
   - Testing: `docker build -t upboat-me .` completes without error; image layer for the Mono upgrade step shows the new version (`mono --version` inside container should report 6.12.0.206, or 6.12.0.182 if the upgrade step was skipped).

2. **Attempt to raise the target .NET Framework version to the latest Mono-supported release (v4.8).**
   Files: [UpboatMe/UpboatMe.csproj](../../UpboatMe/UpboatMe.csproj), [UpboatMe.Imaging/UpboatMe.Imaging.csproj](../../UpboatMe.Imaging/UpboatMe.Imaging.csproj), [UpboatMeTests/UpboatMeTests.csproj](../../UpboatMeTests/UpboatMeTests.csproj), [UpboatMe/Web.config](../../UpboatMe/Web.config), [UpboatMe/packages.config](../../UpboatMe/packages.config)
   - Why: Closest safe analogue to "update to the latest .NET version" given a full ASP.NET Core migration is infeasible (see Analysis Summary); .NET Framework 4.8 is the final/latest release of the classic .NET Framework line.
   - Changes: `<TargetFrameworkVersion>v4.5</TargetFrameworkVersion>` → `v4.8` in all 3 `.csproj` files; `httpRuntime targetFramework="4.5"` and `compilation targetFramework="4.5"` → `"4.8"` in `Web.config`; `targetFramework="net45"` → `net48` in `packages.config` entries.
   - Testing: Rebuild inside the Mono container (step 1). If MSBuild reports missing reference assemblies or other v4.8-specific failures, step down to `v4.7.2`, then `v4.6.1`, rebuilding each time, until the build succeeds. Record the final version actually used and the reason for any step-down directly in this plan file and in a short note in the README.

3. **Rebuild and run the container; validate a 200 response from the root.**
   - Why: This is the user's explicit acceptance criterion.
   - Changes: No source changes; operational validation step.
   - Testing:
     ```sh
     docker build -t upboat-me .
     docker run --rm --publish 8080:8080 --name upboat-me-check upboat-me &
     curl -i http://localhost:8080/
     ```
     Confirm `HTTP/1.1 200 OK` (or `200`) from `/` (routes to `HomeController.Index`). Also spot-check `/robots.txt` and one static asset under `/Content/` to confirm static file serving still works under the refreshed image. Stop/remove the container afterward.

4. **Document the outcome.**
   File: [README.md](../../README.md) (append/update, don't overwrite existing local edits)
   - Why: Record the Mono version actually shipped and the .NET Framework target actually achieved, plus a pointer to the "future ASP.NET Core migration" scope note for anyone revisiting this later.
   - Changes: Short note under "Run locally" confirming the pinned Mono version and any TargetFrameworkVersion step-down, and a one-paragraph "Why not .NET Core" summary referencing the blockers table above.
   - Testing: N/A (documentation).

## Risk Assessment

- **APT keyserver/network access blocked in build sandbox** — mitigation: treat the Mono in-container upgrade as a best-effort separate `RUN` layer; if it fails, fall back to the pinned `mono:6.12.0.182` base alone (already strictly better than the current floating `6.12` tag) and note in the README that 6.12.0.206 could not be fetched in this environment.
- **`TargetFrameworkVersion v4.8` reference assemblies missing/incompatible under Mono 6.12** — mitigation: incremental step-down (4.8 → 4.7.2 → 4.6.1 → 4.5) with a rebuild after each attempt; keep whichever is the highest version that builds cleanly.
- **`msbuild` behaves differently than `xbuild` for this old-style project** (e.g., different default targets/warnings-as-errors behavior) — mitigation: build step is isolated in the Dockerfile; if `msbuild` fails where `xbuild` previously succeeded, revert just that one line to `xbuild` and note it as a known Mono limitation.
- **Existing uncommitted portability fixes in `Global.asax.cs`/`MemeConfig.cs` get overwritten or conflict** — mitigation: this plan only edits Dockerfile, the 3 `.csproj` files, `Web.config`, `packages.config`, and README; it does not touch `Global.asax.cs`/`MemeConfig.cs` content.
- **Mono is deprecated upstream** — no further mitigation available; this is disclosed to the user as a fundamental constraint of the fallback path, not a fixable risk. The blockers table above is the durable rationale for why a future ASP.NET Core migration (rather than further Mono tuning) is the real long-term fix.

## Testing Strategy

- **Build verification:** `docker build -t upboat-me .` succeeds; confirm Mono version via `docker run --rm upboat-me mono --version`.
- **Compilation verification:** MSBuild output inside the Docker build log shows `Build succeeded` for `UpboatMe.csproj` (which transitively builds `UpboatMe.Imaging.csproj`).
- **Manual/functional verification (required by user):** Container run + `curl -i http://localhost:8080/` returns `200`; also check `/robots.txt` (static content) and a `Content/` asset to confirm the static-file and image-rendering paths still work post-upgrade.
- **Unit tests:** `UpboatMeTests` is an MSTest (VS test framework) project with no console runner configured in this repo/Dockerfile; out of scope for automated CI execution here. Note this as a pre-existing gap, not something introduced by this change.

## Rollout Notes

- No production deployment is in scope here — this is a local Docker developer-experience fix.
- If the APT-based Mono upgrade step is kept, document in the Dockerfile comments that it depends on outbound access to `download.mono-project.com` and a GPG keyserver, so future maintainers understand why a build might need the fallback path in a restricted network.
- Flag to the user/team: because Mono is deprecated and this app's dependency stack (System.Web, GDI+, unmaintained NuGet packages, custom `IHttpHandler`/`IHttpModule`) has no direct ASP.NET Core equivalent, a real modernization requires a scoped rewrite project (imaging engine → ImageSharp/SkiaSharp, SDK-style projects, ASP.NET Core MVC, middleware for the two IIS extensions, and dropping/replacing the sprite-thumbnail and analytics packages). Recommend tracking this separately rather than attempting incrementally.
