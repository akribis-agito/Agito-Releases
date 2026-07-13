# Agito-Releases

Central distribution point for Akribis-Agito release binaries. **Download the
named assets and verify against `SHA256SUMS.txt`** - the "Source code (zip /
tar.gz)" links GitHub attaches to every release are auto-generated stubs of this
(nearly empty) repo and cannot be removed; ignore them.

## Release tag namespaces

| Namespace | Example | Contents |
|---|---|---|
| `firmware/<Product>/vX.Y.Z` | `firmware/AGD301-ET-2D05/v5.0.0` | Motion-controller firmware, one release per product: `.hex`/`.bin` + `.ver` (MD5), per-product `SHA256SUMS.txt`, `release_manifest.json` |
| `fpga/<Product>/vX.Y.Z` | `fpga/<PublicProduct>/v1.2.0` | FPGA bitstream packages (reserved namespace) |
| `aacomm/vX.Y.Z` | `aacomm/v13.0.0` | AAComm / AAMotion SDKs |
| `pcsuite-vX.Y.Z-<edition>` | `pcsuite-v17.2.0-agito` | PCSuite (per-edition: `-agito`, `-mvg`) |

## Rules

- **Append-only.** A published release is never overwritten or re-tagged; a bad
  release is fixed forward with a new version. The firmware promote pipeline
  enforces this and will refuse a duplicate.
- Firmware releases here are *promoted copies*: they are created only from a tag
  that passed the full CI certification run in the source repo
  (Firmware-Main `Release - certify, tag & publish` -> `Promote` workflows).
- `release_manifest.json` on each firmware release records the source repo, the
  source tag, and the SHA256 of every asset - it is machine-readable provenance
  for dashboards and tooling.
