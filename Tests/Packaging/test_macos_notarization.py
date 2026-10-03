"""Exercise release fail-closed boundaries without credentials or Apple uploads."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
SHIM = r'''#!/usr/bin/env python3
import json, os, pathlib, sys
name = pathlib.Path(sys.argv[0]).name
args = sys.argv[1:]
with open(os.environ['TRACE'], 'a') as trace:
    trace.write(name + ' ' + ' '.join(args) + '\n')
if name == 'codesign':
    if '--display' in args:
        # Ensure the authority filter reads the whole stream under pipefail.
        print('Authority=Developer ID Application: Test Team')
        print('Details=' + 'x' * 100000)
elif name == 'xcrun':
    if args[:2] == ['notarytool', 'submit']:
        print(json.dumps({'id': 'fake-submission'}))
    elif args[:2] == ['notarytool', 'wait']:
        print(json.dumps({'status': os.environ['NOTARY_STATUS']}))
    elif args[:2] == ['notarytool', 'log']:
        pathlib.Path(args[-1]).write_text(json.dumps({'status': os.environ['NOTARY_STATUS']}))
    elif args[0] != 'stapler':
        sys.exit(9)
elif name in ('spctl', 'syspolicy_check'):
    sys.exit(int(os.environ['GATEKEEPER_EXIT']))
elif name == 'ditto':
    pathlib.Path(args[-1]).write_bytes(b'fake upload zip')
else:
    sys.exit(9)
'''


class MacNotarizationTests(unittest.TestCase):
    def run_pipeline(self, status, gatekeeper_exit, app=False):
        with tempfile.TemporaryDirectory(prefix="Unfold-notary-boundary-") as temporary:
            root = Path(temporary)
            tools = root / "tools"
            tools.mkdir()
            for name in ("codesign", "xcrun", "spctl", "syspolicy_check", "ditto"):
                path = tools / name
                path.write_text(SHIM)
                path.chmod(0o755)
            target = root / ("Test.app" if app else "Test.dmg")
            if app:
                target.mkdir()
            else:
                target.write_bytes(b"fake disk image")
            trace = root / "trace.txt"
            env = dict(os.environ, PATH=str(tools) + os.pathsep + os.environ["PATH"],
                       TRACE=str(trace), NOTARY_STATUS=status, GATEKEEPER_EXIT=str(gatekeeper_exit),
                       UNFOLD_NOTARY_LOG_DIR=str(root / "logs"))
            result = subprocess.run(["bash", str(ROOT / "Scripts/notarize-macos.sh"),
                                     str(target), "fake-profile"], env=env, capture_output=True, text=True)
            events = trace.read_text()
            logs = list((root / "logs").rglob("apple-log.json"))
            self.assertEqual(len(logs), 1, "Apple result must be recorded before success or rejection")
            self.assertEqual(json.loads(logs[0].read_text())["status"], status)
            return result, events

    def test_apple_rejection_never_staples_or_assesses(self):
        result, events = self.run_pipeline("Invalid", 0)
        self.assertNotEqual(result.returncode, 0)
        self.assertNotIn("xcrun stapler", events)
        self.assertNotIn("spctl", events)
        self.assertNotIn("Accepted and stapled", result.stdout)

    def test_gatekeeper_rejection_still_blocks_an_apple_accepted_build(self):
        result, events = self.run_pipeline("Accepted", 3)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("xcrun stapler validate", events)
        self.assertIn("spctl --assess", events)
        self.assertNotIn("Accepted and stapled", result.stdout)

    def test_success_requires_both_ticket_and_gatekeeper_checks(self):
        result, events = self.run_pipeline("Accepted", 0)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertLess(events.index("xcrun stapler staple"), events.index("xcrun stapler validate"))
        self.assertLess(events.index("xcrun stapler validate"), events.index("spctl --assess"))
        self.assertIn("Accepted and stapled", result.stdout)

    def test_app_uses_modern_system_policy_check(self):
        result, events = self.run_pipeline("Accepted", 0, app=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("syspolicy_check distribution", events)
        self.assertNotIn("spctl --assess", events)

    def test_app_policy_rejection_blocks_release(self):
        result, events = self.run_pipeline("Accepted", 1, app=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("syspolicy_check distribution", events)
        self.assertNotIn("Accepted and stapled", result.stdout)


if __name__ == "__main__":
    unittest.main()
