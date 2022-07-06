using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Collections.Generic;
using PriorityCases.Model;
using PriorityCases;

namespace Dreamtec.Function
{
    public static class PriorityCases
    {

        [FunctionName(Constants.PriorityCasesOrchestrator)]
        public static async Task<OrchestrationDto> RunOrchestrator(
           [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            var orchestrationDto = new OrchestrationDto
            {
                Name = context.GetInput<string>()
            };

            if (!context.IsReplaying)
            {
                log.LogWarning($"begin MyOrchestration with input {context.GetInput<string>()}");
            }

            var sendSignalRMessage = await context.CallActivityAsync<string>(
                Constants.SendSignalRMessage, context.GetInput<string>());

            return orchestrationDto;
        }

        [FunctionName("PriorityCasesTrigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log,
            [DurableClient] IDurableOrchestrationClient starter)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync(Constants.PriorityCasesOrchestrator, null, name);

            log.LogWarning($"Started orchestration with ID = '{instanceId}' and name = '{name}.");

            starter.CreateCheckStatusResponse(req, instanceId);

            return name != null
                ? (ActionResult)new OkObjectResult($"Hello, {name}")
                : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
        }

        [FunctionName("Megotiate")]
        public static SignalRConnectionInfo Negotiate(
            [HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequest req,
            [SignalRConnectionInfo(HubName = "serverless")] SignalRConnectionInfo connectionInfo,
            ILogger log)
        {
            log.LogWarning($"ConnectionInfo: {connectionInfo.Url} {connectionInfo.AccessToken} {req.Path} {req.Query}");

            return connectionInfo;
        }

        [FunctionName(Constants.SendSignalRMessage)]
        public static async Task SendMessage([ActivityTrigger] IDurableActivityContext context, ILogger log,
            [SignalR(HubName = "serverless")] IAsyncCollector<SignalRMessage> signalRMessages)
        {

            string name = context.GetInput<string>();

            log.LogWarning($"Sending signal R message with name = '{name}.");

            await signalRMessages.AddAsync(
                 new SignalRMessage
                 {
                     Target = "newMessage",
                     Arguments = new[] { name }
                 });
        }
    }
}
