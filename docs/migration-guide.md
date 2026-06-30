# Migration Guide

How to bring metadata (tags, ratings, labels, keywords) from other photo tools into Images.

Images reads standard **XMP sidecar** files (`.xmp` alongside your images). The strategy for every source app is the same: export your metadata to XMP sidecars, then open the folder in Images. The `XmpSidecarImportService` picks up `dc:subject`, `xmp:Rating`, `xmp:Label`, `digiKam:TagsList`, `lr:hierarchicalSubject`, and IPTC location fields automatically.

---

## digiKam

1. In digiKam, select all images (or the albums you want to migrate).
2. Run **Settings > Metadata > Write metadata to files** (or right-click > *Write Metadata to File*).
3. digiKam writes `.xmp` sidecar files next to each image containing tags, ratings, labels, and location.
4. Open the folder in Images. Metadata is read automatically.

## XnView MP

1. In XnView MP, select the images you want to migrate.
2. Run the built-in **Export to XMP** option (Tools > Metadata > Export XMP sidecar).
3. XnView writes standard `.xmp` sidecar files next to each image.
4. Open the folder in Images. Metadata is read automatically.

## Apple Photos

Apple Photos stores its database in `.photoslibrary/database/Photos.sqlite`, which uses a Core Data schema that **changes with every macOS release**. Do not attempt to read it directly.

Instead, use [osxphotos](https://github.com/RhetTbull/osxphotos) to export with XMP sidecars:

```bash
# On macOS, install osxphotos
pip install osxphotos

# Export your entire library with XMP sidecars
osxphotos export /path/to/export --sidecar xmp
```

This exports all photos with their metadata (keywords, albums, favorites, locations, persons) written into standard XMP sidecar files. Open the exported folder in Images.

**Why not read Photos.sqlite directly?** The Core Data schema is undocumented and breaks across macOS versions (Catalina, Monterey, Ventura, Sonoma, Sequoia each changed it). The `osxphotos` project maintains per-version schema mappings full-time. Delegating to it is the only reliable path.

## Lightroom Classic

Lightroom catalog import (`.lrcat`) is planned (M-02). In the meantime, Lightroom can write XMP sidecars natively:

1. In Lightroom, select all images.
2. Run **Metadata > Save Metadata to Files** (Ctrl+S).
3. Lightroom writes `.xmp` sidecar files with ratings, keywords, collections, and develop settings.
4. Open the folder in Images. Ratings, keywords, and labels are read automatically.

## Picasa

Picasa stores metadata in `.picasa.ini` files (one per folder) and `contacts.xml` (global face-name mappings). These are not standard XMP and require dedicated parsing.

Use **Import Inbox > Import Picasa metadata** on a folder that contains `.picasa.ini`. Images reads a local `contacts.xml` file from the selected folder or its parent when present, then writes XMP sidecars next to the images without changing the originals.

The importer converts Picasa star/rating values to `xmp:Rating`, album assignments to `album:` tags plus Lightroom hierarchical subjects, and face rectangles to XMP region records. Resolved contacts are also written as `person:` tags.

## IrfanView

IrfanView's `.thumbs.db` is a **thumbnail cache** with no tag, rating, or metadata value. No migration is needed. If you used IrfanView's batch-rename or IPTC editor, that metadata is already embedded in the image files and Images reads it directly.

---

## General Notes

- Images treats XMP sidecars as the authoritative metadata source when present.
- Sidecar files must be named `<filename>.xmp` (e.g., `IMG_0001.jpg.xmp`) and sit in the same directory as the image.
- If your source tool can write IPTC/XMP directly into image files (embedded metadata), Images reads that too --- sidecars are not strictly required, but they avoid modifying original files.
