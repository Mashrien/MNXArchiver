"""Exercise the NativeAOT exports with independently generated Ren'Py RPA fixtures."""

import codecs
import concurrent.futures
import ctypes
import json
import os
import pathlib
import pickle
import pickletools
import struct
import sys
import tempfile
import threading
import unittest
import zlib


CANCEL = ctypes.CFUNCTYPE(ctypes.c_int, ctypes.c_void_p)
KEY = 0xF2345678
HEADER_LENGTH = 34
NEGOTIATION = dict(hostVersion="2.0.0", minPluginVersion="1.0.0", maxPluginVersion="1.0.999")


def make_archive(path, data, index, protocol=5, *, encoded_index=None):
    header = f"RPA-3.0 {HEADER_LENGTH + len(data):016x} {KEY:08x}\n".encode("ascii")
    payload = encoded_index if encoded_index is not None else pickle.dumps(index, protocol)
    path.write_bytes(header + data + zlib.compress(payload))


def segment(offset, length, *prefix):
    return (offset ^ KEY, length ^ KEY, *prefix)


def read_archive(path):
    data = path.read_bytes()
    magic, index_offset, key = data.split(b"\n", 1)[0].split()
    assert magic == b"RPA-3.0"
    key = int(key, 16)
    payload = zlib.decompress(data[int(index_offset, 16):])
    protocol_opcode, protocol, _ = next(pickletools.genops(payload))
    assert protocol_opcode.name == "PROTO"
    index = pickle.loads(payload)
    files = {}
    for name, segments in index.items():
        contents = bytearray()
        for offset, length, *prefix in segments:
            offset ^= key
            length ^= key
            if prefix:
                contents.extend(prefix[0])
            contents.extend(data[offset:offset + length])
        files[name] = bytes(contents)
    return key, protocol, files


