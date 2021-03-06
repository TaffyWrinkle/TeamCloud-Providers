﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TeamCloud.Model;
using TeamCloud.Model.Commands;
using TeamCloud.Model.Commands.Core;
using TeamCloud.Model.Data.Core;
using TeamCloud.Orchestration;
using TeamCloud.Orchestration.Deployment;
using TeamCloud.Providers.Azure.DevTestLabs.Activities;
using TeamCloud.Serialization;

namespace TeamCloud.Providers.Azure.DevTestLabs.Orchestrations
{
    public static class ProjectCreateOrchestration
    {
        [FunctionName(nameof(ProjectCreateOrchestration))]
        public static async Task RunOrchestration(
            [OrchestrationTrigger] IDurableOrchestrationContext functionContext,
            ILogger log)
        {
            if (functionContext is null)
                throw new ArgumentNullException(nameof(functionContext));

            var command = functionContext.GetInput<ProviderProjectCreateCommand>();
            var commandResult = command.CreateResult();
            var commandLog = functionContext.CreateReplaySafeLogger(log ?? NullLogger.Instance);

            using (log.BeginCommandScope(command))
            {
                try
                {
                    var deploymentOutput = await functionContext
                        .CallDeploymentAsync(nameof(ProjectCreateActivity), command.Payload)
                        .ConfigureAwait(true);

                    commandResult.Result = new ProviderOutput
                    {
                        Properties = deploymentOutput.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString())
                    };

                    var resources = await functionContext
                        .CallActivityWithRetryAsync<IEnumerable<string>>(nameof(ProjectResourceListActivity), command.Payload)
                        .ConfigureAwait(true);

                    var tasks = new List<Task>();

                    tasks.AddRange(resources.Select(resource => functionContext.CallActivityWithRetryAsync(nameof(ProjectResourceRolesActivity), (command.Payload, resource))));
                    tasks.AddRange(resources.Select(resource => functionContext.CallActivityWithRetryAsync(nameof(ProjectResourceTagsActivity), (command.Payload, resource))));

                    await Task
                        .WhenAll(tasks)
                        .ConfigureAwait(true);
                }
                catch (Exception exc)
                {
                    commandResult ??= command.CreateResult();
                    commandResult.Errors.Add(exc);

                    throw exc.AsSerializable();
                }
                finally
                {
                    var commandException = commandResult.Errors?.ToException();

                    if (commandException is null)
                        functionContext.SetCustomStatus($"Command succeeded", commandLog);
                    else
                        functionContext.SetCustomStatus($"Command failed: {commandException.Message}", commandLog, commandException);

                    functionContext.SetOutput(commandResult);
                }
            }
        }
    }
}
