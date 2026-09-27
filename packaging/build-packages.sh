#!/usr/bin/env bash
# Builds the four Linux packages from one published tree.
#
#   packaging/build-packages.sh <version> <publish-dir> <output-dir>
#
# Runs the same way locally and in CI, so a release can be reproduced without pushing a tag.
# Needs: nfpm (deb/rpm/pacman), appimagetool (AppImage), rsvg-convert or ImageMagick (icon).
set -euo pipefail

VERSION=${1:?version, e.g. 1.0.0}
PUBLISH=${2:?path to the published app}
OUT=${3:-dist}
HERE="$(cd "$(dirname "$0")" && pwd)"

# Regenerated from the restored dependency graph so it can't drift from the build.
NOTICES="${OUT}/THIRD-PARTY-NOTICES.md"
mkdir -p "$OUT"
if command -v python3 >/dev/null; then
  python3 "$HERE/third-party-notices.py" "$NOTICES" || echo "could not generate notices; carrying on" >&2
fi

mkdir -p "$OUT"
STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT

# --- Icons -------------------------------------------------------------------------------------
# One SVG in the repo, rasterised to the sizes the desktop environments look for.
icon_png() {
  local size=$1 dest=$2
  mkdir -p "$(dirname "$dest")"
  if command -v rsvg-convert >/dev/null; then
    rsvg-convert -w "$size" -h "$size" "$HERE/mybudget.svg" -o "$dest"
  elif command -v convert >/dev/null; then
    convert -background none -resize "${size}x${size}" "$HERE/mybudget.svg" "$dest"
  else
    echo "need rsvg-convert or ImageMagick to rasterise the icon" >&2; exit 1
  fi
}

# --- Common layout -----------------------------------------------------------------------------
# The app lives in one directory because the published output expects its files beside the binary;
# /usr/bin gets a small launcher rather than the binary itself.
build_root() {
  local root=$1
  mkdir -p "$root/usr/lib/mybudget" "$root/usr/bin" "$root/usr/share/applications"
  cp -r "$PUBLISH"/. "$root/usr/lib/mybudget/"
  chmod +x "$root/usr/lib/mybudget/MyBudget.Desktop"

  cat > "$root/usr/bin/mybudget" <<'LAUNCH'
#!/bin/sh
cd /usr/lib/mybudget || exit 1
exec ./MyBudget.Desktop "$@"
LAUNCH
  chmod +x "$root/usr/bin/mybudget"

  cp "$HERE/mybudget.desktop" "$root/usr/share/applications/mybudget.desktop"

  # Licence and third-party notices travel with the package, not just the repository.
  mkdir -p "$root/usr/share/doc/mybudget"
  [ -f "$HERE/../LICENSE" ] && cp "$HERE/../LICENSE" "$root/usr/share/doc/mybudget/"
  [ -f "$NOTICES" ] && cp "$NOTICES" "$root/usr/share/doc/mybudget/THIRD-PARTY-NOTICES.md"
  for size in 32 48 64 128 256; do
    icon_png "$size" "$root/usr/share/icons/hicolor/${size}x${size}/apps/mybudget.png"
  done
  mkdir -p "$root/usr/share/icons/hicolor/scalable/apps"
  cp "$HERE/mybudget.svg" "$root/usr/share/icons/hicolor/scalable/apps/mybudget.svg"
}

# --- AppImage ----------------------------------------------------------------------------------
echo "==> AppImage"
APPDIR="$STAGE/MyBudget.AppDir"
build_root "$APPDIR"
cp "$HERE/AppRun" "$APPDIR/AppRun"
chmod +x "$APPDIR/AppRun"
cp "$HERE/mybudget.desktop" "$APPDIR/mybudget.desktop"
icon_png 256 "$APPDIR/mybudget.png"
ARCH=x86_64 appimagetool --no-appstream "$APPDIR" "$OUT/MyBudget-${VERSION}-x86_64.AppImage"

# --- deb / rpm / pacman ------------------------------------------------------------------------
# WebKitGTK is Photino's renderer and is not bundled; each package names its distro's spelling of it.
PKGROOT="$STAGE/pkgroot"
build_root "$PKGROOT"

if ! command -v nfpm >/dev/null; then
  echo "nfpm is not installed; see https://github.com/goreleaser/nfpm/releases" >&2
  exit 1
fi

# nfpm does not expand environment variables inside contents.src, so the config is rendered with the
# staging path and version filled in. It lands in $STAGE, which the trap above cleans up.
NFPM_CONFIG="$STAGE/nfpm.yaml"
sed -e "s|\${PKGROOT}|$PKGROOT|g" -e "s|\${VERSION}|$VERSION|g" "$HERE/nfpm.yaml" > "$NFPM_CONFIG"

# Explicit file names rather than nfpm's defaults, because the release notes and the README name
# these files. nfpm's default deb name carries the "-1" release, which those docs do not.
echo "==> deb"
nfpm package -f "$NFPM_CONFIG" -p deb       -t "$OUT/mybudget_${VERSION}_amd64.deb"
echo "==> rpm"
nfpm package -f "$NFPM_CONFIG" -p rpm       -t "$OUT/mybudget-${VERSION}-1.x86_64.rpm"
echo "==> pacman"
nfpm package -f "$NFPM_CONFIG" -p archlinux -t "$OUT/mybudget-${VERSION}-1-x86_64.pkg.tar.zst"

echo
echo "Built:"
ls -lh "$OUT"
