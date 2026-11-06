# The base image for anything that runs ffprobe or FFmpeg: the web host validates uploads with ffprobe and the
# worker converts them. CI builds and pushes it, then publishes both images on top of it with ContainerBaseImage.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*
