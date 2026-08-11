# API Gateway 
This project implements a hybrid API Gateway using Ocelot and ASP.NET 8 controllers to route requests to various microservices and provide additional processing capabilities.
 It combines the robust routing and middleware features of Ocelot with the flexibility and power of ASP.NET 8 controllers.

## Prerequisites

- .NET 8 SDK
- Visual Studio 2022 or later (recommended for development)
- An understanding of microservices architecture and RESTful APIs

## Configuration

Running the project locally requires no `OCELOT_CONFIG_PATH`. In the `Development` environment the
Ocelot configuration is built in memory from `src/Config/ocelot.*.json`: the files are merged, the
`#{env_id}#` / `#{env}#` tokens are substituted, and every downstream is rewritten to point either
at a service running on your machine or at the deployed environment. See the `LocalRouting` section
of `appsettings.Development.json`, and copy `appsettings.local.example.json` to
`appsettings.local.json` to declare the services you run locally.

`appsettings.local.json` also holds the Gigya credentials, which are never versioned. `GigyaApiKey`
is declared there only: `appsettings.Development.json` references it through the
`#{gigya_api_key}#` token, both in the JWK fetch URL and in the issuer of the Gigya authority.

`OCELOT_CONFIG_PATH` is still read outside `Development`. It must then point to the folder holding
the `ocelot.json` produced by the deployment pipeline.

See [`doc/Run-Local-Gateway.md`](doc/Run-Local-Gateway.md) for the full local run: how a route is
rewritten, and what to do when one starts returning an nginx 404.

