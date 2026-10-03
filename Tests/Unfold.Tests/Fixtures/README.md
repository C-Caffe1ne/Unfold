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
