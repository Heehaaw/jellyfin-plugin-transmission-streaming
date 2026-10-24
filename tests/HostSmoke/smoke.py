#!/usr/bin/env python3
"""Exercise only the disposable Jellyfin instance on loopback port 18097.

The fixture imitates Transmission and Radarr. It never connects to the real
services, reads their credentials, or changes the normal Jellyfin instance.
"""

from __future__ import annotations

import base64
import hashlib
import http.server
import json
import math
import os
from pathlib import Path
import secrets
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.parse
import urllib.request


BASE = Path('/var/tmp/jellyfin-transmission-streaming-test')
JELLYFIN = 'http://127.0.0.1:18097'
UPSTREAM = 'http://127.0.0.1:18098'
PLUGIN = '843c341c-ce40-4cc3-8077-53ebd66d83b4'
PIECE_SIZE = 4096
DEADLINE = time.monotonic() + 120


def field(value: dict, name: str, default=None):
    """Jellyfin responses may use either property naming convention."""
    return next((item for key, item in value.items() if key.casefold() == name.casefold()), default)


def require(condition: bool, message: str):
    if not condition:
        raise AssertionError(message)


def wait_for(probe, description: str, seconds: float = 30):
    stop = min(DEADLINE, time.monotonic() + seconds)
    last = None
    while time.monotonic() < stop:
        try:
            result = probe()
            if result:
                return result
        except (urllib.error.URLError, TimeoutError, OSError) as error:
            last = type(error).__name__
        time.sleep(0.25)
    suffix = '' if last is None else f' ({last})'
    raise AssertionError(f'Timed out waiting for {description}{suffix}')


