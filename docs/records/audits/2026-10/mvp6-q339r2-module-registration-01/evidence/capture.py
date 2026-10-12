# Stand-in for Platform's register-manifest endpoint. Records header NAMES, the internal key as length + sha256/8 only.
import http.server, json, hashlib, sys, datetime
OUT=sys.argv[2]
class H(http.server.BaseHTTPRequestHandler):
    def do_POST(self):
        if self.headers.get("Transfer-Encoding","").lower()=="chunked":
            body=b""
            while True:
                n=int(self.rfile.readline().strip(),16)
                if n==0: self.rfile.readline(); break
                body+=self.rfile.read(n); self.rfile.readline()
        else:
            body=self.rfile.read(int(self.headers.get("Content-Length",0) or 0))
        k=self.headers.get("X-Internal-Api-Key")
        try: code=json.loads(body).get("moduleCode") or json.loads(body).get("ModuleCode")
        except Exception: code="<unparsed>"
        rec={"at":datetime.datetime.utcnow().strftime("%H:%M:%S"),"method":"POST","path":self.path,
             "header_names":sorted(self.headers.keys()),
             "X-Internal-Api-Key":("<redacted len=%d sha8=%s>"%(len(k),hashlib.sha256(k.encode()).hexdigest()[:8])) if k is not None else "<absent>",
             "moduleCode":code,"pages":(lambda j:[x.get("pageCode") or x.get("PageCode") for x in (j.get("pages") or j.get("Pages") or [])])(json.loads(body)) if body else [],"body_bytes":len(body)}
        open(OUT,"a").write(json.dumps(rec)+"\n")
        self.send_response(200); self.send_header("Content-Type","application/json"); self.end_headers(); self.wfile.write(b"{}")
    def log_message(self,*a): pass
http.server.HTTPServer(("127.0.0.1",int(sys.argv[1])),H).serve_forever()
