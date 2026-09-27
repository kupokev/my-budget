#!/usr/bin/env bash
# Builds the four Linux packages from one published tree.
#
#   packaging/build-packages.sh <version> <publish-dir> <output-dir>
#
# Runs the same way locally and in CI, so a release can be reproduced without pushing a tag.
# Needs: fpm (deb/rpm/pacman), appimagetool (AppImage), rsvg-convert or ImageMagick (icon).
set -euo pipefail

VERSION=${1:?version, e.g. 1.0.0}
PUBLISH=${2:?path to the published app}
OUT=${3:-dist}
HERE="$(cd "$(dirname "$0")" && pwd)"

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

fpm_common=(
  -s dir -C "$PKGROOT"
  --name mybudget
  --version "$VERSION"
  --license MIT
  --vendor "MyBudget"
  --maintainer "MyBudget"
  --url "https://github.com/kupokev/my-budget"
  --description "Personal budget app: budget, paycheck estimates, HSA planning and card rewards, running entirely on your own machine."
  --category Office
  -f
)

echo "==> deb"
fpm "${fpm_common[@]}" -t deb \
  --depends "libwebkit2gtk-4.1-0 | libwebkit2gtk-4.0-37" \
  --deb-no-default-config-files \
  -p "$OUT/mybudget_${VERSION}_amd64.deb" .

echo "==> rpm"
fpm "${fpm_common[@]}" -t rpm \
  --depends "webkit2gtk4.1" \
  --rpm-digest sha256 \
  -p "$OUT/mybudget-${VERSION}-1.x86_64.rpm" .

echo "==> pacman"
fpm "${fpm_common[@]}" -t pacman \
  --depends "webkit2gtk-4.1" \
  -p "$OUT/mybudget-${VERSION}-1-x86_64.pkg.tar.zst" .

echo
echo "Built:"
ls -lh "$OUT"
