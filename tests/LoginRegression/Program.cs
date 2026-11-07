using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using March7thHoney.Configuration;
using March7thHoney.Database;
using March7thHoney.Database.Account;
using March7thHoney.Util;
using March7thHoney.WebServer;
using March7thHoney.WebServer.Handler;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using SqlSugar;

var temp = Path.Combine(Path.GetTempPath(), "march7th-login-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(temp, "Security"));
ConfigManager.Config.Path.ConfigPath = temp;
ConfigManager.Config.HttpServer.SendHotfix = false;
ConfigManager.Config.GameServer.UsePacketEncryption = false;
ConfigManager.Config.ServerOption.AutoCreateUser = true;
using var db = new SqlSugarScope(new ConnectionConfig { DbType = DbType.Sqlite,
    ConnectionString = $"Data Source={Path.Combine(temp, "test.sqlite")};", IsAutoCloseConnection = true,
    ConfigureExternalServices = DatabaseHelper.CreateExternalServices(DbType.Sqlite) });
DatabaseHelper.sqlSugarScope = db; _ = new DatabaseHelper();
DatabaseHelper.InitializeSqlite();
AccountHelper.CreateAccount(new string('A', 171) + "=", "fixture@example.invalid", "fixture-password", 10001, null, true);
using var key = RSA.Create(2048);
File.WriteAllText(Path.Combine(temp, "Security/LoginRsaPrivateKey.pem"), key.ExportPkcs8PrivateKeyPem());
var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
using var host = WebProgram.BuildWebHost(["--applicationName", typeof(WebProgram).Assembly.GetName().Name!], port, $"http://127.0.0.1:{port}");
await host.StartAsync();
using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
async Task<JObject> Post(string path, object data)
{
    using var response = await client.PostAsJsonAsync(path, data);
    Check(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode} at {path}");
    return JObject.Parse(await response.Content.ReadAsStringAsync());
}
var single = ConfigManager.Config.WebSecurity.SinglePlayerLogin;
var tests = new (string, Func<Task>)[]
{
    ("Default config leaves automatic login disabled", async () => {
        Check(!single.Enabled, "Enabled by default");
        Check((int)(await Post("/hkrpg_global/mdk/shield/api/login", new { account = "", password = "" }))["retcode"]! != 0, "Empty credentials accepted");
    }),
    ("HEAD probes and GET dispatch/gateway succeed", async () => {
        foreach (var path in new[] { "/query_dispatch", "/query_gateway?version=OSPRODWin4.2.0" }) {
            using var get = await client.GetAsync(path); Check(get.IsSuccessStatusCode, "GET failed");
            _ = Convert.FromBase64String(await get.Content.ReadAsStringAsync());
            using var head = await client.SendAsync(new(HttpMethod.Head, path));
            Check(head.IsSuccessStatusCode && (await head.Content.ReadAsByteArrayAsync()).Length == 0, "HEAD failed or sent a body");
        }
    }),
    ("Local login repairs an explicitly bound UID without creating another account", async () => {
        single.Enabled = true; single.Username = "biu"; single.AccountUid = 10001;
        var response = await Post("/account/ma-cn-passport/app/loginByPassword", new { account = new string('X', 172), password = "ciphertext" });
        Check((int)response["retcode"]! == 0 && (string?)response["data"]?["user_info"]?["account_name"] == "biu", "Fixed identity missing");
        Check((string?)response["data"]?["user_info"]?["aid"] == "10001" && db.Queryable<AccountData>().Count() == 1, "UID changed or duplicate account");
    }),
    ("Shield and Passport cached logins consistently select biu", async () => {
        var shield = await Post("/hkrpg_global/mdk/shield/api/verify", new { uid = "stale", token = "old" });
        Check((string?)shield["data"]?["account"]?["name"] == "biu", "Shield cache not remapped");
        var passport = await Post("/account/ma-cn-session/app/verify", new { mid = "stale", token = new { token_type = 1, token = "old" } });
        Check((string?)passport["data"]?["user_info"]?["aid"] == "10001", "Passport cache not remapped");
        var modern = await Post("/hkrpg_global/account/ma-passport/api/appLoginByPassword", new { account = "anything", password = "anything" });
        Check((string?)modern["data"]?["user_info"]?["account_name"] == "biu", "Modern login not remapped");
    }),
    ("Local session exchange and combo return persisted valid tokens", async () => {
        var exchange = await Post("/account/ma-cn-session/app/exchange", new { });
        var account = AccountData.GetAccountByUid(10001)!;
        Check(account.ValidateDispatchToken((string?)exchange["data"]?["token"]?["token"]), "Exchange returned invalid token");
        var combo = await Post("/hkrpg_global/combo/granter/login/v2/login", new { data = "stale cache" });
        Check((string?)combo["data"]?["open_id"] == "10001" && account.ValidateComboToken((string?)combo["data"]?["combo_token"]), "Invalid game token");
    }),
    ("Concurrent login calls do not create accounts or invalidate each other's tokens", async () => {
        var calls = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Post("/hkrpg_global/mdk/shield/api/login", new { account = "", password = "" })));
        var tokens = calls.Select(r => (string?)r["data"]?["account"]?["token"]).Distinct().ToArray();
        Check(tokens.Length == 1 && AccountData.GetAccountByUid(10001)!.ValidateDispatchToken(tokens[0]) && db.Queryable<AccountData>().Count() == 1, "Concurrent login race");
    }),
    ("Remote peers cannot spoof loopback with forwarded headers", () => {
        var context = new DefaultHttpContext(); context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.25");
        SinglePlayerLogin.RememberPeer(context);
        context.Request.Headers["X-Forwarded-For"] = "127.0.0.1";
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        Check(SinglePlayerLogin.TryRespond(context, SinglePlayerResponse.Shield, out var response) &&
            (int)JObject.FromObject(response.Value!)["retcode"]! != 0, "Forwarded IP bypass");
        return Task.CompletedTask;
    }),
    ("Reopening SQLite keeps repaired name and issued game token", () => {
        var token = AccountData.GetAccountByUid(10001)!.ComboToken;
        DatabaseHelper.UidInstanceMap.TryRemove(10001, out _);
        var account = DatabaseHelper.Instance!.GetInstanceOrCreateNew<AccountData>(10001);
        Check(account.Username == "biu" && account.ValidateComboToken(token), "Reload lost identity/token");
        return Task.CompletedTask;
    }),
    ("Normal Passport login rejects undecryptable ciphertext without creating accounts", async () => {
        single.Enabled = false;
        using var wrongKey = RSA.Create(2048);
        var encrypted = Convert.ToBase64String(wrongKey.Encrypt(Encoding.UTF8.GetBytes("not-an-account"), RSAEncryptionPadding.Pkcs1));
        foreach (var path in new[] { "/account/ma-cn-passport/app/loginByPassword", "/hkrpg_global/account/ma-passport/api/appLoginByPassword" }) {
            var response = await Post(path, new { account = encrypted, password = encrypted });
            Check((int)response["retcode"]! != 0 && db.Queryable<AccountData>().Count() == 1, "Ciphertext became an account");
        }
    }),
    ("Normal RSA login still decrypts the matching key; exchange stays gated", async () => {
        ConfigManager.Config.ServerOption.AutoCreateUser = false;
        string Encrypt(string value) => Convert.ToBase64String(key.Encrypt(Encoding.UTF8.GetBytes(value), RSAEncryptionPadding.Pkcs1));
        var response = await Post("/account/ma-cn-passport/app/loginByPassword", new { account = Encrypt("biu"), password = Encrypt("fixture-password") });
        Check((int)response["retcode"]! == 0, "Valid RSA login rejected");
        Check((int)(await Post("/account/ma-cn-session/app/exchange", new { }))["retcode"]! != 0, "Exchange bypass without local mode");
    }),
    ("Single-player mode still rejects a banned account", async () => {
        single.Enabled = true;
        var account = AccountData.GetAccountByUid(10001)!; account.IsBanned = true;
        try { Check((int)(await Post("/hkrpg_global/mdk/shield/api/login", new { }))["retcode"]! != 0, "Ban bypass"); }
        finally { account.IsBanned = false; }
    }),
    ("Fresh fixed-name account is created once and remains stable", async () => {
        single.Username = "new_fixture"; single.AccountUid = 0;
        var first = await Post("/account/ma-cn-passport/app/loginByPassword", new { });
        var second = await Post("/account/ma-cn-passport/app/loginByPassword", new { });
        Check((int)first["retcode"]! == 0 && (string?)first["data"]?["user_info"]?["aid"] == (string?)second["data"]?["user_info"]?["aid"] &&
            db.Queryable<AccountData>().Count() == 2, "Fresh fixed account duplicated");
    }),
    ("Explicit UID binding refuses an existing name owned by another account", async () => {
        single.AccountUid = 10001;
        Check((int)(await Post("/account/ma-cn-passport/app/loginByPassword", new { }))["retcode"]! != 0 &&
            AccountData.GetAccountByUid(10001)!.Username == "biu", "Name collision overwrote an account");
    })
};
var failed = 0;
try {
    foreach (var (name, run) in tests) {
        try { await run(); Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.Error.WriteLine($"FAIL {name}: {e.Message}"); }
    }
} finally { await host.StopAsync(); }
Console.WriteLine($"{tests.Length-failed}/{tests.Length} scenarios passed. Isolated fixtures: {temp}");
return failed == 0 ? 0 : 1;
static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
