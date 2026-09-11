# upboat.me

A free thing you can use to create, link to, and even embed on-the-fly memes wherever you may be. Head over to [upboat.me](http://upboat.me) to check it out!

## Run locally

From the repository root, run:

```sh
docker build -t upboat-me .
docker run --rm --publish 8080:8080 upboat-me
```

Open [http://localhost:8080](http://localhost:8080) in a browser. Press `Ctrl+C` to stop the application.

You can also run with the local SDK:

```sh
dotnet run --project UpboatMe/UpboatMe.csproj
```

### Runtime and framework notes

- The solution now targets `net10.0`.
- The web host is ASP.NET Core MVC (`Microsoft.NET.Sdk.Web`).
- Image rendering uses SkiaSharp (`SkiaSharp` + `SkiaSharp.NativeAssets.Linux.NoDependencies`) instead of `System.Drawing`.
- Docker uses `mcr.microsoft.com/dotnet/sdk:10.0` for build and `mcr.microsoft.com/dotnet/aspnet:10.0` for runtime.
- Static assets are still stored in `UpboatMe/Content`, `UpboatMe/Scripts`, `UpboatMe/Images`, and `UpboatMe/Fonts`; ASP.NET Core maps those directories directly at runtime instead of relying on a `wwwroot` tree.
- Linux containers install `fonts-liberation2`, `fonts-dejavu-core`, and `fonts-noto-core`. These are used as substitutes when original Windows font families are unavailable.

### Fonts and rendering parity

- Meme config still asks for original font names (`Impact`, `Arial`, `Comic Sans MS`, `Segoe UI`).
- On Linux, these are mapped to available fallback families to keep layout stable.
- Rendered output is validated in both local and Docker runs, but exact raster parity with the legacy Windows/GDI+ output is not guaranteed because Linux substitutes different font files.
- Bundled custom fonts in [UpboatMe/Fonts](UpboatMe/Fonts) are still used for specific templates (for example CSI/Batman variants).
- The Docker image and application bundle use Anton as the deterministic, open-licensed replacement for Impact. The original Microsoft Impact font is not redistributed.

### License notes

- Project source license: see [LICENSE.txt](LICENSE.txt).
- SkiaSharp is pulled from NuGet under the terms published by the package authors.
- The bundled `SFActionManExtended` fonts are redistributed in this repository with their included EULA at [UpboatMe/Fonts/ShyFoundry Freeware EULA.pdf](UpboatMe/Fonts/ShyFoundry%20Freeware%20EULA.pdf).
- Anton is distributed under the SIL Open Font License; its license is included at [UpboatMe/Fonts/Anton-OFL.txt](UpboatMe/Fonts/Anton-OFL.txt).

On Windows, open `UpboatMe.sln` in Visual Studio and run the `UpboatMe` project with IIS Express.
