Optional app-local jpegtran runtime
===================================

Images can use libjpeg-turbo's jpegtran.exe for exact MCU-aligned JPEG crop
writeback after the exact runtime artifact is approved. Rotation writeback
and trim-confirmation UI are still future work.

Expected app-local layout:

  Codecs\JpegTran\jpegtran.exe
  Codecs\JpegTran\LICENSE.md
  Codecs\JpegTran\README.ijg

The runtime detector also accepts an explicit developer override via:

  IMAGES_JPEGTRAN_EXE=C:\path\to\jpegtran.exe

Do not commit third-party jpegtran binaries. Stage the approved runtime only
for release packaging after recording the upstream release URL, license files,
artifact SHA-256, and source archive provenance in docs and release notes.
