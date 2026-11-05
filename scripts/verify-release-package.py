"""Read-only NuGet package identity verification for manual public publishing."""
import re
import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree as et

def verify(file: Path, version: str) -> None:
    if not re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?", version):
        raise ValueError("Expected valid SemVer.")
    if file.name != f"Mkx.Templates.Api.{version}.nupkg":
        raise ValueError("Package filename is not the requested release identity.")
    with zipfile.ZipFile(file) as archive:
        members = archive.namelist()
        manifests = [name for name in members if name.endswith(".nuspec")]
        if len(manifests) != 1:
            raise ValueError("Exactly one NuGet manifest is required.")
        root = et.fromstring(archive.read(manifests[0]))
        namespace = root.tag.split("}")[0] + "}" if "}" in root.tag else ""
        metadata = root.find(f"{namespace}metadata")
        if metadata is None or metadata.findtext(f"{namespace}id") != "Mkx.Templates.Api":
            raise ValueError("Unexpected package identity.")
        if metadata.findtext(f"{namespace}version") != version:
            raise ValueError("Package version mismatch.")
        for path in ("clients/typescript/src/client.ts", "clients/dart/lib/src/client.dart"):
            if not any(name.endswith(path) for name in members):
                raise ValueError(f"SDK payload missing: {path}")
        forbidden = ("/node_modules/", "/bin/", "/obj/", "/.dart_tool/", "/dist/")
        if any(any(folder in "/" + name for folder in forbidden) for name in members):
            raise ValueError("Transient build files detected in package.")
        if any(name.lower().endswith((".pfx", ".key", ".env", ".bak")) for name in members):
            raise ValueError("Potential secret file detected in package.")
        print(f"Verified NuGet package {file.name} ({len(members)} entries)")

if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit("Usage: verify-release-package.py FILE VERSION")
    try:
        verify(Path(sys.argv[1]), sys.argv[2])
    except (ValueError, zipfile.BadZipFile, et.ParseError) as exc:
        sys.exit(f"Package verification failed: {exc}")
