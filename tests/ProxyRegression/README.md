# Proxy regression

Run from the repository root with the installed mitmproxy binary:

```powershell
mitmdump --set server=false --set flow_detail=0 -s tests/ProxyRegression/check_addon.py
```

Uses the real mitmproxy Request/Response objects and production addon. The 11 scenarios cover SDK/dispatch routing, the two localization URLs seen in the 4.2 client log, security-file/CDN passthrough, other games, deceptive host suffixes, unchanged region/language/body/query, backend Host, Alt-Svc and origin validation. No listening proxy or upstream HTTP request is created. A failure exits nonzero.
