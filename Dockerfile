FROM mono:6.12.0.182

WORKDIR /app

RUN sed -i 's|deb.debian.org/debian|archive.debian.org/debian|g; s|deb.debian.org/debian-security|archive.debian.org/debian-security|g' /etc/apt/sources.list \
    && apt-get -o Acquire::Check-Valid-Until=false update \
    && apt-get install --no-install-recommends --yes libgdiplus mono-xsp4 \
    && rm -rf /var/lib/apt/lists/*

COPY . .

RUN echo "docker-build" > UpboatMe/App_Data/version.txt \
    && mono .nuget/NuGet.exe restore UpboatMe.sln -NonInteractive -PackagesDirectory packages \
    && msbuild UpboatMe/UpboatMe.csproj /p:Configuration=Debug /p:Platform=AnyCPU /p:PreBuildEvent=

EXPOSE 8080

CMD ["xsp4", "--port", "8080", "--nonstop", "--applications=/:/app/UpboatMe"]