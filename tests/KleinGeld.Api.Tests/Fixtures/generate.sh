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
# Tagged files: a 1000x1000 cover passes the cover checks, a 1200x1000 one fails them.
quiet="-hide_banner -loglevel error -y"
tags="-metadata title=Zonsopgang -metadata album=Avondrood -metadata date=2019 -metadata genre=Polder-pop"
ffmpeg $quiet -f lavfi -i color=c=red:s=1000x1000 -frames:v 1 -update 1 cover-square.png
ffmpeg $quiet -f lavfi -i color=c=red:s=1200x1000 -frames:v 1 -update 1 cover-wide.png
ffmpeg $tone $tags -c:a libmp3lame tagged.mp3
ffmpeg $tone $tags tagged.flac
ffmpeg $tone $tags -c:a aac tagged.m4a
for shape in square wide; do
    ffmpeg $quiet -i tagged.mp3 -i cover-$shape.png -map 0 -map 1 -map_metadata 0 -c:a copy -c:v png \
        -disposition:v attached_pic -id3v2_version 3 tagged-$shape-cover.mp3
done
rm cover-square.png cover-wide.png
for file in tone* tagged* video.mp4; do
    echo "$file: $(ffprobe -v error -show_entries format=format_name:stream=codec_type,codec_name -of csv=p=0 "$file" | tr '\n' ' ')"
done
