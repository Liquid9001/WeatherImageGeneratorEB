# Weather Image Generator (Azure Functions)

An asynchronous, scalable serverless application built with .NET 8 and Azure Functions. This API generates dynamic weather images by fetching current weather data for 50 stations in the Netherlands and overlaying it onto background images.

## Features

* **Asynchronous Processing:** Uses an HTTP API to accept requests and immediately returns a Job ID while processing happens in the background.
* **Fan-Out Architecture:** Employs multiple Azure Storage Queues to fan out tasks, ensuring scalable and parallel processing for 50 weather stations.
* **External API Integration:** * [Buienradar API](https://data.buienradar.nl/2.0/feed/json) for live weather data.
  * Pexels API for dynamic, high-quality background images.
* **Infrastructure as Code (IaC):** Fully automated infrastructure provisioning using Azure Bicep.
* **Automated Deployment:** Includes a PowerShell script (`deploy.ps1`) to compile code, build infrastructure, and publish the Function App via the Azure CLI.

## Architecture Workflow

1. **POST `/api/jobs/start`:** User requests a new image generation job.
2. **Queue (`image-start`):** The HTTP trigger drops a message into the start queue and returns the Job ID to the user.
3. **Fan-Out Worker:** A QueueTrigger picks up the job, fetches the 50 weather stations from Buienradar, and creates 50 separate messages in the next queue.
4. **Processing Worker (`image-process`):** Multiple QueueTriggers run in parallel to grab background images, draw the weather data onto the images, and upload them to Azure Blob Storage.
5. **GET Endpoints:** Users can poll the status endpoint and eventually fetch the secure SAS links to the generated images.

## Prerequisites

To run or deploy this project, you need:
* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
* [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
* PowerShell 7+
* A valid [Pexels API Key](https://www.pexels.com/api/)

## Local Development Setup

1. Clone the repository.
2. Create a `local.settings.json` file in the root of the project with the following structure (ensure this file is ignored in `.gitignore`):
   ```json
   {
     "IsEncrypted": false,
     "Values": {
       "AzureWebJobsStorage": "UseDevelopmentStorage=true",
       "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
       "PEXELS_API_KEY": "your_api_key_here"
     }
   }

 ## talking to the azure api
    if you wanna test the api that i pushed to azure use these links
    in your windows powershell do this and form there you will get your jobID.
  ```powershell
    Invoke-RestMethod -Uri "https://func-weatherimg-2026-4633.azurewebsites.net/api/jobs/start" -Method Post -Body "{`"requestedBy`": `"azure-test`"}" -ContentType "application/json"
  ```

    https://func-weatherimg-2026-4633.azurewebsites.net/api/jobs/{jobID}/images
    https://func-weatherimg-2026-4633.azurewebsites.net/api/jobs/{jobID}/status
