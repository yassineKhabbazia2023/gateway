# API Gateway 
This project implements a hybrid API Gateway using Ocelot and ASP.NET 8 controllers to route requests to various microservices and provide additional processing capabilities.
 It combines the robust routing and middleware features of Ocelot with the flexibility and power of ASP.NET 8 controllers.

## Prerequisites

- .NET 8 SDK
- Visual Studio 2022 or later (recommended for development)
- An understanding of microservices architecture and RESTful APIs

## Configuration

Before running the project locally, you must initialize the `OCELOT_CONFIG_PATH` environment variable. This variable should point to the location of your Ocelot configuration file (`ocelot.json`).

### Setting the `OCELOT_CONFIG_PATH` Environment Variable

- **Windows:**

  ```bash
  setx OCELOT_CONFIG_PATH "C:\path\to\your\ocelot.json"
  
  or just initialize your variable in appsettings.developpement.json 

