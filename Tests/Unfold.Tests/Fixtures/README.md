# Generated media fixtures

These tiny test patterns were generated locally with FFmpeg. They contain no user artwork or audio.

- `pet-motion.mp4`: H.264/YUV420P, 32×24, 12 fps, one second of the `testsrc` filter.
- `pet-motion.gif`: the same pattern at four frames per second; checks pixel/timing preservation.
- `pet-too-long.mp4`: H.264/YUV420P, 16×16, 12 fps, eleven seconds of solid red; checks rejection rather than silent trimming.

Native MP4 tests use the same bundled converter as the app. Prepare it with
`dotnet run --project tools/Unfold.MediaSetup -- <rid> <repository-root>` before testing.
