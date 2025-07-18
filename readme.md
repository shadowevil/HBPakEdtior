# PAK File Reader Documentation

This document provides a structured breakdown of the PAK file format as interpreted by the provided C# code.

---

## 1. PAK File Structure Overview

| Offset | Size      | Type            | Description                                                  |
|--------|-----------|-----------------|--------------------------------------------------------------|
| 0x00   | 20 bytes  | string          | Magic string: "<Pak file header>" (must match hardcoded)     |
| 0x14   | 3 bytes   | skip            | Reserved/unused bytes                                        |
| 0x17   | 4 bytes   | int32           | Sprite count (N)                                             |
| 0x1B   | 8*N bytes | int32 * 2 per N | Sprite entry offsets and endsets                             |
| ...    | variable  | Sprite data     | Sequence of N Sprite structures                              |

---

## 2. Sprite Entry Structure

Each Sprite entry starts with a SpriteHeader.

| Offset | Size       | Type        | Description                                                   |
|--------|------------|-------------|---------------------------------------------------------------|
| 0x00   | 22 bytes   | string      | Magic string: "<Sprite File Header>" (must match hardcoded)   |
| 0x16   | 78 bytes   | skip        | Padding to 100-byte header total                              |
| 0x64   | 4 bytes    | int32       | Rectangle count (R)                                           |
| 0x68   | 12*R bytes | 6 * int16   | Rectangle data (x, y, w, h, pivotX, pivotY)                   |
| ...    | 54 bytes   | BMP header  | Standard BMP header (bitmap file header)                     |
| ...    | variable   | byte[]      | BMP image data of size `BfSize`                               |

### Rectangle Entry Structure

| Field   | Type  | Description         |
|---------|-------|---------------------|
| x       | int16 | X-coordinate        |
| y       | int16 | Y-coordinate        |
| width   | int16 | Width of rectangle  |
| height  | int16 | Height of rectangle |
| pivotX  | int16 | X pivot offset      |
| pivotY  | int16 | Y pivot offset      |

---

## 3. BMP Header

The BMP header starts 54 bytes before the bitmap data. It is parsed using standard BMP structure:
- **BITMAPFILEHEADER** (14 bytes)
- **BITMAPINFOHEADER** (40 bytes)

These are read using `BmpHeader.ReadFromStream(reader)` and used to reconstruct the image.

---

## Notes

- Strings are assumed to be UTF-8 encoded.
- All integers are read using `EndianBinaryReader`, suggesting endianness is configurable.
- Sprite rectangle and bitmap reading is done per sprite.
- Header magic strings are explicitly verified against hardcoded values:
  - `PAKFileHeader.Magic` = "<Pak file header>"
  - `SpriteHeader.Magic` = "<Sprite File Header>"
