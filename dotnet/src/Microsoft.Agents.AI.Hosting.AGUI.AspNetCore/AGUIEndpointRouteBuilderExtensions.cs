// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;

/// <summary>
/// Provides extension methods for mapping AG-UI agents to ASP.NET Core endpoints.
/// </summary>
public static class AGUIEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps an AG-UI agent endpoint.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="pattern">The URL pattern for the endpoint.</param>
    /// <param name="agentFactory">Factory function to create an agent instance. Receives messages and tools from the client.</param>
    /// <returns>An <see cref="IEndpointConventionBuilder"/> for the mapped endpoint.</returns>
    public static IEndpointConventionBuilder MapAGUIAgent(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        Func<IEnumerable<ChatMessage>, IEnumerable<AITool>, AIAgent> agentFactory)
    {
        return endpoints.MapPost(pattern, async context =>
        {
            var cancellationToken = context.RequestAborted;
            var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("MapAGUIAgent");

            RunAgentInput? input;
            try
            {
                input = await JsonSerializer.DeserializeAsync(context.Request.Body, AGUIJsonSerializerContext.Default.RunAgentInput, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                await TypedResults.BadRequest().ExecuteAsync(context).ConfigureAwait(false);
                return;
            }

            if (input is null)
            {
                await TypedResults.BadRequest().ExecuteAsync(context).ConfigureAwait(false);
                return;
            }

            var messages = input.Messages.AsChatMessages(AGUIJsonSerializerContext.Default.Options);
            logger.LogInformation("[MapAGUIAgent] Received request - ThreadId: {ThreadId}, RunId: {RunId}, MessageCount: {MessageCount}",
                input.ThreadId, input.RunId, messages.Count());

            for (int i = 0; i < messages.Count(); i++)
            {
                var msg = messages.ElementAt(i);
                logger.LogDebug("[MapAGUIAgent]   Message[{Index}]: Role={Role}, ContentCount={ContentCount}",
                    i, msg.Role.Value, msg.Contents.Count);

                foreach (var content in msg.Contents)
                {
                    if (content is FunctionCallContent fcc)
                    {
                        logger.LogDebug("[MapAGUIAgent]     - FunctionCallContent: Name={Name}, CallId={CallId}",
                            fcc.Name, fcc.CallId);
                    }
                    else if (content is FunctionResultContent frc)
                    {
                        logger.LogDebug("[MapAGUIAgent]     - FunctionResultContent: CallId={CallId}, Result={Result}",
                            frc.CallId, frc.Result);
                    }
                    else
                    {
                        logger.LogDebug("[MapAGUIAgent]     - {ContentType}", content.GetType().Name);
                    }
                }
            }

            var contextValues = input.Context;
            var forwardedProps = input.ForwardedProperties;
            IEnumerable<AITool> tools = input.Tools?.AsAITools() ?? [];

            logger.LogInformation("[MapAGUIAgent] Creating agent with {ToolCount} tools", tools.Count());
            var agent = agentFactory(messages, tools);

            logger.LogInformation("[MapAGUIAgent] Starting agent.RunStreamingAsync for ThreadId: {ThreadId}, RunId: {RunId}",
                input.ThreadId, input.RunId);

            var events = agent.RunStreamingAsync(
                messages,
                cancellationToken: cancellationToken)
                .AsChatResponseUpdatesAsync()
                .AsAGUIEventStreamAsync(
                    input.ThreadId,
                    input.RunId,
                    AGUIJsonSerializerContext.Default.Options,
                    cancellationToken);

            var sseLogger = context.RequestServices.GetRequiredService<ILogger<AGUIServerSentEventsResult>>();
            await new AGUIServerSentEventsResult(events, sseLogger).ExecuteAsync(context).ConfigureAwait(false);
        });
    }
}
