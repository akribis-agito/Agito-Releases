# Agito-Releases

Central distribution point for Akribis-Agito release binaries. **Download the
named assets and verify against `SHA256SUMS.txt`** — the "Source code (zip /
tar.gz)" links GitHub attaches to every release are auto-generated stubs of this
(nearly empty) repo and cannot be removed; ignore them.

> This repo was formerly named `PC-Toolsets-Releases`. GitHub redirects the old
> repo and release URLs, but **GitHub Pages URLs do not redirect** — use
> `akribis-agito.github.io/Agito-Releases/…`, not the old name.

## Release tag namespaces

One release per **line and version**, carrying every product built for that
version. Product-level detail lives inside `index.json`, not in the tag.

| Namespace | Example | Contents |
|---|---|---|
| `Firmware/vX.Y.Z` | `Firmware/v5.2.0` | Motion-controller firmware, all products: `.hex`/`.bin` + `.ver` (MD5) per product, per-product PC-sim `.zip`, release/feature notes |
| `FPGA/vX.Y.Z` | `FPGA/v5.4.2` | FPGA bitstreams, all products: `.pof`/`.rpd`, `.dat`/`.jed`, `.bit`/`.hdf` + `.ver` per product |
| `Software/AAComm/vX.Y.Z` | `Software/AAComm/v13.0.0` | AAComm / AAMotion SDK packages |
| `Software/PCSuite/vX.Y.Z` | `Software/PCSuite/v17.2.0` | PCSuite installer, Agito edition |
| `Software/PCSuite-MVG/vX.Y.Z` | `Software/PCSuite-MVG/v17.2.0` | PCSuite installer, MVG edition |

Every release additionally carries `SHA256SUMS.txt` covering **all** of its
assets, and an `index.json` describing them.

## `index.json`

The machine-readable contract. Integrations should read products and assets from
here and treat tags as supplying only the line and version.

```json
{
  "schema": 1,
  "line": "Firmware",
  "version": "5.2.0",
  "source": {
    "repo": "akribis-agito/Agito-Releases",
    "migratedFrom": ["firmware/AGD155-AF-2A06/v5.2.0", "..."]
  },
  "products": [
    {
      "name": "AGD301-ET-2D05",
      "variants": [],
      "assets": [
        "AGD301_ET_2D05_V5.2.0.0-0.hex",
        "AGD301_ET_2D05_V5.2.0.0-0.ver",
        "AG300_MAS01_sim_AGD301-ET-2D05_v5.2.0.zip"
      ]
    }
  ],
  "changelog": [],
  "reports": {
    "summary":  { "en": "summary_v5.2.0.en.html" },
    "features": [ { "id": "...", "title": "...", "products": [], "files": { "en": "..." } } ]
  }
}
```

`source.migratedFrom` records the per-product tags a release replaced during the
2026-07-30 consolidation. It is empty for releases created natively under the
current scheme (`FPGA/v5.4.2` onward).

## Verification

```bash
sha256sum -c SHA256SUMS.txt
```

Firmware `.ver` files carry the image MD5 and a **product family** string
(`Type:AGD301/AGC301`) — the family, not the product name. Product names come
from `index.json`.

## Rules

- **Append-only.** A published release is never overwritten or re-tagged; a bad
  release is fixed forward with a new version. The firmware promote pipeline
  enforces this and will refuse a duplicate. The 2026-07-30 consolidation from
  per-product to line-level tags was a one-time migration, recorded in each
  release's `source.migratedFrom`.
- Firmware and FPGA releases here are *promoted copies*: they are created only
  from a tag that passed the full CI certification run in the source repo
  (`Firmware-Main` / `FPGA-main` → `Promote` workflows).

## GitHub Pages

The `gh-pages` branch serves <https://akribis-agito.github.io/Agito-Releases/>:

| Path | Purpose |
|---|---|
| `latest.json` | PCSuite update manifest (polled by the app's update check) |
| `aacomm/` | AAComm API documentation |
| `aamotion/` | AAMotion API documentation |
| `release-notes/<yyyy-mm>/` | Human-readable release notes for a release train (features, fixes, downloads) — e.g. `release-notes/2026-08/` |
