# Code Review Report — .NET/Mono Runtime Upgrade

**Review model:** Claude Opus 4.8 — chosen because verifying this change requires reconciling a legacy multi-project .NET Framework/Mono codebase against an approved plan and design-review constraints, plus empirically re-running the Docker build and root-200 acceptance gate.

**Re-invocation attempt:** 1 (initial review).

## Summary
- **Plan Alignment:** Strong. Implementation follows the approved fallback path (pin Mono image, drop git dependency, switch xbuild->msbuild, retarget web+imaging to .NET Framework 4.8) and honors the design-review constraints (no unproven APT overlay, no fake git surface, build only web+imaging, root must return 200).
- **Overall Assessment:** Ready to merge (with optional cleanup suggestions).
- **REQUIRED Issues:** 0
- **OPTIONAL Issues:** 4

## Independent Verification (performed by reviewer)
- docker build from the current worktree -> Build succeeded (0 errors; 1 pre-existing benign warning: MSB3245 unresolved System.Web.Entity, unrelated to this change).
- docker run ... mono --version -> Mono 6.12.0.182 (matches report).
- Fresh container, curl http://127.0.0.1:8085/ -> HTTP 200 (root, routes to HomeController.Index).
- curl .../robots.txt static asset -> HTTP 200 (static pipeline intact).
- Temporary review image removed after verification.

The report PASS claims for all three gates (build, runtime version, root 200) are credible and reproduced.

## Detailed Findings

### REQUIRED Issues
None. The user acceptance criterion (200 from root) is met and independently reproduced, and the delivery matches the approved plan + design-review verdict.

### OPTIONAL Issues

1. Undisclosed cosmetic reformatting in files the plan said it would not touch — UpboatMe/App_Start/MemeConfig.cs, UpboatMe/Global.asax.cs
   - Suggestion: Revert the whitespace/brace-style/LINQ-chain reformatting in these two files (functionally identical, likely auto-format-on-save) to keep the diff minimal.
   - Rationale: The plan explicitly stated it does not touch Global.asax.cs/MemeConfig.cs content, and the coder report did not disclose these edits. Non-blocking because behavior is unchanged and build/200 gates pass.

2. "Latest Mono" not literally achieved; README omits the documented caveat — Dockerfile, README.md
   - Suggestion: Add the note the plan Rollout section called for — upstream Mono 6.12.0.206 exists but Docker Hub newest published image is 6.12.0.182, so the pinned image is the newest available and 206 was intentionally not fetched (design-review constraint against an unproven APT overlay).
   - Rationale: User asked for "the latest Mono version." Delivering the newest published image is the correct outcome, but README frames the pin only as deterministic builds. Documentation completeness only.

3. Framework-target inconsistency: csproj/Web.config say v4.8 but packages.config still says net45 — UpboatMe/packages.config
   - Suggestion: For coherence, bump the targetFramework net45 attributes to net48, or add a note that this metadata is intentionally left to avoid a broad packages.config rewrite.
   - Rationale: packages.config targetFramework is NuGet install-time metadata and does not affect the Mono build (verified). Design review warned against broad packages.config rewriting, so the choice is defensible — cosmetic inconsistency only.

4. UpboatMeTests.csproj left at v4.5 and tests not executed — UpboatMeTests/UpboatMeTests.csproj
   - Suggestion: Track as follow-up; optionally note in README that the test project remains v4.5 with no container test runner.
   - Rationale: Test project is not built/run in the Docker path; design review narrowed scope. Pre-existing gap, disclosed by coder.

### Testing Assessment
- Unit Test Coverage: No automated test execution added (out of scope; MSTest project has no container runner). Pre-existing gap.
- Integration/Functional: Root 200 and static-asset 200 verified end-to-end in a running container.
- Edge Cases: /p:PreBuildEvent= override + echo > version.txt correctly suppresses the git-based prebuild; version.txt is consumed by HtmlHelperExtensions.LastUpdated with a File.Exists guard, so simplified content is safe.

### Security & Performance
- Security: Removing build-time git install and fake git-repo init reduces image surface — net improvement. Pinned exact base tag improves reproducibility/supply-chain determinism. No secrets or unsafe patterns introduced. Mono remains upstream-deprecated (disclosed constraint, not a fixable defect here).
- Performance: No runtime performance impact; xbuild->msbuild is a supported build-driver swap only.

## Recommendation
**APPROVED WITH OPTIONAL SUGGESTIONS**

No REQUIRED findings. The implementation satisfies the user acceptance criterion (independently reproduced HTTP 200 from root), aligns with the approved plan, and respects the design-review constraints. The four OPTIONAL items are non-blocking polish and do not require Coder re-invocation. If the orchestrator elects to address them, instruct the Coder to continue on the same master worktree for continuity.
