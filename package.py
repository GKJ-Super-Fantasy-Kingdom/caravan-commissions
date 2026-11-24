"""Build the local-import archive from the verified Release output."""
import hashlib
import json
import struct
from pathlib import Path
from tempfile import TemporaryDirectory
from xml.etree import ElementTree
from zipfile import ZIP_DEFLATED, ZipFile

root = Path(__file__).resolve().parent
listing = root / "thunderstore"
manifest = json.loads((listing / "manifest.json").read_text(encoding="utf-8"))
project = ElementTree.parse(root / "CaravanCommissions.csproj")
version = manifest["version_number"]
framework = project.findtext(".//TargetFramework")
if version != project.findtext(".//Version") or manifest["name"] != "CaravanCommissions":
    raise SystemExit("Package identity does not match the project.")
files = {
    "LICENSE": root / "LICENSE",
    "manifest.json": listing / "manifest.json",
    "README.md": root / "README.md",
    "CONTRIBUTING.md": root / "CONTRIBUTING.md",
    "docs/visitor-calendar.md": root / "docs" / "visitor-calendar.md",
    "CHANGELOG.md": root / "CHANGELOG.md",
    "icon.png": listing / "icon.png",
    "plugins/CaravanCommissions.dll": root / "bin" / "Release" / framework / "CaravanCommissions.dll",
}
icon = files["icon.png"].read_bytes()
if icon[:8] != b"\x89PNG\r\n\x1a\n" or struct.unpack(">II", icon[16:24]) != (256, 256):
    raise SystemExit("Expected a 256 by 256 PNG.")
output = root / "dist" / f"CaravanCommissions-{version}.zip"
output.parent.mkdir(exist_ok=True)
with TemporaryDirectory(dir=output.parent) as folder:
    staged = Path(folder) / output.name
    with ZipFile(staged, "w", ZIP_DEFLATED) as archive:
        for member, source in files.items():
            archive.write(source, member)
    with ZipFile(staged) as archive:
        if archive.testzip() or sorted(archive.namelist()) != sorted(files):
            raise SystemExit("Unexpected or unreadable ZIP entries.")
        for member, source in files.items():
            if archive.read(member) != source.read_bytes():
                raise SystemExit(f"Archive differs from source: {member}")
    staged.replace(output)
print(output)
print("SHA-256", hashlib.sha256(output.read_bytes()).hexdigest())
