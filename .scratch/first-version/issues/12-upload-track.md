# 12: Upload a Track

Spec: [KleinGeld Music first version](../../music-service/spec.md)

**What to build:** A managing Member adds audio files to a Draft and sees each upload's progress; non-audio files are rejected; Tracks can be removed from a Draft.

**Blocked by:** 10 (Draft Release details)

**Status:** ready-for-agent

- [ ] Each file uploads directly to Blob through its own SAS write link, with real progress
- [ ] Files are validated by ffprobe, not extension: FLAC, WAV, AIFF, ALAC, MP3, AAC; at most 500 MB
- [ ] A Release holds 1–50 Tracks; adding more is refused with a message
- [ ] Each Track shows Uploading until confirmed; the Track title defaults to the file name
- [ ] Removing a Track from a Draft deletes its blobs at once
- [ ] API tests (Aspire testing seam) with small audio fixtures, a non-audio file and the Track limit
