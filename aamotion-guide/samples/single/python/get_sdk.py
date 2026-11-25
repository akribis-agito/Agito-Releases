"""下载 AAMotionNative SDK，校验后把本机平台的库与 AACommServerNative 放到本文件旁边。
Download the AAMotionNative SDK, verify it, and put this platform's library and AACommServerNative beside this file.

    python get_sdk.py

只需运行一次；之后运行 Demo 不再联网。来源是 Agito 的公开发布页，按发布页的 SHA256SUMS.txt 校验。
Run it once; the demos never go online afterwards. The source is Agito's public release page, checked against its SHA256SUMS.txt.
"""
import hashlib
import io
import os
import platform
import sys
import urllib.request
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
# samples/sdk-version.txt is the one place the samples name the SDK version.
with open(os.path.join(HERE, "..", "..", "sdk-version.txt"), encoding="utf-8") as _f:
    VERSION = _f.read().strip()
BASE = ("https://github.com/akribis-agito/Agito-Releases/releases/download/"
        f"Software/AAMotion-Native/v{VERSION}/")
ARCHIVE = f"agito-aamotion-native-sdk-{VERSION}.zip"


def platform_files():
    """SDK 中本机平台的文件夹，以及要放到 aamotion.py 旁边的文件。"""
    if sys.platform == "win32":
        return "win-x64", ["aamotion.dll", "AACommServerNative.exe"]
    if sys.platform.startswith("linux"):
        return "linux-x64", ["libaamotion.so", "AACommServerNative"]
    if sys.platform == "darwin":
        rid = "osx-arm64" if platform.machine() == "arm64" else "osx-x64"
        return rid, ["libaamotion.dylib", "AACommServerNative"]
    sys.exit(f"no AAMotionNative library for {sys.platform}")


def fetch(url):
    print("downloading", url)
    with urllib.request.urlopen(url, timeout=300) as reply:
        return reply.read()


def main():
    rid, names = platform_files()
    sums = fetch(BASE + "SHA256SUMS.txt").decode("utf-8")
    expected = next((line.split()[0] for line in sums.splitlines()
                     if line.strip().endswith(ARCHIVE)), None)
    if expected is None:
        sys.exit(f"{ARCHIVE} is not listed in SHA256SUMS.txt")
    data = fetch(BASE + ARCHIVE)
    actual = hashlib.sha256(data).hexdigest()
    if actual != expected:
        sys.exit(f"checksum mismatch: expected {expected}, got {actual}; nothing written")
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        for name in names:
            target = os.path.join(HERE, name)
            with open(target, "wb") as out:
                out.write(archive.read(f"{rid}/{name}"))
            if not name.endswith((".dll", ".exe")):
                os.chmod(target, 0o755)   # Linux / macOS：库与守护进程需要可执行权限
            print("wrote", name)   # 不打印路径：重定向的输出可能编码不了中文路径
    print(f"AAMotionNative {VERSION} ({rid}) is ready beside aamotion.py")
    return 0


if __name__ == "__main__":
    sys.exit(main())
