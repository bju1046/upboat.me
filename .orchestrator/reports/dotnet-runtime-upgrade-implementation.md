# Implementation Report: dotnet-runtime-upgrade

## Model and rationale
Implemented by GPT-5.3-Codex.

I treated the inline design review as authoritative and followed the smallest viable path for this legacy ASP.NET MVC 4 / System.Web codebase:
- No ASP.NET Core migration attempt (infeasible as an in-place runtime upgrade).
- Mono runtime container hardening and determinism first.
- Framework retarget attempt only for the build-relevant projects (web + transitive imaging), keeping scope focused on the required HTTP 200 runtime goal.

## Summary of what was implemented
1. Pinned Docker runtime image from floating `mono:6.12` to exact `mono:6.12.0.182`.
2. Removed build-time `git` dependency and fake in-container git repo initialization.
3. Replaced git-based prebuild output generation with deterministic file generation:
   - `echo "docker-build" > UpboatMe/App_Data/version.txt`
4. Switched deprecated `xbuild` to `msbuild` for web project build in Docker.
5. Attempted and retained .NET Framework bump to v4.8 where it matters for runtime delivery:
   - `UpboatMe/UpboatMe.csproj`: v4.5 -> v4.8
   - `UpboatMe.Imaging/UpboatMe.Imaging.csproj`: v4.5 -> v4.8
   - `UpboatMe/Web.config`: `httpRuntime` and `compilation` targetFramework 4.5 -> 4.8
6. Updated README runtime notes to document pinned Mono runtime and rationale for not doing an ASP.NET Core in-place migration.

## Files modified
- `Dockerfile`
- `UpboatMe/UpboatMe.csproj`
- `UpboatMe.Imaging/UpboatMe.Imaging.csproj`
- `UpboatMe/Web.config`
- `README.md`

## Validation gates and commands run

### 1) Image build gate
Command:
```sh
docker build --pull --no-cache -t upboat-me-runtime .
```
Result: PASS
- Build completed successfully.
- Image produced: `upboat-me-runtime`.

### 2) Runtime version gate
Command:
```sh
docker run --rm upboat-me-runtime mono --version
```
Result: PASS
- Reported version:
  - `Mono JIT compiler version 6.12.0.182`

This exactly matches the pinned Docker runtime choice.

### 3) Root HTTP 200 gate
Commands:
```sh
docker run --rm -p 8080:8080 --name upboat-me-check upboat-me-runtime
curl -fsS -o /dev/null -w "%{http_code}" http://127.0.0.1:8080/
```
Result: PASS
- HTTP status returned: `200`

## Actual framework/runtime result
- Modern .NET (ASP.NET Core/.NET 8/9): Not implemented (not feasible as an in-place upgrade for this codebase architecture).
- Runtime delivered: Mono in Docker.
- Docker runtime version: `mono:6.12.0.182` (pinned exact tag).
- .NET Framework target used by runtime projects: v4.8 for web and imaging projects.

## Breaking-change handling
- Removed dependence on git metadata at build time by generating `App_Data/version.txt` directly in Docker build.
- Kept application hosting model and xsp4 runtime command unchanged to avoid behavioral regression.
- Upgraded build driver from deprecated xbuild to msbuild; confirmed successful compile in Docker build log.

## Tests
- No MSTest project execution was added or required for this task.
- The existing test project (`UpboatMeTests`) remains out of scope for this runtime delivery validation.
- Primary acceptance criterion (`/` returns HTTP 200) was met.

## Deviations from plan/review and why
1. Did not implement Mono APT overlay to 6.12.0.206.
   - Reason: Design review explicitly required defaulting to pinned exact image strategy unless external package-source path is proven in this environment.
   - Outcome: Chosen deterministic and lower-risk Strategy A (pinned image, no secondary Mono source overlay).

2. Did not retarget `UpboatMeTests/UpboatMeTests.csproj` or rewrite `UpboatMe/packages.config` targetFramework metadata.
   - Reason: Design review corrected scope to runtime web delivery and warned against blind packages.config rewrites.
   - Outcome: Kept blast radius small and aligned with root-200 acceptance requirement.

3. Temporary branch creation occurred and was then reversed.
   - I briefly created a local branch during implementation, then switched back to `master` and deleted the temporary branch to satisfy the explicit no-branch directive.

## Final status
Task objectives achieved:
- Updated project to the latest feasible runtime path for this architecture.
- Updated build/runtime container path and removed unnecessary build-time dependency.
- Attempted modern framework bump and retained v4.8 where validated.
- Successfully built and served locally with HTTP 200 from root.
