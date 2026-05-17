# Competitor Matrix

Date: 2026-05-17

## Direct Viewers

| Product | Type | Notable features | Evidence | Lesson for Images |
| --- | --- | --- | --- | --- |
| ImageGlass | Windows viewer, source available | Broad ImageMagick-backed formats, supported-format docs, Ghostscript-dependent document formats, app configuration docs. | https://imageglass.org/docs/supported-formats, https://github.com/d2phap/ImageGlass | Keep a user-facing capability matrix and make runtime dependencies explicit. |
| PicView | Windows/macOS viewer | Fast browsing, configurable UI, zoom preview options, PDF export. | https://picview.org/download/ | Fit polish, navigation feel, and export convenience matter as much as raw codec count. |
| nomacs | Cross-platform OSS viewer | RAW/PSD support, local/LAN synchronized pan/zoom, overlay compare, format-provider docs. | https://github.com/nomacs/nomacs, https://nomacs.org/blog/synchronization/, https://nomacs.org/docs/documentation/features/ | Compare/overlay mode is a high-fit feature and should sync pan/zoom/rotation. |
| QuickLook | Windows preview utility | Spacebar instant preview from Explorer, keyboard navigation even when preview lacks focus. | https://github.com/QL-Win/QuickLook | Keep peek/shell workflows lightweight and separate from the main app chrome. |
| qView | Minimal cross-platform viewer | Minimal UI, common format support, practical no-clutter positioning. | https://interversehq.com/qview/, https://github.com/jurplel/qView | Images should preserve image-first quiet chrome despite adding power workflows. |
| JPEGView | Lightweight viewer/editor | Fast viewer with JPEG-centric lightweight editing heritage. | https://github.com/sylikc/jpegview | Lossless JPEG paths are strategically useful but must stay optional/provenance-gated. |
| qimgv | Image/video hybrid viewer | Fast Qt viewer with optional video-oriented workflow. | https://github.com/easymodo/qimgv | Video support is tempting but would dilute Images until image/catalog workflows are stronger. |
| Oculante | Rust image viewer | Lightweight cross-platform viewer reference. | https://github.com/woelper/oculante | Modern viewers compete on speed and clean input model; use as a UX pressure source. |
| NeeView | Book/comic viewer | Book navigation, panelized reading workflows, keyboard-centric reading. | https://neelabo.github.io/NeeView/en-us/userguide.html | Archive book mode should keep improving as a first-class reading workflow. |
| YACReader | Comic reader/library | Comic library and reading workflows. | https://www.yacreader.com/ | Books need library state, progress, spreads, and cover behavior rather than plain archive decode. |
| Geeqie | Image viewer/organizer | Metadata-oriented browsing and comparison. | https://www.geeqie.org/ | Metadata and compare workflows are a natural bridge between viewer and catalog. |

## Commercial And Productized Tools

| Product | Type | Notable features | Evidence | Lesson for Images |
| --- | --- | --- | --- | --- |
| XnView MP | Freeware/commercial viewer/organizer | 500+ formats, export to many formats, browser, metadata, batch conversion, duplicate workflows. | https://www.xnview.com/en/xnviewmp/ | Images needs catalog and batch/export polish to compete beyond simple viewing. |
| FastStone Image Viewer | Freeware viewer/editor/converter | Browser, editor, batch converter, histogram, Save As quality/file-size comparison, crop/draw board. | https://www.faststone.org/FSViewerDetail.htm | A Squoosh/FastStone-style export comparison workbench is a strong local-first differentiator. |
| ACDSee Ultimate 2026 | Commercial DAM/editor | Manage/search/view/develop/edit, AI face detection, People Mode, AI keywords, layered editing. | https://www.acdsee.com/en/products/photo-studio-ultimate/features/ | AI features should serve organization and workflow, not exist as disconnected demos. |
| Eagle | Commercial digital asset manager | Collections, tags, design asset library, import/copy-based library model. | https://eagle.cool/ | Avoid source-file lock-in. If Images adds a catalog, keep source files as the authority. |
| PureRef | Commercial/reference canvas | Always-on-top, always-on-top-of app, transparent/reference modes, GIF playback, drawing, groups, notes. | https://www.pureref.com/handbook/2.0/features/ | Pinned overlay and reference board can become a mature design-reference workflow without becoming a full editor. |

