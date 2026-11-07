"""Redirect Star Rail dispatch/SDK requests; leave CDN and other games alone."""
import logging
import re
from urllib.parse import urlsplit

from mitmproxy import ctx, exceptions, http

VENDOR_SUFFIXES = ("hoyoverse.com", "mihoyo.com", "starrails.com", "bhsr.com")
SDK_PATH = re.compile(
    r"/(?:hkrpg|hkrpg_global|hkrpg_cn)/(?:mdk/|combo/|account/)|"
    r"/common/(?:hkrpg|hkrpg_global|hkrpg_cn)/announcement/"
)
SHARED_PATHS = {
    "/account/risky/api/check", "/account/ma-cn-passport/app/loginByPassword",
    "/account/ma-cn-session/app/verify", "/combo/box/api/config/sdk/combo",
    "/account/ma-cn-session/app/exchange", "/account/ma-session/app/exchange",
    "/combo/box/api/config/sw/precache", "/device-fp/api/getFp",
    "/device-fp/api/getExtList", "/data_abtest_api/config/experiment/list",
}


def should_redirect(host: str, path: str) -> bool:
    host = host.lower().rstrip(".")
    vendor = any(host == suffix or host.endswith("." + suffix) for suffix in VENDOR_SUFFIXES)
    path = path.split("?", 1)[0]
    return vendor and (path in {"/query_dispatch", "/query_gateway"} or
                       path in SHARED_PATHS or SDK_PATH.match(path) is not None)


def parse_target(value: str):
    target = urlsplit(value)
    if (target.scheme not in {"http", "https"} or not target.hostname or
            target.username is not None or target.password is not None or
            target.path not in {"", "/"} or target.query or target.fragment):
        raise ValueError("march7th_server must be an http(s) origin without credentials or path")
    return target.scheme, target.hostname, target.port or (443 if target.scheme == "https" else 80)


class StarRailRedirect:
    def load(self, loader):
        loader.add_option("march7th_server", str, "http://127.0.0.1:21000", "March7thHoney HTTP origin")

    def configure(self, updated):
        if "march7th_server" in updated:
            try:
                parse_target(ctx.options.march7th_server)
            except ValueError as exc:
                raise exceptions.OptionsError(str(exc)) from exc

    def request(self, flow: http.HTTPFlow):
        request = flow.request
        if not should_redirect(request.host, request.path):
            return
        original_host = request.host
        scheme, host, port = parse_target(ctx.options.march7th_server)
        request.scheme, request.host, request.port = scheme, host, port
        # Dispatch builds query_gateway from the incoming Host: it must see the local origin.
        authority = f"[{host}]" if ":" in host else host
        request.host_header = f"{authority}:{port}"
        for header in ("Forwarded", "X-Forwarded-Host", "X-Forwarded-Proto", "Alt-Used"):
            request.headers.pop(header, None)
        flow.metadata["march7th_redirected"] = True
        # Never print login bodies, tokens, cookies or URL query parameters.
        logging.info("March7th redirect %s %s%s", request.method, original_host, request.path.split("?", 1)[0])

    def response(self, flow: http.HTTPFlow):
        if flow.metadata.get("march7th_redirected"):
            flow.response.headers.pop("alt-svc", None)


addons = [StarRailRedirect()]
