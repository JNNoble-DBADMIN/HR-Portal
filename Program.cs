using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Novell.Directory.Ldap;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Yarp.ReverseProxy.Configuration;

public class Program
{
    // ---- CONFIG ----
    private const int GatewayExternalPort = 88;            // host published port
    private const string CookieDomain = ".tv5.com.ph";
    private const string SessionCookieName = "GatewaySession";

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var portalHost = builder.Configuration["Gateway:PortalHost"] ?? "";
        var app1Host = builder.Configuration["Gateway:App1Host"] ?? "";
        var app2Host = builder.Configuration["Gateway:App2Host"] ?? "";

        if (string.IsNullOrWhiteSpace(portalHost) ||
            string.IsNullOrWhiteSpace(app1Host) ||
            string.IsNullOrWhiteSpace(app2Host))
        {
            throw new InvalidOperationException("Gateway:PortalHost / App1Host / App2Host must be set.");
        }

        // ---- YARP: host-based routing ----
        builder.Services.AddReverseProxy()
            .LoadFromMemory(
                routes: new[]
                {
                    new RouteConfig
                    {
                        RouteId = "app1-route",
                        ClusterId = "app1-cluster",
                        Match = new RouteMatch
                        {
                            Hosts = new[] { app1Host },
                            Path = "/{**catch-all}"
                        },
                        // Avoid IIS "Bad Request - Invalid Hostname" (keep if needed)
                        Transforms = new[]
                        {
                            new Dictionary<string, string>
                            {
                                ["RequestHeader"] = "Host",
                                ["Set"] = "app1"
                            }
                        }
                    },
                    new RouteConfig
                    {
                        RouteId = "app2-route",
                        ClusterId = "app2-cluster",
                        Match = new RouteMatch
                        {
                            Hosts = new[] { app2Host },
                            Path = "/{**catch-all}"
                        },
                        Transforms = new[]
                        {
                            new Dictionary<string, string>
                            {
                                ["RequestHeader"] = "Host",
                                ["Set"] = "app2"
                            }
                        }
                    }
                },
                clusters: new[]
                {
                    new ClusterConfig
                    {
                        ClusterId = "app1-cluster",
                        Destinations = new Dictionary<string, DestinationConfig>
                        {
                            ["d1"] = new DestinationConfig { Address = "http://app1:80/" }
                        }
                    },
                    new ClusterConfig
                    {
                        ClusterId = "app2-cluster",
                        Destinations = new Dictionary<string, DestinationConfig>
                        {
                            ["d1"] = new DestinationConfig { Address = "http://app2:80/" }
                        }
                    }
                });

        var app = builder.Build();

        // Serve /wwwroot static files (logo etc.)
        app.UseStaticFiles();

        bool IsPortalHost(HttpContext ctx) =>
            string.Equals(ctx.Request.Host.Host, portalHost, StringComparison.OrdinalIgnoreCase);

        bool IsAppHost(HttpContext ctx)
        {
            var h = ctx.Request.Host.Host;
            return string.Equals(h, app1Host, StringComparison.OrdinalIgnoreCase)
                || string.Equals(h, app2Host, StringComparison.OrdinalIgnoreCase);
        }

        bool IsLoggedIn(HttpContext ctx) =>
            ctx.Request.Cookies.TryGetValue(SessionCookieName, out var v) && !string.IsNullOrWhiteSpace(v);

        // Rewrite /Login -> /login ONLY for the PORTAL host.
        // (Do NOT rewrite for shuttle/workstation or you'll break their real /Login page)
        app.Use(async (ctx, next) =>
        {
            if (IsPortalHost(ctx) && ctx.Request.Path.Equals("/Login", StringComparison.Ordinal))
            {
                ctx.Request.Path = "/login";
            }
            await next();
        });

        // Gate: portal always allowed; app hosts require gateway session
        app.Use(async (ctx, next) =>
        {
            if (IsPortalHost(ctx))
            {
                await next();
                return;
            }

            if (IsAppHost(ctx) && !IsLoggedIn(ctx))
            {
                var returnTo =
                    $"http://{ctx.Request.Host.Host}:{GatewayExternalPort}{ctx.Request.Path}{ctx.Request.QueryString}";

                ctx.Response.Redirect(
                    $"http://{portalHost}:{GatewayExternalPort}/login?next={Uri.EscapeDataString(returnTo)}");
                return;
            }

            await next();
        });

        // -------------------------
        // PORTAL ROUTES (PORTAL HOST ONLY)
        // -------------------------

