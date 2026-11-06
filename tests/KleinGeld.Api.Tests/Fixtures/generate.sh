#!/bin/sh
# Regenerates the audio fixtures. Run from this folder, in any image with FFmpeg, e.g. docker/ffmpeg-base.Dockerfile.
set -e
tone="-hide_banner -loglevel error -y -f lavfi -i sine=frequency=440:duration=0.5:sample_rate=22050 -map_metadata -1"
ffmpeg $tone tone.flac
ffmpeg $tone tone.wav
ffmpeg $tone tone.aiff
ffmpeg $tone -c:a alac tone-alac.m4a
ffmpeg $tone -c:a libmp3lame tone.mp3
ffmpeg $tone -c:a aac tone-aac.m4a
ffmpeg $tone -c:a libvorbis tone.ogg
ffmpeg -hide_banner -loglevel error -y -f lavfi -i color=c=blue:s=64x64:d=0.5:r=5 -f lavfi -i sine=duration=0.5 \
    -c:v mpeg4 -c:a aac -shortest video.mp4
printf 'This is a text file wearing a .flac extension.' > not-audio.flac
# A sound header over undecodable audio: ffprobe accepts it, and FFmpeg exits cleanly having converted nothing.
head -c 3000 tone.flac > corrupt.flac
head -c 6000 /dev/zero | tr '\0' 'x' >> corrupt.flac
for file in tone* video.mp4; do
    echo "$file: $(ffprobe -v error -show_entries format=format_name:stream=codec_type,codec_name -of csv=p=0 "$file" | tr '\n' ' ')"
done
