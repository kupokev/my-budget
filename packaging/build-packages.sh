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
ARCH_PKG="$OUT/mybudget-${VERSION}-1-x86_64.pkg.tar.zst"
nfpm package -f "$NFPM_CONFIG" -p archlinux -t "$ARCH_PKG"

# nfpm writes directory modes into the .MTREE as Go's FileMode ("20000000755", permission bits with
# the directory flag on top) where makepkg writes plain "755". pacman compares the raw values, fails,
# and then prints both sides masked — so every install says "directory permissions differ ...
# filesystem: 755 package: 755" for each standard directory. Harmless but noisy, and there is no nfpm
# release with it fixed, so rewrite those entries and repack. File entries are already correct.
fix_arch_dir_modes() {
  local pkg="$1" work
  work=$(mktemp -d)

  tar -xpf "$pkg" -C "$work"
  gzip -dc "$work/.MTREE" > "$work/mtree.txt"
  sed -i -E '/type=dir/ s/ mode=2[0-9]{7}([0-7]{3})/ mode=\1/' "$work/mtree.txt"
  gzip -9 -n -c "$work/mtree.txt" > "$work/.MTREE"
  rm -f "$work/mtree.txt"

  # .PKGINFO must come first, and everything is owned by root regardless of who runs this script.
  local rest
  rest=$(cd "$work" && ls -A | grep -vx '.PKGINFO' | grep -vx '.MTREE')
  (cd "$work" && tar --zstd --owner=0 --group=0 --numeric-owner -cf "$pkg.tmp" .PKGINFO .MTREE $rest)
  mv "$pkg.tmp" "$pkg"
  rm -rf "$work"
}
fix_arch_dir_modes "$ARCH_PKG"

echo
echo "Built:"
ls -lh "$OUT"
