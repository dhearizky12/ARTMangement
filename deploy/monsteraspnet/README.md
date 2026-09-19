# MonsterASP.NET deployment template

`web.config.template` is a safe template for an ASP.NET Core Module deployment. The .NET SDK also generates a `web.config` during `dotnet publish`; use this template only when the hosting plan requires environment variables to be declared in the IIS configuration.

Copy the template to the published application as `web.config` on the server, then fill the values through MonsterASP.NET App Settings or on the server's private file. Do not put real values in this repository. The application uses the existing double-underscore configuration keys exactly as listed in [the MonsterASP.NET runbook](../../docs/MONSTERASP.NET.md).
