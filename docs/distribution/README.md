# Distribution

Linux packages are built by GitHub Actions and attached to a release.

## Cutting a release

```
git tag v1.0.0
git push origin v1.0.0
```

The `Release` workflow runs the tests, publishes a self-contained `linux-x64` build, and produces
four packages plus `SHA256SUMS.txt`. A tag whose tests fail produces no release.

To rehearse without tagging, run the workflow by hand from the Actions tab and give it a version;
it builds and uploads artifacts but publishes no release.

## What gets built

| File | Notes |
| --- | --- |
| `MyBudget-<v>-x86_64.AppImage` | `chmod +x` and run. No installation, no package manager. |
| `mybudget_<v>_amd64.deb` | Debian, Ubuntu |
| `mybudget-<v>-1.x86_64.rpm` | Fedora, RHEL |
| `mybudget-<v>-1-x86_64.pkg.tar.zst` | Arch |

All four contain the same self-contained build, around 120 MB: the app, the .NET runtime, Photino's
native library and the web assets. No .NET installation is required on the target machine.

**WebKitGTK is the exception.** Photino renders through it and it is not bundled. The deb, rpm and
pacman packages declare it as a dependency so the package manager pulls it in. The AppImage cannot,
so it expects WebKitGTK to be present already — the usual reason an AppImage fails to open a window.

## Layout

The app is installed as a directory rather than a single binary, because the published output expects
its files beside the executable:

```
/usr/lib/mybudget/          the published app
/usr/bin/mybudget           a launcher that cds there and execs it
/usr/share/applications/    the desktop entry
/usr/share/icons/hicolor/   32, 48, 64, 128, 256 and scalable
```

## Building packages locally

```
dotnet publish src/MyBudget.Desktop -c Release -r linux-x64 --self-contained true \
  -p:Version=1.0.0 -o publish
packaging/build-packages.sh 1.0.0 publish dist
```

Needs `fpm` for deb/rpm/pacman, `appimagetool` for the AppImage, and `rsvg-convert` or ImageMagick
for the icons. The workflow installs all of them; the script is the same one CI runs, so a local
build and a released build come out the same way.

## Upgrades and data

The budget lives at `~/.local/share/MyBudget/mybudget.db`, outside anything a package owns, so
installing or removing a package never touches it. Schema changes are applied by EF migrations when
the app starts, so a database from an older version comes forward on first launch.

Export from **Admin → Settings → Back up and restore** before a version jump anyway; it costs a click
and it is the only thing that makes a bad upgrade recoverable.
