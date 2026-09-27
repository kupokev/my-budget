#!/usr/bin/env python3
"""Write THIRD-PARTY-NOTICES.md for the packages the shipped app actually restores.

    packaging/third-party-notices.py <output.md>

Reads the dependency list from `dotnet list package` rather than a hand-kept list, so it can't drift
from what is in the build, and pulls each licence out of the package's own .nuspec in the NuGet cache.
"""
import json
import os
import pathlib
import subprocess
import sys
import xml.etree.ElementTree as ET

PROJECT = "src/MyBudget.Desktop/MyBudget.Desktop.csproj"


def restored_packages() -> list[tuple[str, str]]:
    """Every package the desktop app pulls in, direct and transitive."""
    out = subprocess.run(
        ["dotnet", "list", PROJECT, "package", "--include-transitive", "--format", "json"],
        capture_output=True, text=True, check=True).stdout
    found: set[tuple[str, str]] = set()
    for project in json.loads(out).get("projects", []):
        for framework in project.get("frameworks", []):
            for kind in ("topLevelPackages", "transitivePackages"):
                for pkg in framework.get(kind, []):
                    version = pkg.get("resolvedVersion") or pkg.get("requestedVersion")
                    if version:
                        found.add((pkg["id"], version))
    return sorted(found, key=lambda p: p[0].lower())


def cache_roots() -> list[pathlib.Path]:
    roots = [pathlib.Path(os.environ["NUGET_PACKAGES"])] if os.environ.get("NUGET_PACKAGES") else []
    roots.append(pathlib.Path.home() / ".nuget" / "packages")
    return [r for r in roots if r.is_dir()]


def licence_of(package: str, version: str) -> tuple[str, str]:
    """(licence, project url) from the package's own nuspec."""
    for root in cache_roots():
        nuspec = root / package.lower() / version.lower() / f"{package.lower()}.nuspec"
        if not nuspec.is_file():
            continue
        try:
            meta = ET.parse(nuspec).getroot().find("{*}metadata")
            if meta is None:
                continue
            url = (meta.findtext("{*}projectUrl") or "").strip()
            node = meta.find("{*}license")
            if node is not None and (node.text or "").strip():
                kind = node.get("type", "expression")
                return ((node.text or "").strip() if kind == "expression" else f"see {node.text.strip()} in the package", url)
            if legacy := (meta.findtext("{*}licenseUrl") or "").strip():
                return (f"[link]({legacy})", url)
        except ET.ParseError:
            pass
    return ("not stated in the package", "")


def main() -> int:
    dest = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "THIRD-PARTY-NOTICES.md")
    packages = restored_packages()
    if not packages:
        print("no packages found — was the project restored?", file=sys.stderr)
        return 1

    lines = [
        "# Third-party notices",
        "",
        "MyBudget ships the following open-source packages. Each remains under its own licence, held by",
        "its own authors; this file records what is included and under what terms.",
        "",
        "Generated from the restored dependency graph, so it lists what the build actually contains,",
        "including packages pulled in indirectly.",
        "",
        "| Package | Version | Licence | Project |",
        "| --- | --- | --- | --- |",
    ]
    for name, version in packages:
        licence, url = licence_of(name, version)
        link = f"[{name}]({url})" if url else name
        lines += [f"| {link} | {version} | {licence} | {url or ''} |"]

    lines += [
        "",
        "## Not bundled",
        "",
        "**WebKitGTK** renders the window and is a system library, installed by your package manager",
        "rather than shipped here. It is under the LGPL and BSD licences of the WebKit project.",
        "",
        "**.NET runtime** is included in the packaged build and is under the MIT licence.",
        "",
        "**SQLite** itself, bundled as the native library behind SQLitePCLRaw, is in the public domain.",
        "",
    ]
    dest.write_text("\n".join(lines))
    print(f"{dest}: {len(packages)} packages")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
