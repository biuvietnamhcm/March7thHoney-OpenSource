"""Run with mitmdump --set server=false -s tests/ProxyRegression/check_addon.py."""
import importlib.util
import pathlib
from types import SimpleNamespace

from mitmproxy import ctx, http

path = pathlib.Path(__file__).resolve().parents[2] / "scripts/proxy/starrail_redirect.py"
spec = importlib.util.spec_from_file_location("redirect_under_test", path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def check(condition, message):
    if not condition:
        raise AssertionError(message)


class Regression:
    failed = False

    def load(self, loader):
        self.redirect = module.StarRailRedirect()
        self.redirect.load(loader)

    def running(self):
        try:
            cases = [
                ("globaldp-prod-os01.starrails.com", "/query_dispatch?version=OSPRODWin4.2.0", True),
                ("passport-api.mihoyo.com", "/account/ma-cn-session/app/verify", True),
                ("api-beta-sdk.mihoyo.com", "/hkrpg_cn/mdk/shield/api/loadConfig", True),
                ("webstatic.mihoyo.com", "/admin/mi18n/plat_os/m09291531181441/m09291531181441-version.json", False),
                ("webstatic.mihoyo.com", "/admin/mi18n/plat_oversea/m2020030410/m2020030410-version.json", False),
                ("globaldp-prod-os01.starrails.com", "/query_security_file?file_key=OSPRODWin4.2.0", False),
                ("autopatchos.starrails.com", "/client/download/asset.bundle", False),
                ("sdk.hoyoverse.com", "/hk4e_global/mdk/shield/api/login", False),
                ("mihoyo.com.example.invalid", "/query_dispatch", False),
            ]
            count = 0
            for host, route, expected in cases:
                req = http.Request.make("GET", "https://" + host + route)
                flow = SimpleNamespace(request=req, metadata={})
                original = req.url
                self.redirect.request(flow)
                check(bool(flow.metadata.get("march7th_redirected")) == expected, f"Wrong route: {host}{route}")
                if expected:
                    check(req.host == "127.0.0.1" and req.port == 21000 and req.path == route, "Redirect lost path/query")
                else:
                    check(req.url == original, "Passthrough URL changed")
                count += 1
            req = http.Request.make("POST", "https://api-beta-sdk.mihoyo.com/hkrpg_cn/mdk/shield/api/login?game_key=hkrpg_cn",
                b'{"account":"fixture","password":"fixture"}', {"x-rpc-language": "zh-cn", "x-rpc-game_biz": "hkrpg_cn",
                    "Content-Type": "application/json", "X-Forwarded-Host": "wrong.invalid"})
            flow = SimpleNamespace(request=req, metadata={}, response=http.Response.make(200, b"ok", {"alt-svc": "h3=:443"}))
            body = req.raw_content
            self.redirect.request(flow)
            check(req.path == "/hkrpg_cn/mdk/shield/api/login?game_key=hkrpg_cn" and req.raw_content == body, "Changed client contract")
            check(req.headers["x-rpc-language"] == "zh-cn" and req.headers["x-rpc-game_biz"] == "hkrpg_cn", "Changed region/language")
            check(req.host_header == "127.0.0.1:21000" and "X-Forwarded-Host" not in req.headers, "Wrong backend host")
            self.redirect.response(flow)
            check("alt-svc" not in flow.response.headers, "Leaked upstream Alt-Svc")
            count += 1
            for value in ["http://user:pass@localhost:21000", "file:///tmp", "http://localhost/path", "http://localhost?token=x"]:
                try:
                    module.parse_target(value)
                    raise AssertionError("Accepted invalid origin")
                except ValueError:
                    pass
            count += 1
            print(f"PASS {count}/{count} proxy scenarios: assets/security passthrough, SDK redirect, unchanged region/body and valid origin")
        except Exception:
            self.failed = True
            raise
        finally:
            ctx.master.shutdown()

    def done(self):
        if self.failed:
            raise SystemExit(1)


addons = [Regression()]