## Adjacent Local Photo Platforms

| Product | Type | Notable features | Evidence | Lesson for Images |
| --- | --- | --- | --- | --- |
| digiKam | OSS photo manager | Advanced search, tags, geolocation, face recognition, similarity database, duplicate/search-by-sketch. | https://www.digikam.org/about/features/, https://docs.digikam.org/en/getting_started/database_intro.html | Catalog schema and separate similarity/index databases should precede smart search. |
| Immich | Self-hosted photo platform | CLIP contextual search, people/faces, OCR search, locations, tags, star ratings, machine-learning jobs. | https://docs.immich.app/features/searching/, https://docs.immich.app/features/facial-recognition | Model jobs need clear queues, embeddings, index versioning, and rebuild controls. |
| PhotoPrism | Self-hosted photo app | Private photo browsing, filters, labels, location, color/chroma/quality, face detection pipeline. | https://www.photoprism.app/features, https://docs.photoprism.app/user-guide/ai/face-recognition/ | Search should combine metadata and derived signals while staying privacy-visible. |
| Czkawka | OSS duplicate/junk cleaner | Duplicate files, similar images, perceptual hashing, broken files. | https://czkawka.net/, https://github.com/qarmin/czkawka | Duplicate cleanup needs transparent confidence, false-positive control, and safe actions. |
| Hydrus | Local collection manager | Tag-heavy local media management. | https://hydrusnetwork.github.io/hydrus/ | Tag power can overwhelm; Images should keep local tags scoped and approachable. |
| PhotoDemon | OSS photo editor | Local editor workflow with broad effects. | https://photodemon.org/ | Images can add editing workbenches but should preserve non-destructive sidecars and viewer speed. |
| Squoosh | Web converter | Before/after visual comparison, codec settings, output size feedback. | https://squoosh.app/, https://github.com/GoogleChromeLabs/squoosh | Export workbench should make quality/size tradeoffs visible before writing. |
| Upscayl | Local upscaler | Model-backed local image upscaling. | https://github.com/upscayl/upscayl | Upscaling belongs behind a model/runtime manager and clear hardware expectations. |

## Specialized Imaging References

| Project | Domain | Notable features | Evidence | Lesson for Images |
| --- | --- | --- | --- | --- |
| OpenSeadragon | Deep zoom | Tile-based large image viewing. | https://openseadragon.github.io/ | Gigapixel support needs a tile engine, not larger bitmaps in current viewer code. |
| OpenSlide | Whole-slide imaging | Pyramidal slide image support. | https://openslide.org/ | Large-image formats need metadata and tile awareness from the start. |
| Bio-Formats | Scientific imaging | Broad scientific image metadata and format handling. | https://www.openmicroscopy.org/bio-formats/ | Scientific format support would be a separate tier, not a casual extension. |
| napari | Multidimensional viewer | Layered N-dimensional image exploration. | https://napari.org/ | Avoid multidimensional scope creep unless a clear target user emerges. |
| QuPath | Pathology imaging | Annotation and analysis over large images. | https://qupath.github.io/ | Annotation work for huge images needs different storage/rendering than simple XMP overlays. |
| libvips | Image processing | Streaming, low-memory image processing. | https://www.libvips.org/ | Consider as an engine reference for future large/batch processing, not immediate dependency. |
| OpenImageIO | VFX/film IO | Professional image IO and color pipeline. | https://openimageio.readthedocs.io/en/latest/ | Color-management and professional formats need explicit policy before dependency expansion. |
| OpenColorIO | Color management | Configurable display/view transforms and color workflows. | https://opencolorio.readthedocs.io/en/latest/releases/ | ICC/OCIO decisions should be made before advanced color UI. |

## Positioning Conclusion

Images should not try to become a full Lightroom, digiKam, or ACDSee clone next. Its best strategic lane is:

- Faster and more trustworthy than Windows Photos.
- More modern and privacy-explicit than legacy viewers.
- More workflow-complete than minimal viewers.
- More local and source-file-respecting than DAMs.
- Stronger at codec/runtime transparency than most competitors.

The roadmap should therefore prioritize trust, settings/accessibility, compare/export/culling workflows, catalog foundations, and opt-in model infrastructure before large AI feature surfaces.
