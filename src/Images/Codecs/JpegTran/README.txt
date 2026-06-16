Optional app-local jpegtran runtime
===================================

Images can use libjpeg-turbo's jpegtran.exe for exact MCU-aligned JPEG crop
and right-angle rotation writeback. Interactive crop/rotation flows warn before
any MCU edge trim and let the user choose lossless trimmed output or exact
raster re-encode.

Expected app-local layout:

  Codecs\JpegTran\jpegtran.exe
  Codecs\JpegTran\jpeg62.dll
  Codecs\JpegTran\LICENSE.md
  Codecs\JpegTran\README.ijg
  Codecs\JpegTran\PROVENANCE.md

The runtime detector also accepts an explicit developer override via:

  IMAGES_JPEGTRAN_EXE=C:\path\to\jpegtran.exe

Do not commit third-party jpegtran binaries. The approved libjpeg-turbo
3.1.4.1 artifact, installer SHA-256, extracted executable SHA-256, source
archive SHA-256, required jpeg62.dll SHA-256, and license file requirements are
recorded in PROVENANCE.md. Use scripts\Prepare-JpegTranBundle.ps1 to stage the
complete jpegtran runtime for release packaging after verifying those hashes.