class Jellyfin:
    def __init__(self):
        self.token = None
        self.user_id = None

    def request(self, path: str, body=None, method: str | None = None):
        require(path.startswith('/') and not path.startswith('//'), 'Invalid local test API path')
        remaining = DEADLINE - time.monotonic()
        require(remaining > 0, 'Smoke test exceeded its time budget')
        headers = {'Accept': 'application/json'}
        if self.token:
            headers['Authorization'] = f'MediaBrowser Token="{self.token}"'
        else:
            headers['Authorization'] = 'MediaBrowser Client="Prototype smoke test", Device="Isolated fixture", DeviceId="transmission-prototype-smoke", Version="0.1"'
        data = None if body is None else json.dumps(body).encode()
        if data is not None:
            headers['Content-Type'] = 'application/json'
        request = urllib.request.Request(JELLYFIN + path, data=data, headers=headers, method=method)
        with urllib.request.urlopen(request, timeout=min(15, remaining)) as response:
            content = response.read()
            return json.loads(content) if content else None

    def authenticate(self):
        credentials_path = BASE / 'smoke-credentials.json'
        if credentials_path.exists():
            credentials = json.loads(credentials_path.read_text())
        else:
            credentials = {'Name': 'prototype-smoke-admin', 'Password': secrets.token_urlsafe(32)}
            descriptor = os.open(credentials_path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
            with os.fdopen(descriptor, 'w') as stream:
                json.dump(credentials, stream)
        info = self.request('/System/Info/Public')
        if not field(info, 'StartupWizardCompleted', False):
            self.request('/Startup/User')
            self.request('/Startup/User', credentials, 'POST')
            self.request('/Startup/Configuration', {
                'ServerName': 'Disposable Transmission prototype test', 'UICulture': 'en-US',
                'MetadataCountryCode': 'US', 'PreferredMetadataLanguage': 'en'}, 'POST')
            self.request('/Startup/RemoteAccess', {'EnableRemoteAccess': False}, 'POST')
            self.request('/Startup/Complete', {}, 'POST')
        result = self.request('/Users/AuthenticateByName', {
            'Username': credentials['Name'], 'Pw': credentials['Password']}, 'POST')
        self.token = field(result, 'AccessToken')
        self.user_id = field(field(result, 'User'), 'Id')
        require(bool(self.token and self.user_id), 'Disposable administrator login failed')


class Fixture:
    def __init__(self):
        self.lock = threading.Lock()
        self.run_id = str(time.time_ns())
        self.movie_id = int(self.run_id[-8:]) + 1
        self.title = 'Transmission Prototype Fixture ' + self.run_id
        self.name = self.title + '.mkv'
        self.hash = hashlib.sha1(self.run_id.encode()).hexdigest()
        self.api_key = secrets.token_urlsafe(24)
        self.rpc_password = secrets.token_urlsafe(24)
        self.source = BASE / 'media/incomplete' / self.name
        self.final = BASE / 'media/movies' / self.name
        self.remote_source = '/data/incomplete/' + self.name
        self.remote_final = '/data/movies/' + self.name
        self.expected = b''
        self.finished = False
        self.imported = False
        self.present = True
        self.rpc_calls = 0
        self.handler_errors = 0

    def create_media(self):
        self.source.parent.mkdir(parents=True, exist_ok=True)
        self.final.parent.mkdir(parents=True, exist_ok=True)
        reference = BASE / ('fixture-reference-' + self.run_id + '.mkv')
        subprocess.run([
            '/usr/lib/jellyfin-ffmpeg/ffmpeg', '-hide_banner', '-loglevel', 'error',
            '-f', 'lavfi', '-i', 'testsrc2=size=160x90:rate=10', '-t', '3',
            '-c:v', 'libx264', '-preset', 'ultrafast', '-an', '-y', str(reference)
        ], check=True, timeout=15, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
        self.expected = reference.read_bytes()
        require(len(self.expected) > PIECE_SIZE * 2, 'Video fixture is too small for a missing-piece test')
        # The allocated length resembles a real partially downloaded torrent. Only
        # the first piece has valid bytes; a careless file proxy would leak zeros.
        with self.source.open('wb') as stream:
            stream.write(self.expected[:PIECE_SIZE])
            stream.truncate(len(self.expected))

    def finish(self):
        with self.lock:
            with self.source.open('r+b') as stream:
                stream.seek(PIECE_SIZE)
                stream.write(self.expected[PIECE_SIZE:])
                stream.flush()
                os.fsync(stream.fileno())
            self.finished = True

    def import_file(self):
        with self.lock:
            self.source.rename(self.final)
            self.imported = True

    def torrent(self):
        count = math.ceil(len(self.expected) / PIECE_SIZE)
        bits = bytearray(math.ceil(count / 8))
        for index in range(count if self.finished else 1):
            bits[index // 8] |= 1 << (7 - index % 8)
        completed = len(self.expected) if self.finished else PIECE_SIZE
        return {
            'hash_string': self.hash, 'name': self.title, 'download_dir': '/data/completed',
            'piece_count': count, 'piece_size': PIECE_SIZE,
            'pieces': base64.b64encode(bits).decode(),
            'files': [{'name': self.name, 'length': len(self.expected), 'bytes_completed': completed}],
            'percent_done': completed / len(self.expected), 'status': 6 if self.finished else 4,
            'error_string': '', 'sequential_download': True, 'sequential_download_from_piece': 0}

    def movie(self):
        return {'id': self.movie_id, 'title': self.title, 'year': 2026, 'tmdbId': 0, 'imdbId': ''}

    def history(self):
        if not self.imported:
            return []
        return [{
            'id': 1, 'movieId': self.movie_id, 'downloadId': self.hash,
            'eventType': 'downloadFolderImported', 'date': '2026-10-24T12:00:00Z',
            'data': {'droppedPath': self.remote_source, 'importedPath': self.remote_final,
                     'size': str(len(self.expected)), 'fileId': '1'}}]


class UpstreamHandler(http.server.BaseHTTPRequestHandler):
    fixture: Fixture

    def log_message(self, *_args):
        # Capability URLs and API headers never enter console logs.
        pass

    def reply(self, status: int, value):
        content = json.dumps(value).encode()
        self.send_response(status)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(content)))
        self.end_headers()
        try:
            self.wfile.write(content)
        except (BrokenPipeError, ConnectionResetError):
            pass

    def request_body(self, maximum=128 * 1024):
        """Read the small control request, including .NET's chunked JsonContent."""
        self.connection.settimeout(10)
        transfer = self.headers.get('Transfer-Encoding', '').casefold().strip()
        length = self.headers.get('Content-Length')
        if transfer:
            if transfer != 'chunked' or length is not None:
                raise ValueError('Unsupported fixture transfer encoding')
            body = bytearray()
            trailer_bytes = 0
            while True:
                line = self.rfile.readline(8193)
                if len(line) > 8192 or not line.endswith(b'\r\n'):
                    raise ValueError('Invalid chunk header')
                size_text = line[:-2].split(b';', 1)[0]
                if not size_text or any(byte not in b'0123456789abcdefABCDEF' for byte in size_text):
                    raise ValueError('Invalid chunk size')
                size = int(size_text, 16)
                if size > maximum - len(body):
                    raise ValueError('Fixture control request is too large')
                if size == 0:
                    # Trailers terminate at the empty line; their bytes are bounded
                    # separately so a malformed peer cannot hold a fixture thread.
                    while True:
                        trailer = self.rfile.readline(8193)
                        trailer_bytes += len(trailer)
                        if len(trailer) > 8192 or trailer_bytes > 8192 or not trailer.endswith(b'\r\n'):
                            raise ValueError('Invalid chunk trailer')
                        if trailer == b'\r\n':
                            return bytes(body)
                chunk = self.rfile.read(size)
                if len(chunk) != size or self.rfile.read(2) != b'\r\n':
                    raise ConnectionError('Incomplete fixture request')
                body.extend(chunk)
        if length is None:
            return b''
        if not length.isascii() or not length.isdecimal() or int(length) > maximum:
            raise ValueError('Invalid fixture content length')
        body = self.rfile.read(int(length))
        if len(body) != int(length):
            raise ConnectionError('Incomplete fixture request')
        return body

    def do_POST(self):
        if urllib.parse.urlsplit(self.path).path != '/transmission/rpc':
            return self.reply(404, {})
        expected = 'Basic ' + base64.b64encode(('fixture:' + self.fixture.rpc_password).encode()).decode()
        if self.headers.get('Authorization') != expected:
            return self.reply(401, {})
        if self.headers.get('X-Transmission-Session-Id') != 'prototype-test-session':
            self.send_response(409)
            self.send_header('X-Transmission-Session-Id', 'prototype-test-session')
            self.send_header('Content-Length', '0')
            self.end_headers()
            return
        try:
            request = json.loads(self.request_body())
        except (json.JSONDecodeError, ValueError, ConnectionError, TimeoutError):
            self.fixture.handler_errors += 1
            return self.reply(400, {'error': 'Malformed fixture control request'})
        with self.fixture.lock:
            self.fixture.rpc_calls += 1
            method = request.get('method')
            if method == 'torrent_get':
                result = {'torrents': [self.fixture.torrent()] if self.fixture.present else []}
            elif method == 'session_get':
                result = {'incomplete_dir': '/data/incomplete', 'incomplete_dir_enabled': True,
                          'rename_partial_files': True, 'cache_size_mib': 4, 'version': '4.1.3'}
            elif method == 'torrent_set':
                result = {}
            else:
                self.fixture.handler_errors += 1
                return self.reply(400, {'error': 'Unsupported fixture RPC method'})
        self.reply(200, {'jsonrpc': '2.0', 'id': request.get('id'), 'result': result})

    def do_GET(self):
        if self.headers.get('X-Api-Key') != self.fixture.api_key:
            return self.reply(401, {})
        path = urllib.parse.urlsplit(self.path).path
        with self.fixture.lock:
            if path == '/api/v3/queue':
                rows = [] if self.fixture.imported else [{
                    'id': 1, 'movieId': self.fixture.movie_id,
                    'downloadId': self.fixture.hash, 'movie': self.fixture.movie()}]
                result = {'records': rows, 'totalRecords': len(rows)}
            elif path == '/api/v3/history':
                rows = self.fixture.history()
                result = {'records': rows, 'totalRecords': len(rows)}
            elif path == f'/api/v3/movie/{self.fixture.movie_id}':
                result = self.fixture.movie()
            elif path == '/api/v3/moviefile/1' and self.fixture.imported:
                result = {'id': 1, 'movieId': self.fixture.movie_id,
                          'path': self.fixture.remote_final, 'size': len(self.fixture.expected)}
            else:
                return self.reply(404, {})
        self.reply(200, result)


class FixtureServer(http.server.ThreadingHTTPServer):
    def handle_error(self, request, client_address):
        # Players can abandon probe/range requests. Other fixture errors must stay
        # visible because hiding them would turn a bad harness into a false pass.
        if isinstance(sys.exception(), (BrokenPipeError, ConnectionResetError)):
            return
        super().handle_error(request, client_address)


def check_stream(url: str, expected: bytes, start=0, end=None, method='GET'):
    headers = {}
    if end is not None:
        headers['Range'] = f'bytes={start}-{end}'
    with urllib.request.urlopen(urllib.request.Request(url, method=method, headers=headers), timeout=20) as response:
        if method == 'HEAD':
            require(int(response.headers['Content-Length']) == len(expected), 'HEAD reported the wrong file length')
            return
        actual = response.read()
        wanted = expected if end is None else expected[start:end + 1]
        require(actual == wanted, 'The stream returned missing or incorrect media bytes')
        if end is not None:
            require(response.status == 206, 'A byte range did not produce partial content')


def add_library(api: Jellyfin, projection: Path):
    # Explicit provider allowlists prevent synthetic titles from starting remote
    # metadata/image searches. The deliberately unmatched name disables providers.
    options = {
        'PathInfos': [{'Path': str(projection / 'movies')}],
        'EnableRealtimeMonitor': False, 'SaveLocalMetadata': False,
        'EnableChapterImageExtraction': False, 'EnableTrickplayImageExtraction': False,
        'TypeOptions': [{'Type': 'Movie', 'MetadataFetchers': ['__fixture_disabled__'],
                         'ImageFetchers': ['__fixture_disabled__']}]}
    folders = api.request('/Library/VirtualFolders')
    existing = next((folder for folder in folders if field(folder, 'Name') == 'Prototype Streaming'), None)
    if existing:
        api.request('/Library/VirtualFolders/LibraryOptions', {
            'Id': field(existing, 'ItemId'), 'LibraryOptions': options}, 'POST')
        api.request('/Library/Refresh', {}, 'POST')
        return
    query = urllib.parse.urlencode({'name': 'Prototype Streaming', 'collectionType': 'movies',
                                   'refreshLibrary': 'true', 'paths': str(projection / 'movies')})
    api.request('/Library/VirtualFolders?' + query, {'LibraryOptions': options}, 'POST')


def locate_movie(api: Jellyfin, fixture: Fixture):
    query = urllib.parse.urlencode({'userId': api.user_id, 'Recursive': 'true',
                                   'IncludeItemTypes': 'Movie', 'Fields': 'Path'})
    result = api.request('/Items?' + query)
    return next((item for item in field(result, 'Items', [])
                 if fixture.title in field(item, 'Path', '') and field(item, 'Path', '').endswith('.strm')), None)


def run():
    require(BASE.is_dir() and (BASE / 'config/network.xml').is_file(), 'Disposable test server has not been prepared')
    # The port and directory guards make accidental use against the normal server fail early.
    require(urllib.parse.urlsplit(JELLYFIN).port == 18097, 'Only the isolated test port is permitted')
    fixture = Fixture()
    fixture.create_media()
    handler = type('FixtureHandler', (UpstreamHandler,), {'fixture': fixture})
    server = FixtureServer(('127.0.0.1', 18098), handler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    api = Jellyfin()
    saved = None
    report = []
    try:
        wait_for(lambda: api.request('/System/Info/Public'), 'isolated Jellyfin startup', 20)
        api.authenticate()
        try:
            api.request('/TransmissionStreaming/status')
        except urllib.error.HTTPError as error:
            raise AssertionError(f'Plugin status endpoint returned HTTP {error.code}; verify host service registration before discovery.') from None
        saved = api.request('/Plugins/' + PLUGIN + '/Configuration')
        configuration = dict(saved)
        projection = BASE / 'projection'
        configuration.update({
            'Enabled': True, 'TransmissionUrl': UPSTREAM + '/transmission/rpc',
            'TransmissionUsername': 'fixture', 'TransmissionPassword': fixture.rpc_password,
            'RadarrUrl': UPSTREAM, 'RadarrApiKey': fixture.api_key, 'SonarrUrl': '', 'SonarrApiKey': '',
            'RemoteMediaRoot': '/data', 'LocalMediaRoot': str(BASE / 'media'),
            'ProjectionRoot': str(projection), 'JellyfinPublicUrl': JELLYFIN,
            'PollSeconds': 2, 'PieceWaitSeconds': 15, 'PrioritizePlayback': True,
            'ShowDownloadStatus': True})
        api.request('/Plugins/' + PLUGIN + '/Configuration', configuration, 'POST')

        def projected_link():
            return next((path for path in projection.rglob('*.strm') if fixture.title in str(path)), None)

        try:
            link_file = wait_for(projected_link, 'automatic torrent discovery', 25)
        except AssertionError:
            diagnostics = {'fixtureRpcCalls': fixture.rpc_calls, 'fixtureHandlerErrors': fixture.handler_errors}
            try:
                status = api.request('/TransmissionStreaming/status')
                diagnostics.update({
                    'pluginEnabled': field(status, 'Enabled'),
                    'lastSuccessfulPoll': field(status, 'LastSuccessfulPoll'),
                    'lastFailure': field(status, 'LastFailure'),
                    'entryCount': len(field(status, 'Entries', []))})
            except urllib.error.HTTPError as error:
                diagnostics['statusHttpCode'] = error.code
            print(json.dumps({'discoveryDiagnostics': diagnostics}, indent=4))
            raise
        url = link_file.read_text().strip()
        require(url.startswith(JELLYFIN + '/TransmissionStreaming/streams/'), 'Unexpected fixture playback origin')
        report.append('automatic discovery')
        try:
            urllib.request.urlopen(url.split('?', 1)[0], timeout=5)
            raise AssertionError('A stream without its capability key was accepted')
        except urllib.error.HTTPError as error:
            require(error.code == 404, 'Missing capability key did not return 404')
        check_stream(url, fixture.expected, method='HEAD')
        check_stream(url, fixture.expected, start=0, end=PIECE_SIZE - 1)
        report.extend(['capability check', 'HEAD length', 'available byte range'])

        finish = threading.Timer(1.5, fixture.finish)
        finish.start()
        started = time.monotonic()
        check_stream(url, fixture.expected)
        finish.join(timeout=5)
        require(time.monotonic() - started >= 1.0, 'Full playback did not wait for the missing pieces')
        report.append('missing-piece wait without sparse zeros')

        add_library(api, projection)
        item = wait_for(lambda: locate_movie(api, fixture), 'Jellyfin indexing of the projected movie', 25)
        item_id = field(item, 'Id')
        viewer = api.request('/Users/New', {'Name': 'prototype-viewer-' + fixture.run_id,
                                          'Password': secrets.token_urlsafe(24)}, 'POST')
        users = [(api.user_id, {'Played': True, 'PlaybackPositionTicks': 0, 'PlayCount': 2, 'IsFavorite': True}),
                 (field(viewer, 'Id'), {'Played': False, 'PlaybackPositionTicks': 5000000, 'PlayCount': 1, 'IsFavorite': False})]
        before = {}
        for user, state in users:
            path = '/UserItems/' + item_id + '/UserData?' + urllib.parse.urlencode({'userId': user})
            api.request(path, state, 'POST')
            before[user] = api.request(path)

        fixture.import_file()
        def imported():
            result = api.request('/TransmissionStreaming/status')
            return next((entry for entry in field(result, 'Entries', [])
                         if field(entry, 'Title') == fixture.title and field(entry, 'Status') == 'Complete'), None)

        wait_for(imported, 'exact arr import handoff', 20)
        fixture.present = False
        check_stream(url, fixture.expected)
        require(link_file.read_text().strip() == url, 'Completion changed the permanent playback URL')
        after_item = locate_movie(api, fixture)
        require(field(after_item or {}, 'Id') == item_id, 'Completion replaced the Jellyfin item identity')
        for user, _state in users:
            path = '/UserItems/' + item_id + '/UserData?' + urllib.parse.urlencode({'userId': user})
            after = api.request(path)
            for key in ['Played', 'PlaybackPositionTicks', 'PlayCount', 'IsFavorite']:
                require(field(after, key) == field(before[user], key), 'Completion changed a user watch-state field')
        require(fixture.handler_errors == 0, 'The fixture received an unsupported RPC call')
        report.extend(['completed file after torrent removal', 'stable Jellyfin identity', 'two-user watch-state retention'])
        print(json.dumps({'passed': report, 'fixtureBytes': len(fixture.expected)}, indent=4))
    finally:
        if saved is not None and api.token:
            try:
                saved['Enabled'] = False
                api.request('/Plugins/' + PLUGIN + '/Configuration', saved, 'POST')
            except (OSError, urllib.error.URLError, AssertionError):
                pass
        server.shutdown()
        server.server_close()


if __name__ == '__main__':
    try:
        run()
    except Exception as error:
        # HTTPError.__str__ contains the request URL. Report no token-bearing URLs.
        print(json.dumps({'failed': type(error).__name__, 'reason': str(error) if isinstance(error, AssertionError) else 'Check isolated server logs.'}))
        raise SystemExit(1)
