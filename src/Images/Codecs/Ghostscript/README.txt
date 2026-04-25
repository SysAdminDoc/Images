Optional app-local Ghostscript runtime
=====================================

Images can render EPS, PS, PDF, and AI previews through Magick.NET when
Ghostscript is available beside the app.

Expected layout after publish:

  Images.exe
  Codecs\Ghostscript\bin\gsdll64.dll
  Codecs\Ghostscript\bin\gswin64c.exe
  Codecs\Ghostscript\...

The runtime detector also accepts a flat Codecs\Ghostscript folder that contains
gsdll64.dll and gswin64c.exe directly. IMAGES_GHOSTSCRIPT_DIR can point to the
same layout for development/testing.

Do not commit third-party Ghostscript binaries unless the release owner has
confirmed redistribution rights for the exact package being shipped.
