# Login regression checks

Run `dotnet run --project tests/LoginRegression -c Release` from the repository root.

Uses real Kestrel HTTP routes on an ephemeral loopback port, generated RSA keys and a disposable SQLite database. No game resources, production database or upstream account service is needed. Covers 13 scenarios: default-off behavior, HEAD probes, explicit UID/name repair, cached login, local exchange/combo, concurrent requests, forwarded-IP spoofing, database reload, RSA mismatch/success, bans, fresh account creation and username conflicts.

The local exchange response is a compatibility implementation; passing server tests does not validate the entire client SDK exchange contract or establish that the game reaches gameplay.
