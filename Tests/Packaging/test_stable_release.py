import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("updates", Path(__file__).resolve().parents[2] / "Scripts/package-updates.py")
updates = importlib.util.module_from_spec(spec)
spec.loader.exec_module(updates)


class StableReleaseTests(unittest.TestCase):
    def test_beta_feed_preserves_stable_bytes_version_and_hash_for_each_runtime(self):
        for runtime in ("win-x64", "osx-arm64", "osx-x64"):
            with self.subTest(runtime=runtime), tempfile.TemporaryDirectory() as scratch:
                output = Path(scratch)
                payload = b"stable package, including its signature"
                name = f"{updates.PACKAGE_ID}-1.2.0-{runtime}-stable-full.nupkg"
                (output / name).write_bytes(payload)
                source = {"Assets": [{"PackageId": updates.PACKAGE_ID, "Version": "1.2.0", "Type": "Full",
                    "FileName": name, "Size": len(payload), "SHA256": hashlib.sha256(payload).hexdigest()}]}
                updates.publish_beta_transition(output, source, runtime, "1.2.0")
                asset, = json.loads((output / f"releases.{runtime}-beta.json").read_text())["Assets"]
                self.assertEqual(asset["Version"], "1.2.0")
                self.assertEqual(asset["FileName"], name.replace("-stable-full", "-beta-full"))
                self.assertEqual((output / asset["FileName"]).read_bytes(), payload)
                self.assertEqual(asset["SHA256"], hashlib.sha256(payload).hexdigest())
                self.assertEqual(source["Assets"][0]["FileName"], name)

    def test_beta_release_does_not_overwrite_its_feed(self):
        with tempfile.TemporaryDirectory() as scratch:
            output = Path(scratch)
            updates.publish_beta_transition(output, {"Assets": []}, "win-x64", "1.2.0-beta")
            self.assertEqual(list(output.iterdir()), [])

    def test_transition_rejects_a_mismatched_package(self):
        with tempfile.TemporaryDirectory() as scratch:
            with self.assertRaises(SystemExit):
                updates.publish_beta_transition(Path(scratch), {"Assets": []}, "win-x64", "1.2.0")


if __name__ == "__main__":
    unittest.main()