class RpaModuleTests(unittest.TestCase):
    def setUp(self):
        directory = tempfile.TemporaryDirectory()
        self.addCleanup(directory.cleanup)
        self.root = pathlib.Path(directory.name)
        self.archive = self.root / "game.rpa"

    def request(self, operation, *, callback=None, **fields):
        request = dict(NEGOTIATION, operation=operation, archivePath=str(self.archive))
        request.update(fields)
        return self.send(json.dumps(request).encode("utf-8"), callback)

    def send(self, request, callback=None):
        response = ctypes.c_void_p()
        length = ctypes.c_int()
        cancel = CANCEL(callback) if callback is not None else None
        cancel_pointer = ctypes.cast(cancel, ctypes.c_void_p)
        status = module.mnx_plugin_request(request, len(request), cancel_pointer, None,
                                           ctypes.byref(response), ctypes.byref(length))
        try:
            self.assertEqual(status, 0)
            self.assertTrue(response.value)
            self.assertGreater(length.value, 0)
            return json.loads(ctypes.string_at(response, length.value))
        finally:
            if response.value:
                module.mnx_plugin_release(response)

    def read_entry(self, entry_path, entry_size, capacity, *, callback=None):
        request = dict(NEGOTIATION, operation="read", archivePath=str(self.archive),
                       entryPath=entry_path, entrySize=entry_size)
        payload = json.dumps(request).encode("utf-8")
        destination = ctypes.create_string_buffer(capacity)
        response = ctypes.c_void_p()
        length = ctypes.c_int()
        cancel = CANCEL(callback) if callback is not None else None
        status = module.mnx_plugin_read_entry(
            payload, len(payload), ctypes.cast(cancel, ctypes.c_void_p), None,
            destination if capacity else None, capacity, ctypes.byref(response), ctypes.byref(length))
        try:
            self.assertEqual(status, 0)
            self.assertTrue(response.value)
            return json.loads(ctypes.string_at(response, length.value)), destination.raw
        finally:
            if response.value:
                module.mnx_plugin_release(response)

    def assert_success(self, response):
        self.assertIsNone(response["error"])
        self.assertFalse(response["cancelled"])
        return response

    def write_source(self, name, data):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        return str(path)

    def test_descriptor_and_mutual_compatibility(self):
        self.assertEqual(module.mnx_plugin_abi_version(), 1)
        description = self.assert_success(self.send(b'{"operation":"describe"}'))["format"]
        self.assertEqual(description, {
            "id": "renpy.rpa", "name": "Ren'Py RPA", "version": "1.0.2",
            "minHostVersion": "2.0.0", "maxHostVersion": "2.1.999",
            "extensions": [".rpa"], "versions": [{
                "version": "3.0", "magicHex": "5250412D332E3020", "actions": 63,
            }], "currentVersion": "3.0",
        })
        for host_version in ["2.0.0", "2.0.999", "2.1.0", "2.1.999"]:
            self.assert_success(self.request("negotiate", hostVersion=host_version))
        for field, value in [("hostVersion", "2.2.0"), ("minPluginVersion", "1.0.3"),
                             ("maxPluginVersion", "0.9.999"), ("hostVersion", None)]:
            for operation in ["negotiate", "open", "create", "add", "replace", "delete", "extract"]:
                response = self.request(operation, **{field: value})
                self.assertIn("range", response["error"].lower())
        self.assertFalse(self.archive.exists())
        self.assertTrue(self.send(b"null")["error"])
        self.assertTrue(self.send(b"{")["error"])

    def test_python_protocols_with_prefixes_and_multiple_segments(self):
        for protocol in [2, 3, 4, 5]:
            with self.subTest(protocol=protocol):
                data = b"ABCxyz"
                index = {
                    "images/café.txt": [segment(34, 3, b"\x00\x80\xff"), segment(37, 3, b"!"),
                                        segment(34, 0, b""), segment(34, 0, None)],
                    "plain.txt": [segment(34, 1), segment(38, 2)],
                }
                make_archive(self.archive, data, index, protocol)
                entries = self.assert_success(self.request("open"))["entries"]
                self.assertEqual({entry["path"]: entry["size"] for entry in entries},
                                 {"images/café.txt": 10, "plain.txt": 3})
                destination = self.root / f"output-{protocol}" / "one.txt"
                self.assert_success(self.request("extract", outputs=[{
                    "entryPath": "images/café.txt", "destination": str(destination),
                }]))
                self.assertEqual(destination.read_bytes(), b"\x00\x80\xffABC!xyz")
                expected = destination.read_bytes()
                for capacity in [0, 2, 6, 8, len(expected)]:
                    response, preview = self.read_entry("images/café.txt", len(expected), capacity)
                    self.assertEqual(self.assert_success(response)["bytesRead"], capacity)
                    self.assertEqual(preview, expected[:capacity])
                changed, _ = self.read_entry("images/café.txt", 9, 2)
                self.assertIn("size changed", changed["error"])
                cancelled, _ = self.read_entry("images/café.txt", len(expected), 2, callback=lambda _: 1)
                self.assertTrue(cancelled["cancelled"])


    def test_python_two_byte_strings(self):
        # Protocol 2 opcodes emitted for Python 2 str filenames and byte prefixes.
        filename = "images/café.txt".encode("utf-8")
        offset, length = segment(34, 3)
        encoded = (b"\x80\x02}q\x00U" + bytes([len(filename)]) + filename +
                   b"q\x01]q\x02\x8a\x05" + struct.pack("<I", offset) + b"\x00" +
                   b"\x8a\x05" + struct.pack("<I", length) + b"\x00U\x02\x80\xff\x87as.")
        make_archive(self.archive, b"ABC", {}, encoded_index=encoded)
        entries = self.assert_success(self.request("open"))["entries"]
        self.assertEqual(entries, [{"path": "images/café.txt", "size": 5}])
        destination = self.root / "bytes.txt"
        self.assert_success(self.request("extract", outputs=[{
            "entryPath": "images/café.txt", "destination": str(destination),
        }]))
        self.assertEqual(destination.read_bytes(), b"\x80\xffABC")

    def test_create_add_and_replace_cross_read_with_python(self):
        first = self.write_source("first.txt", b"First")
        nested = self.write_source("images/sub/é.txt", b"second")
        self.assert_success(self.request("create", sources=[first, str(self.root / "images")]))
        _, protocol, files = read_archive(self.archive)
        self.assertEqual(protocol, 2)
        self.assertEqual(files, {"first.txt": b"First", "images/sub/é.txt": b"second"})

        original = self.archive.read_bytes()
        self.assertTrue(self.request("create", sources=[first])["error"])
        self.assertTrue(self.request("add", sources=[first])["error"])
        self.assertEqual(self.archive.read_bytes(), original)

        self.assert_success(self.request("add", folder="extra/more", sources=[nested]))
        replacement = self.write_source("replacement.bin", b"new contents")
        entries = self.assert_success(self.request(
            "replace", entryPath="images/sub/é.txt", sources=[replacement]))["entries"]
        self.assertEqual(len(entries), 3)
        _, protocol, files = read_archive(self.archive)
        self.assertEqual(protocol, 2)
        self.assertEqual(files, {"first.txt": b"First", "images/sub/é.txt": b"new contents",
                                 "extra/more/é.txt": b"second"})

    def test_edit_preserves_prefix_data_key_and_pickle_protocol(self):
        source = self.write_source("second.txt", b"new file")
        replacement = self.write_source("new.bin", b"replacement")
        for original_protocol in [2, 3, 4, 5]:
            with self.subTest(protocol=original_protocol):
                make_archive(self.archive, b"ABCxyz",
                             {"one.txt": [segment(34, 3, b"\x00\xff"), segment(37, 3, b"!")]},
                             original_protocol)
                self.assert_success(self.request("add", sources=[source]))
                key, protocol, files = read_archive(self.archive)
                self.assertEqual((key, protocol), (KEY, original_protocol))
                self.assertEqual(files, {"one.txt": b"\x00\xffABC!xyz", "second.txt": b"new file"})
                self.assert_success(self.request("replace", entryPath="second.txt", sources=[replacement]))
                key, protocol, files = read_archive(self.archive)
                self.assertEqual((key, protocol), (KEY, original_protocol))
                self.assertEqual(files, {"one.txt": b"\x00\xffABC!xyz", "second.txt": b"replacement"})

    def test_create_replaces_an_existing_file_only_when_authorized(self):
        self.archive.write_bytes(b"existing contents")
        source = self.write_source("replacement.txt", b"new contents")
        self.assertTrue(self.request("create", sources=[source])["error"])
        self.assertEqual(self.archive.read_bytes(), b"existing contents")
        self.assert_success(self.request("create", overwrite=True, sources=[source]))
        _, protocol, files = read_archive(self.archive)
        self.assertEqual(protocol, 2)
        self.assertEqual(files, {"replacement.txt": b"new contents"})

    def test_delete_selected_files_and_create_a_valid_empty_index(self):
        index = {
            "nested/first.txt": [segment(34, 1)],
            "nested/second.txt": [segment(35, 1)],
            "third.txt": [segment(36, 1, b"!")],
        }
        for original_protocol in [2, 3, 4, 5]:
            with self.subTest(protocol=original_protocol):
                make_archive(self.archive, b"ABC", index, original_protocol)
                result = self.assert_success(self.request(
                    "delete", entryPaths=["nested/first.txt", "nested/second.txt"]))
                self.assertEqual(result["entries"], [{"path": "third.txt", "size": 2}])
                self.assertEqual(read_archive(self.archive), (KEY, original_protocol, {"third.txt": b"!C"}))

                result = self.assert_success(self.request("delete", entryPaths=["third.txt"]))
                self.assertEqual(result["entries"], [])
                self.assertEqual(read_archive(self.archive), (KEY, original_protocol, {}))
                self.assertEqual(self.assert_success(self.request("open"))["entries"], [])

    def test_invalid_delete_leaves_the_original_archive_intact(self):
        make_archive(self.archive, b"AB", {"nested/first.txt": [segment(34, 1)],
                                           "second.txt": [segment(35, 1)]})
        original = self.archive.read_bytes()
        for selection in [[], ["nested"], ["nested/first.txt", "missing.txt"],
                          ["nested/first.txt", "nested/first.txt"], ["../outside"], [None]]:
            with self.subTest(selection=selection):
                self.assertTrue(self.request("delete", entryPaths=selection)["error"])
                self.assertEqual(self.archive.read_bytes(), original)

    def test_malformed_index_is_rejected(self):
        for path in ["../outside", "images/../../outside", "/absolute", "C:/root", "a\\b", "a//b"]:
            with self.subTest(path=path):
                make_archive(self.archive, b"x", {path: [segment(34, 1)]})
                self.assertTrue(self.request("open")["error"])
        for index in [
            {"a": [segment(34, 1)], "a/b": [segment(34, 1)]},
            {"a": [segment(2, 1)]},
            {"a": [segment(34, 2)]},
            {"a": [segment(34, -1)]},
            {"a": []},
        ]:
            with self.subTest(index=index):
                make_archive(self.archive, b"x", index)
                self.assertTrue(self.request("open")["error"])
        marker = self.root / "pickle-executed"
        payload = b"\x80\x02cposix\nsystem\nX" + struct.pack("<I", len(f"touch {marker}"))
        payload += f"touch {marker}".encode() + b"\x85R."
        make_archive(self.archive, b"", {}, encoded_index=payload)
        self.assertTrue(self.request("open")["error"])
        self.assertFalse(marker.exists())

    def test_truncated_zlib_index_is_rejected(self):
        make_archive(self.archive, b"ABC", {"one.txt": [segment(34, 3)]})
        complete = self.archive.read_bytes()
        self.archive.write_bytes(complete[:-1])
        self.assertTrue(self.request("open")["error"])

    def test_legacy_prefix_reducers_share_a_conversion_budget(self):
        class BytePrefix:
            def __init__(self, arguments):
                self.arguments = arguments

            def __reduce__(self):
                return codecs.encode, self.arguments

        arguments = ("p" * (16 * 1024 * 1024), "latin1")
        segments = [segment(34, 0, BytePrefix(arguments)) for _ in range(5)]
        make_archive(self.archive, b"", {"a.txt": segments}, protocol=2)
        self.assertLess(self.archive.stat().st_size, 20 * 1024)
        response = self.request("open")
        self.assertIn("prefix conversion limit", response["error"])

    def test_path_depth_and_components_match_the_host_tree_limits(self):
        source = self.write_source("a/" * 128 + "file.txt", b"x")
        self.assertTrue(pathlib.Path(source).exists())
        response = self.request("create", sources=[str(self.root / "a")])
        self.assertTrue(response["error"])
        self.assertFalse(self.archive.exists())

        make_archive(self.archive, b"x", {"a" * 256: [segment(34, 1)]})
        self.assertTrue(self.request("open")["error"])

    def test_tree_limit_includes_synthetic_directories(self):
        index = {f"{number}/" + "a/" * 99 + "file.txt": [segment(34, 1)] for number in range(2500)}
        make_archive(self.archive, b"x", index)
        self.assertIn("tree", self.request("open")["error"] or "")

    @unittest.skipUnless(sys.platform.startswith("linux"), "requires a Linux named pipe")
    def test_named_pipe_is_rejected_as_an_archive_or_source(self):
        os.mkfifo(self.archive)
        self.assertTrue(self.request("open")["error"])
        source = self.archive
        self.archive = self.root / "new.rpa"
        self.assertTrue(self.request("create", sources=[str(source)])["error"])
        self.assertFalse(self.archive.exists())

    def test_extraction_overwrite_and_symlink_safety(self):
        make_archive(self.archive, b"ABC", {"one.txt": [segment(34, 3)]})
        original = self.archive.read_bytes()
        target = self.root / "existing.txt"
        target.write_bytes(b"keep")
        self.assertTrue(self.request("extract", outputs=[{
            "entryPath": "one.txt", "destination": str(target),
        }])["error"])
        self.assertEqual(target.read_bytes(), b"keep")
        self.assert_success(self.request("extract", overwrite=True, outputs=[{
            "entryPath": "one.txt", "destination": str(target),
        }]))
        self.assertEqual(target.read_bytes(), b"ABC")
        self.assertTrue(self.request("extract", overwrite=True, outputs=[{
            "entryPath": "one.txt", "destination": str(self.archive),
        }])["error"])
        self.assertEqual(self.archive.read_bytes(), original)

        real = self.root / "real"
        real.mkdir()
        link = self.root / "link"
        link.symlink_to(real, target_is_directory=True)
        self.assertTrue(self.request("extract", outputs=[{
            "entryPath": "one.txt", "destination": str(link / "file.txt"),
        }])["error"])
        self.assertFalse((real / "file.txt").exists())
        linked_source = self.root / "source.txt"
        linked_source.symlink_to(target)
        self.assertTrue(self.request("add", sources=[str(linked_source)])["error"])
        self.assertEqual(self.archive.read_bytes(), original)

    @unittest.skipUnless(sys.platform.startswith("linux"), "checks Linux file identities")
    def test_extraction_cannot_overwrite_an_alias_of_its_archive(self):
        make_archive(self.archive, b"ABC", {"one.txt": [segment(34, 3)]})
        original = self.archive.read_bytes()
        alias = self.root / "alias.rpa"
        os.link(self.archive, alias)
        response = self.request("extract", overwrite=True, outputs=[{
            "entryPath": "one.txt", "destination": str(alias),
        }])
        self.assertTrue(response["error"])
        self.assertEqual(alias.read_bytes(), original)
        self.assertEqual(self.archive.read_bytes(), original)

    def test_cancellation_leaves_archive_and_destination_untouched(self):
        source = self.write_source("large.bin", b"x" * (4 * 1024 * 1024))
        candidate = self.write_source("candidate.bin", b"keep")
        self.assert_success(self.request("create", sources=[source, candidate]))
        original = self.archive.read_bytes()
        second = self.write_source("second.bin", b"y" * (4 * 1024 * 1024))
        target = self.root / "output.bin"
        target.write_bytes(b"keep")

        def cancel_during_copy(state):
            return int(any(path.stat().st_size >= 256 * 1024 for path in self.root.glob(".mnx-rpa-*.tmp")))

        for operation, fields in [
            ("add", dict(sources=[second])),
            ("delete", dict(entryPaths=["candidate.bin"])),
            ("create", dict(overwrite=True, sources=[second])),
            ("extract", dict(overwrite=True, outputs=[
                {"entryPath": "large.bin", "destination": str(target)},
            ])),
        ]:
            response = self.request(operation, callback=cancel_during_copy, **fields)
            self.assertTrue(response["cancelled"])
            self.assertEqual(self.archive.read_bytes(), original)
            self.assertEqual(target.read_bytes(), b"keep")
            self.assertEqual({path.name for path in self.root.iterdir()},
                             {"game.rpa", "large.bin", "candidate.bin", "second.bin", "output.bin"})

    def test_large_framed_pickle(self):
        index = {f"folder/asset-{number:04d}.txt": [segment(34, 1)] for number in range(3500)}
        make_archive(self.archive, b"x", index)
        entries = self.assert_success(self.request("open"))["entries"]
        self.assertEqual(len(entries), 3500)
        self.assertEqual(entries[-1], {"path": "folder/asset-3499.txt", "size": 1})
        source = self.write_source("last.bin", b"last")
        self.assert_success(self.request("add", sources=[source]))
        _, protocol, files = read_archive(self.archive)
        self.assertEqual((protocol, len(files), files["last.bin"]), (5, 3501, b"last"))
        entries = self.assert_success(self.request("open"))["entries"]
        self.assertEqual(entries[-1], {"path": "last.bin", "size": 4})

    def test_concurrent_edits_both_keep_the_other_update(self):
        make_archive(self.archive, b"x", {"original.txt": [segment(34, 1)]})
        first_source = self.write_source("first.bin", b"first")
        second_source = self.write_source("second.bin", b"second")
        staging = threading.Event()
        release = threading.Event()
        second_arrived = threading.Event()

        def hold_first_edit(state):
            if not staging.is_set() and any(self.root.glob(".mnx-rpa-*.tmp")):
                staging.set()
                return int(not release.wait(5))
            return 0

        def observe_second_edit(state):
            second_arrived.set()
            return 0

        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
            first = executor.submit(self.request, "add", sources=[first_source], callback=hold_first_edit)
            try:
                self.assertTrue(staging.wait(5))
                second = executor.submit(self.request, "add", sources=[second_source], callback=observe_second_edit)
                self.assertTrue(second_arrived.wait(5))
            finally:
                release.set()
            self.assert_success(first.result(timeout=5))
            self.assert_success(second.result(timeout=5))

        _, _, files = read_archive(self.archive)
        self.assertEqual(files, {"original.txt": b"x", "first.bin": b"first", "second.bin": b"second"})


if __name__ == "__main__":
    library = pathlib.Path(sys.argv.pop(1)).resolve()
    module = ctypes.CDLL(str(library)) if library.is_file() else None
    if module is None:
        raise SystemExit(f"Native RPA module is missing: {library}")
    module.mnx_plugin_abi_version.restype = ctypes.c_uint
    module.mnx_plugin_request.argtypes = [
        ctypes.c_void_p, ctypes.c_int, ctypes.c_void_p, ctypes.c_void_p,
        ctypes.POINTER(ctypes.c_void_p), ctypes.POINTER(ctypes.c_int),
    ]
    module.mnx_plugin_request.restype = ctypes.c_int
    module.mnx_plugin_read_entry.argtypes = [
        ctypes.c_void_p, ctypes.c_int, ctypes.c_void_p, ctypes.c_void_p,
        ctypes.c_void_p, ctypes.c_int,
        ctypes.POINTER(ctypes.c_void_p), ctypes.POINTER(ctypes.c_int),
    ]
    module.mnx_plugin_read_entry.restype = ctypes.c_int
    module.mnx_plugin_release.argtypes = [ctypes.c_void_p]
    module.mnx_plugin_release.restype = None
    unittest.main()
