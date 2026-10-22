# Generated media fixtures

These tiny test patterns were generated locally with FFmpeg. They contain no user artwork or audio.

- `pet-motion.mp4`: H.264/YUV420P, 32×24, 12 fps, one second of the `testsrc` filter.
- `pet-motion.gif`: the same pattern at four frames per second; checks pixel/timing preservation.
- `pet-too-long.mp4`: H.264/YUV420P, 16×16, 12 fps, eleven seconds of solid red; checks rejection rather than silent trimming.
- `sound-short.mp3`: generated 440 Hz sine, 0.75 seconds before MP3 encoder padding, mono/44.1 kHz/128 kbps.
- `sound-too-long.mp3`: the same sine for 31.5 seconds; checks rejection rather than silent trimming.

The MP3 files were generated from Python `wave` PCM (amplitude 5000) with `lameenc` 1.8.1,
quality 2, including `encoder.flush()`. This encoder is only a fixture-generation tool;
the app uses the existing LGPL FFmpeg MP3 decoder and PCM WAV encoder.

Native MP4 and MP3 tests use the same bundled converter as the app. Prepare it with
`dotnet run --project tools/Unfold.MediaSetup -- <rid> <repository-root>` before testing.

## Additional pet import formats (2026-10-22)

- `pet-motion.mov`, `.m4v`, `.mkv`: stream-copy of the existing H.264 MP4 into MOV, MP4 (M4V suffix), and Matroska.
- `pet-motion.webm`, `pet-too-long.webm`: VP9 (`libvpx-vp9`, 100k/50k) from the corresponding MP4.
- `pet-motion.avi`, `pet-too-long.avi`: MPEG-4 Part 2 (`mpeg4`, q=3) from the corresponding MP4.
- `pet-too-long.mkv`: stream-copy of the existing eleven-second MP4.
- `pet-motion.apng`: FFmpeg APNG encoding of `pet-motion.gif`, `-plays 0 -f apng`.

These fixtures were encoded with the development FFmpeg; import tests use the shipped LGPL converter.
`AnimatedPetMediaTests` builds additional APNG chunks (including poster, offset, blend and disposal cases)
and lossless WebP animation frames in memory. No external artwork or network downloads are involved.