        app.MapGet("/", async (HttpContext ctx) =>
        {
            var html = $@"
<!doctype html>
<html>
<head>
  <meta charset=""utf-8""/>
  <meta name=""viewport"" content=""width=device-width, initial-scale=1""/>
  <title>Applications Portal</title>

  <style>
    :root {{
      --card-w: 560px;
      --shadow: 0 18px 45px rgba(0,0,0,.22);
      --border: rgba(0,0,0,.10);
      --text: #111;
      --muted: #666;
      --bg: #f3f4f6;
      --btn-bg: #f7f8fa;
    }}

    html, body {{
      height: 100%;
      margin: 0;
      font-family: Segoe UI, Roboto, Arial, sans-serif;
      color: var(--text);
      background: radial-gradient(1200px 700px at 50% 8%, #ffffff 0%, var(--bg) 60%);
    }}

    .wrap {{
      min-height: 100%;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 24px;
    }}

    .card {{
      width: min(var(--card-w), 92vw);
      background: rgba(255,255,255,.96);
      border: 1px solid var(--border);
      border-radius: 12px;
      box-shadow: var(--shadow);
      padding: 34px 34px 26px;
      text-align: center;
    }}

    .logo img {{
      height: 60px;
      object-fit: contain;
      margin-bottom: 14px;
    }}

    h2 {{
      margin: 8px 0 18px;
      font-size: 22px;
      font-weight: 650;
      letter-spacing: .2px;
    }}

    .sub {{
      margin-top: -10px;
      margin-bottom: 18px;
      color: var(--muted);
      font-size: 13px;
    }}

    .link {{
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 10px;
      text-decoration: none;
      background: var(--btn-bg);
      border: 1px solid rgba(0,0,0,.12);
      border-radius: 10px;
      padding: 14px 16px;
      margin: 12px 0;
      font-size: 15px;
      font-weight: 650;
      color: #222;
      transition: .15s ease;
    }}

    .link:hover {{
      background: #fff;
      box-shadow: 0 4px 12px rgba(0,0,0,.12);
      transform: translateY(-1px);
    }}

    .logout {{
      display: inline-block;
      margin-top: 18px;
      font-size: 13px;
      color: var(--muted);
      text-decoration: none;
    }}

    .logout:hover {{
      text-decoration: underline;
    }}
  </style>
</head>

<body>
  <div class=""wrap"">
    <div class=""card"">

      <div class=""logo"">
        <img src=""/assets/mq-cignal.png"" alt=""MediaQuest / Cignal"" />
      </div>

      <h2>Applications Portal</h2>
      <div class=""sub"">Choose an application to continue</div>

      <a class=""link"" href=""http://{app1Host}:{GatewayExternalPort}/Default.aspx"">
        🚍 <span>Shuttle Reservation</span>
      </a>

      <a class=""link"" href=""http://{app2Host}:{GatewayExternalPort}/"">
        💻 <span>Workstation Reservation</span>
      </a>

      <a class=""logout"" href=""/logout"">Logout</a>

    </div>
  </div>
</body>
</html>";

            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync(html);
        }).RequireHost(portalHost);

        app.MapGet("/login", async (HttpContext ctx) =>
        {
            if (IsLoggedIn(ctx))
            {
                ctx.Response.Redirect($"http://{portalHost}:{GatewayExternalPort}/");
                return;
            }

            var next = ctx.Request.Query["next"].ToString();
            var encodedNext = System.Net.WebUtility.HtmlEncode(next);

            var html = $@"
<!doctype html>
<html>
<head>
  <meta charset=""utf-8""/>
  <meta name=""viewport"" content=""width=device-width, initial-scale=1""/>
  <title>Portal Login</title>

  <style>
    :root {{
      --card-w: 560px;
      --shadow: 0 18px 45px rgba(0,0,0,.22);
      --border: rgba(0,0,0,.10);
      --text: #111;
      --muted: #666;
      --bg: #f3f4f6;
      --btn: #7a0b0b;
    }}

    html, body {{
      height: 100%;
      margin: 0;
      font-family: Segoe UI, Roboto, Arial, sans-serif;
      color: var(--text);
      background: radial-gradient(1200px 700px at 50% 8%, #ffffff 0%, var(--bg) 60%);
    }}

    .wrap {{
      min-height: 100%;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 24px;
    }}

    .card {{
      width: min(var(--card-w), 92vw);
      background: rgba(255,255,255,.96);
      border: 1px solid var(--border);
      border-radius: 12px;
      box-shadow: var(--shadow);
      padding: 34px 34px 28px;
      text-align: center;
    }}

    .logo img {{
      height: 60px;
      object-fit: contain;
      margin-bottom: 14px;
    }}

    h2 {{
      margin: 6px 0 6px;
      font-size: 20px;
      font-weight: 750;
    }}

    .hint {{
      margin: 0 0 18px;
      color: var(--muted);
      font-size: 13px;
      line-height: 1.4;
    }}

    .field {{
      text-align: left;
      margin: 12px 0;
    }}

    label {{
      display: block;
      font-size: 13px;
      color: #333;
      margin-bottom: 6px;
      font-weight: 600;
    }}

    input {{
      width: 100%;
      box-sizing: border-box;
      padding: 12px 12px;
      border: 1px solid rgba(0,0,0,.18);
      border-radius: 10px;
      font-size: 14px;
      outline: none;
    }}

    input:focus {{
      border-color: rgba(122,11,11,.55);
      box-shadow: 0 0 0 3px rgba(122,11,11,.12);
    }}

    button {{
      margin-top: 14px;
      width: 100%;
      padding: 12px 14px;
      border: none;
      border-radius: 10px;
      background: var(--btn);
      color: #fff;
      font-size: 14px;
      font-weight: 800;
      cursor: pointer;
      letter-spacing: .4px;
    }}

    button:hover {{
      filter: brightness(1.05);
    }}

    .small {{
      margin-top: 14px;
      color: var(--muted);
      font-size: 12px;
      line-height: 1.4;
    }}
  </style>
</head>

<body>
  <div class=""wrap"">
    <div class=""card"">

      <div class=""logo"">
        <img src=""/assets/mq-cignal.png"" alt=""MediaQuest / Cignal"" />
      </div>

      <h2>HR Applications Portal DBM SUPPORT</h2>

      <div class=""hint"">
        Sign in with your AD account (e.g. <b>TV5\\username</b> or <b>user@cignaltv.com.ph</b>)
      </div>

      <form method=""post"" action=""/login"">
        <input type=""hidden"" name=""next"" value=""{encodedNext}""/>

        <div class=""field"">
          <label>Username</label>
          <input name=""username"" autocomplete=""username"" />
        </div>

        <div class=""field"">
          <label>Password</label>
          <input name=""password"" type=""password"" autocomplete=""current-password"" />
        </div>

        <button type=""submit"">LOGIN</button>

        <div class=""small"">
          If you are redirected back here, your session may have expired.
        </div>
      </form>

    </div>
  </div>
</body>
</html>";

            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync(html);
        }).RequireHost(portalHost);

        app.MapPost("/login", async (HttpContext ctx) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var username = form["username"].ToString().Trim();
            var password = form["password"].ToString();
            var nextUrl = form["next"].ToString();

            var ldapHost = app.Configuration["Ldap:Host"]!;
            var ldapPort = int.Parse(app.Configuration["Ldap:Port"]!);
            var useSsl = bool.Parse(app.Configuration["Ldap:UseSsl"]!);
            var domain = app.Configuration["Ldap:Domain"]!;

            string bindUser = (username.Contains("@") || username.Contains("\\"))
                ? username
                : $"{domain}\\{username}";

            if (!TryLdapBind(ldapHost, ldapPort, useSsl, bindUser, password, out var err))
            {
                ctx.Response.StatusCode = 401;
                ctx.Response.ContentType = "text/html";
                await ctx.Response.WriteAsync(
                    "LDAP bind failed for: <b>" + System.Net.WebUtility.HtmlEncode(bindUser) + "</b>" +
                    "<br/><br/><pre style=\"white-space:pre-wrap;border:1px solid #ccc;padding:12px;\">" +
                    System.Net.WebUtility.HtmlEncode(err) +
                    "</pre><br/><a href=\"/login\">Try again</a>"
                );
                return;
            }

            ctx.Response.Cookies.Append(
                SessionCookieName,
                bindUser,
                new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = false, // set true when HTTPS
                    Path = "/",
                    Domain = CookieDomain
                });

            if (!string.IsNullOrWhiteSpace(nextUrl))
            {
                ctx.Response.Redirect(nextUrl);
                return;
            }

            ctx.Response.Redirect($"http://{portalHost}:{GatewayExternalPort}/");
        }).RequireHost(portalHost);

        app.MapGet("/logout", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete(SessionCookieName, new CookieOptions
            {
                Path = "/",
                Domain = CookieDomain
            });

            ctx.Response.Redirect("/login");
            return Task.CompletedTask;
        }).RequireHost(portalHost);

        // Proxy to apps (host-based)
        app.MapReverseProxy();

        app.Run();
    }

    private static bool TryLdapBind(string host, int port, bool useSsl, string user, string pass, out string error)
    {
        error = "";
        try
        {
            using var conn = new LdapConnection { SecureSocketLayer = useSsl };

            if (useSsl)
            {
#pragma warning disable CS0618
                conn.UserDefinedServerCertValidationDelegate += (sender, certificate, chain, sslPolicyErrors) =>
                {
                    try
                    {
                        if (sslPolicyErrors == SslPolicyErrors.None) return true;

                        var x509 = new X509Certificate2(certificate);
                        using var x509Chain = new X509Chain();
                        x509Chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                        x509Chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
                        x509Chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
                        return x509Chain.Build(x509);
                    }
                    catch { return false; }
                };
#pragma warning restore CS0618
            }

            conn.Connect(host, port);
            conn.Bind(user, pass);
            return conn.Bound;
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return false;
        }
    }
}
