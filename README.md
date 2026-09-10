# upboat.me

A free thing you can use to create, link to, and even embed on-the-fly memes wherever you may be. Head over to [upboat.me](http://upboat.me) to check it out!

## Run locally

Docker is the supported local development environment for macOS and Linux. From the repository root, run:

```sh
docker build -t upboat-me .
docker run --rm --publish 8080:8080 upboat-me
```

Open [http://localhost:8080](http://localhost:8080) in a browser. Press `Ctrl+C` to stop the application.

### Runtime and framework notes

- Docker runtime is pinned to `mono:6.12.0.182` for deterministic local builds.
- The web app and imaging project target .NET Framework 4.8 (`v4.8`), which is the latest .NET Framework line supported for this legacy ASP.NET MVC application shape.
- A direct move to modern .NET (ASP.NET Core on .NET 8/9) is not an in-place runtime upgrade for this codebase because it depends on `System.Web`, legacy MVC/Web API packages, and `System.Drawing`-based rendering. That migration requires a scoped rewrite.

On Windows, open `UpboatMe.sln` in Visual Studio and run the `UpboatMe` project with IIS Express.
