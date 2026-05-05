Optional app-local Ghostscript runtime
=====================================

Images can render EPS, PS, PDF, and AI previews through Magick.NET when
Ghostscript is available beside the app.

Expected layout after publish:

  Images.exe
  Codecs\Ghostscript\bin\gsdll64.dll
  Codecs\Ghostscript\bin\gswin64c.exe  (optional; used for version display)
  Codecs\Ghostscript\...

The runtime detector also accepts a flat Codecs\Ghostscript folder that contains
gsdll64.dll directly. IMAGES_GHOSTSCRIPT_DIR can point to the same layout for
development/testing.

Official release artifacts may bundle Ghostscript app-local so users do not
need to install it separately. The bundled AGPL Ghostscript license file is
installed at:

  Codecs\Ghostscript\doc\COPYING

The matching Ghostscript source archive and SHA-256 provenance are listed in
the GitHub release notes.

Do not commit third-party Ghostscript binaries. Stage them only for release
artifact builds after the exact runtime and license model have been reviewed.
