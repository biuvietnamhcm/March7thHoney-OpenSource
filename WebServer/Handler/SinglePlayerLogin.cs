using System.Net;
using System.Text.RegularExpressions;
using March7thHoney.Database;
using March7thHoney.Database.Account;
using March7thHoney.Util;
using March7thHoney.Util.Security;
using March7thHoney.WebServer.Objects;
using Microsoft.AspNetCore.Mvc;

namespace March7thHoney.WebServer.Handler;

public enum SinglePlayerResponse { Shield, Passport, Combo }

public static class SinglePlayerLogin
{
    private static readonly object Gate = new();
    private static readonly object PeerKey = new();

    // Capture the socket peer BEFORE ForwardedHeaders rewrites RemoteIpAddress.
    public static void RememberPeer(HttpContext context) => context.Items[PeerKey] = context.Connection.RemoteIpAddress;

    public static bool TryRespond(HttpContext context, SinglePlayerResponse kind, out JsonResult result)
    {
        result = null!;
        var config = ConfigManager.Config.WebSecurity.SinglePlayerLogin;
        if (!config.Enabled) return false;
        var peer = context.Items.TryGetValue(PeerKey, out var original) ? original as IPAddress : null;
        if (peer?.IsIPv4MappedToIPv6 == true) peer = peer.MapToIPv4();
        if (peer == null || !IPAddress.IsLoopback(peer))
        {
            result = new JsonResult(LoginResJson.Error(-201, "Single-player login is available only on localhost"));
            return true;
        }

        lock (Gate)
        {
            if (!Regex.IsMatch(config.Username ?? "", "^[a-zA-Z0-9_]{1,32}$") || config.AccountUid < 0)
            {
                result = new JsonResult(LoginResJson.Error(-202, "Invalid single-player account configuration"));
                return true;
            }
            var named = AccountData.GetAccountByUserName(config.Username!);
            var account = config.AccountUid > 0 ? AccountData.GetAccountByUid(config.AccountUid) : named;
            if (named != null && config.AccountUid > 0 && named.Uid != config.AccountUid)
            {
                result = new JsonResult(LoginResJson.Error(-202, "Single-player username belongs to another UID"));
                return true;
            }
            if (account == null)
            {
                AccountHelper.CreateAccount(config.Username!, $"{config.Username}@singleplayer.invalid",
                    AuthSecurity.GenerateSessionToken(), config.AccountUid, null, true);
                account = AccountData.GetAccountByUserName(config.Username!)!;
            }
            if (!LoginSessionFactory.CanAuthenticate(account, [], out var failure))
            {
                result = new JsonResult(LoginResJson.Error(failure.Retcode, failure.Message));
                return true;
            }
            // Explicit UID binding authorizes repairing the name, never moving/deleting player data.
            if (account.Username != config.Username)
            {
                account.Username = config.Username;
                DatabaseHelper.Instance!.SaveUidData(account.Uid);
            }
            var token = account.ValidateDispatchToken(account.DispatchToken) ? account.DispatchToken! : account.GenerateDispatchToken();
            var uid = account.Uid.ToString();
            object payload = kind switch
            {
                SinglePlayerResponse.Shield => LoginResJson.Success(uid, config.Username!, account.Email ?? "", account.IsEmailVerified, token),
                SinglePlayerResponse.Passport => PassportLoginResJson.Success(uid, config.Username!, account.Email ?? "", account.IsEmailVerified, token),
                _ => new ComboTokenResJson { retcode = 0, message = "OK", data = new(uid,
                    account.ValidateComboToken(account.ComboToken) ? account.ComboToken! : account.GenerateComboToken()) }
            };
            result = new JsonResult(payload);
            return true;
        }
    }
}
