"""Export actual development Swagger JSON without requiring a SQL database.

Requires a built .NET solution. Uses a disposable JWT key, explicitly disables
all bootstrap/migration/external IdP features and binds to 127.0.0.1 only.
"""
import argparse
import json
import os
import secrets
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TARGET = ROOT / "openapi" / "v1.json"

def export(configuration: str) -> bytes:
    with socket.socket() as sock:
        sock.bind(("127.0.0.1", 0))
        port = sock.getsockname()[1]
    env = os.environ.copy()
    env.update({
        "ASPNETCORE_ENVIRONMENT": "Development",
        "DOTNET_ENVIRONMENT": "Development",
        "ASPNETCORE_URLS": f"http://127.0.0.1:{port}",
        "Jwt__SigningKey": secrets.token_hex(48),
        "BootstrapAdmin__Enabled": "false",
        "Database__ApplyMigrationsOnStartup": "false",
        "Passkeys__Enabled": "false",
        "AccountLifecycle__Enabled": "false",
        "ExternalOidc__Enabled": "false",
        "OpaqueIntrospection__Enabled": "false",
    })
    command = [
        "dotnet", "run", "--no-build", "--no-launch-profile",
        "--project", str(next((ROOT / "src" / "Server").glob("*.Server/*.csproj"))),
        "--configuration", configuration
    ]
    with tempfile.TemporaryFile(mode="w+t", encoding="utf-8") as log:
        process = subprocess.Popen(command, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT)
        try:
            url = f"http://127.0.0.1:{port}/swagger/v1/swagger.json"
            end = time.monotonic() + 40
            while time.monotonic() < end:
                if process.poll() is not None:
                    break
                try:
                    with urllib.request.urlopen(url, timeout=1.2) as response:
                        if response.status != 200:
                            raise RuntimeError(f"Swagger HTTP {response.status}")
                        result = json.load(response)
                        if not result.get("paths") or "components" not in result:
                            raise RuntimeError("Invalid OpenAPI output")
                        # Canonical order avoids unstable diffs; the server is the authority.
                        return (json.dumps(result, indent=2, ensure_ascii=False, sort_keys=True) + "\n").encode("utf-8")
                except (urllib.error.URLError, TimeoutError, ConnectionError):
                    time.sleep(0.25)
            log.seek(0)
            raise RuntimeError("Could not export running API Swagger:\n" + log.read()[-5500:])
        finally:
            process.terminate()
            try:
                process.wait(timeout=8)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--configuration", choices=["Debug", "Release"], default="Release")
    args = ap.parse_args()
    data = export(args.configuration)
    if args.check:
        if not TARGET.exists() or TARGET.read_bytes() != data:
            sys.exit("OpenAPI schema drift: run python tools/export_openapi.py and regenerate SDKs")
        print("OpenAPI snapshot matches live server contract")
        return
    TARGET.parent.mkdir(exist_ok=True)
    TARGET.write_bytes(data)
    doc = json.loads(data)
    print(f"Exported {len(doc['paths'])} paths and {len(doc['components']['schemas'])} models to {TARGET}")

if __name__ == "__main__":
    main()
