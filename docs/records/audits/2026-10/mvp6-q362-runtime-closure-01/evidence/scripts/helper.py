# local helper on 127.0.0.1:5199 — serves one actor's password to the Web origin only (CORS), and receives page HTML/PNG for the record.
import http.server, os, sys, json, base64
E = os.path.dirname(os.path.abspath(__file__)); ORIGIN = "http://localhost:5001"
class H(http.server.BaseHTTPRequestHandler):
    def _cors(self):
        self.send_header("Access-Control-Allow-Origin", ORIGIN); self.send_header("Access-Control-Allow-Headers", "Content-Type"); self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
    def do_OPTIONS(self): self.send_response(204); self._cors(); self.end_headers()
    def do_GET(self):
        label = self.path.strip("/").split("/")[-1]
        if self.path.startswith("/pw/") and label in ("admin", "nokey") and self.headers.get("Origin") == ORIGIN:
            v = open(f"{E}/run/{label}.pw").read().encode(); self.send_response(200); self._cors(); self.end_headers(); self.wfile.write(v)
        else: self.send_response(404); self._cors(); self.end_headers()
    def do_POST(self):
        n = int(self.headers.get("Content-Length", 0)); body = self.rfile.read(n); name = os.path.basename(self.path.strip("/"))
        if self.path.startswith("/html/"): open(f"{E}/html/{name}", "wb").write(body)
        elif self.path.startswith("/text/"): open(f"{E}/results/{name}", "wb").write(body)
        self.send_response(204); self._cors(); self.end_headers()
    def log_message(self, fmt, *a): open(f"{E}/log/helper.log", "a").write("%s %s\n" % (self.command, self.path.split('?')[0]))
http.server.HTTPServer(("127.0.0.1", 5199), H).serve_forever()
