# Audio pipeline: formats, conversion, streaming

Type: research
Status: resolved
Blocked by: —

## Question

How should audio go from an Artist's upload to a Member's browser player?

- **Upload formats**: which formats should we accept (MP3, FLAC, WAV, AAC/M4A, OGG)? What do independent musicians typically have?
- **Conversion**: should we convert uploads to one streaming format (e.g. AAC or Opus at a fixed bitrate), keep the original, or both? What tooling does that in .NET (FFmpeg via a wrapper, a background worker), and how long does it take per Track?
- **Browser support**: which formats play in the `<audio>` element across current Chrome, Firefox, Safari (desktop and iOS)?
- **Streaming method**: plain HTTP with range requests (ASP.NET Core `EnableRangeProcessing`, or direct signed URLs to object storage) vs HLS/DASH segments. What do we gain from HLS at our scale, and what does it cost in complexity?
- **Access control**: how to stop non-Members from streaming a Track by copying its URL (short-lived signed URLs vs proxying through the API).
- **Metadata**: reading duration and embedded tags (title, cover art) from uploads.

## Answer

- **Accept** lossless (FLAC, WAV, AIFF, ALAC) plus MP3 and AAC, validated by ffprobe rather than file extension; always keep the original. Raise ASP.NET Core's ~28.6 MB default request limit (a 5-minute WAV is ~53 MB).
- **Convert** once per Track in a background job with FFmpeg (FFMpegCore wrapper) to AAC ~192 kbps in a fast-start `.m4a`, which plays in every target browser including older iOS. Opus/Ogg and FLAC are unreliable on Apple devices.
- **Stream** as a plain HTTP file with range requests (required by iOS Safari) via ASP.NET Core range processing. HLS/DASH isn't worth the complexity for v1.
- **Protect** by default by streaming through the API with a cookie-authenticated check on each request (works with disk or blob; Takedown is immediate). Alternative on Azure Blob: short-lived signed URLs (copied links work until expiry). `<audio>` can't send bearer tokens, so auth must be cookie- or URL-based.
- **Metadata**: TagLib# for tags and cover art, ffprobe for duration/codec; embedded tags only pre-fill the Draft form.
- Gaps: conversion time not benchmarked (expect seconds per Track; measure on the chosen host). CliWrap carries extra non-MIT terms of use; `System.Diagnostics.Process` is an alternative.
- Note for hosting: 192 kbps streams are well below the ~330 kbit/s the hosting research assumed, so egress and home-upload estimates there are high.

Findings, with citations: `docs/research/audio-pipeline.md` on branch `research/audio-pipeline` (pushed to origin; never merged).
