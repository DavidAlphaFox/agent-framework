// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Agents.AI.AGUI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.AI.Hosting.AGUI.AspNetCore.IntegrationTests;

public sealed class BasicStreamingTests : IAsyncDisposable
{
    private WebApplication? _app;
    private HttpClient? _client;

    [Fact]
    public async Task ClientReceivesStreamedAssistantMessageAsync()
    {
        // Arrange
        await this.SetupTestServerAsync();
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.CreateAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        AgentThread thread = agent.GetNewThread();
        ChatMessage userMessage = new(ChatRole.User, "hello");

        List<AgentRunResponseUpdate> updates = [];

        // Act
        await foreach (AgentRunResponseUpdate update in agent.RunStreamingAsync([userMessage], thread, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert
        updates.Should().NotBeEmpty();
        updates.Should().AllSatisfy(u => u.Role.Should().Be(ChatRole.Assistant));

        // Verify the assistant message was received
        string fullText = string.Concat(updates.Where(u => !string.IsNullOrEmpty(u.Text)).Select(u => u.Text));
        fullText.Should().Be("Hello from fake agent!");
    }

    [Fact]
    public async Task ClientReceivesRunLifecycleEventsAsync()
    {
        // Arrange
        await this.SetupTestServerAsync();
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.CreateAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        AgentThread thread = agent.GetNewThread();
        ChatMessage userMessage = new(ChatRole.User, "test");

        List<AgentRunResponseUpdate> updates = [];

        // Act
        await foreach (AgentRunResponseUpdate update in agent.RunStreamingAsync([userMessage], thread, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert - RunStarted should be the first update
        updates.Should().NotBeEmpty();
        updates[0].ResponseId.Should().NotBeNullOrEmpty();
        ChatResponseUpdate firstUpdate = updates[0].AsChatResponseUpdate();
        string? threadId = firstUpdate.ConversationId;
        string? runId = updates[0].ResponseId;
        threadId.Should().NotBeNullOrEmpty();
        runId.Should().NotBeNullOrEmpty();

        // Should have received text updates
        updates.Should().Contain(u => !string.IsNullOrEmpty(u.Text));

        // All text content updates should have the same message ID
        List<AgentRunResponseUpdate> textUpdates = updates.Where(u => !string.IsNullOrEmpty(u.Text)).ToList();
        textUpdates.Should().NotBeEmpty();
        string? firstMessageId = textUpdates.FirstOrDefault()?.MessageId;
        firstMessageId.Should().NotBeNullOrEmpty();
        textUpdates.Should().AllSatisfy(u => u.MessageId.Should().Be(firstMessageId));

        // RunFinished should be the last update
        AgentRunResponseUpdate lastUpdate = updates[^1];
        lastUpdate.ResponseId.Should().Be(runId);
        ChatResponseUpdate lastChatUpdate = lastUpdate.AsChatResponseUpdate();
        lastChatUpdate.ConversationId.Should().Be(threadId);
    }

    [Fact]
    public async Task RunAsyncAggregatesStreamingUpdatesAsync()
    {
        // Arrange
        await this.SetupTestServerAsync();
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.CreateAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        AgentThread thread = agent.GetNewThread();
        ChatMessage userMessage = new(ChatRole.User, "hello");

        // Act
        AgentRunResponse response = await agent.RunAsync([userMessage], thread, new AgentRunOptions(), CancellationToken.None);

        // Assert
        response.Messages.Should().NotBeEmpty();
        response.Messages.Should().Contain(m => m.Role == ChatRole.Assistant);
        response.Messages.Should().Contain(m => m.Text == "Hello from fake agent!");
    }

    [Fact]
    public async Task MultiTurnConversationPreservesAllMessagesInThreadAsync()
    {
        // Arrange
        await this.SetupTestServerAsync();
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.CreateAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        AgentThread thread = agent.GetNewThread();
        ChatMessage firstUserMessage = new(ChatRole.User, "First question");

        // Act - First turn
        List<AgentRunResponseUpdate> firstTurnUpdates = [];
        await foreach (AgentRunResponseUpdate update in agent.RunStreamingAsync([firstUserMessage], thread, new AgentRunOptions(), CancellationToken.None))
        {
            firstTurnUpdates.Add(update);
        }

        // Assert first turn completed
        firstTurnUpdates.Should().Contain(u => !string.IsNullOrEmpty(u.Text));

        // Act - Second turn with another message
        ChatMessage secondUserMessage = new(ChatRole.User, "Second question");
        List<AgentRunResponseUpdate> secondTurnUpdates = [];
        await foreach (AgentRunResponseUpdate update in agent.RunStreamingAsync([secondUserMessage], thread, new AgentRunOptions(), CancellationToken.None))
        {
            secondTurnUpdates.Add(update);
        }

        // Assert second turn completed
        secondTurnUpdates.Should().Contain(u => !string.IsNullOrEmpty(u.Text));

        // Verify both responses were received
        string firstTurnText = string.Concat(firstTurnUpdates.Where(u => !string.IsNullOrEmpty(u.Text)).Select(u => u.Text));
        string secondTurnText = string.Concat(secondTurnUpdates.Where(u => !string.IsNullOrEmpty(u.Text)).Select(u => u.Text));

        firstTurnText.Should().Be("Hello from fake agent!");
        secondTurnText.Should().Be("Hello from fake agent!");
    }

    [Fact]
    public async Task AgentSendsMultipleMessagesInOneTurnAsync()
    {
        // Arrange
        await this.SetupTestServerAsync(useMultiMessageAgent: true);
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.CreateAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        AgentThread thread = agent.GetNewThread();
        ChatMessage userMessage = new(ChatRole.User, "Tell me a story");

        List<AgentRunResponseUpdate> updates = [];

        // Act
        await foreach (AgentRunResponseUpdate update in agent.RunStreamingAsync([userMessage], thread, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert - Should have received text updates with different message IDs
        List<AgentRunResponseUpdate> textUpdates = updates.Where(u => !string.IsNullOrEmpty(u.Text)).ToList();
        textUpdates.Should().NotBeEmpty();

        // Extract unique message IDs
        List<string> messageIds = textUpdates.Select(u => u.MessageId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList()!;
        messageIds.Should().HaveCountGreaterThan(1, "agent should send multiple messages");

        // Verify content of all messages
        string fullText = string.Concat(textUpdates.Select(u => u.Text));
        fullText.Should().Contain("First message");
        fullText.Should().Contain("Second message");
        fullText.Should().Contain("Third message");
    }

    [Fact]
    public async Task UserSendsMultipleMessagesAtOnceAsync()
    {
        // Arrange
        await this.SetupTestServerAsync();
        var chatClient = new AGUIChatClient(this._client!, "", null);
        AIAgent agent = chatClient.CreateAIAgent(instructions: null, name: "assistant", description: "Sample assistant", tools: []);
        AgentThread thread = agent.GetNewThread();

        // Multiple user messages sent in one turn
        ChatMessage[] userMessages =
        [
            new ChatMessage(ChatRole.User, "First part of question"),
            new ChatMessage(ChatRole.User, "Second part of question"),
            new ChatMessage(ChatRole.User, "Third part of question")
        ];

        List<AgentRunResponseUpdate> updates = [];

        // Act
        await foreach (AgentRunResponseUpdate update in agent.RunStreamingAsync(userMessages, thread, new AgentRunOptions(), CancellationToken.None))
        {
            updates.Add(update);
        }

        // Assert - Should have received assistant response
        updates.Should().Contain(u => !string.IsNullOrEmpty(u.Text));

        // Verify the complete assistant response
        string fullText = string.Concat(updates.Where(u => !string.IsNullOrEmpty(u.Text)).Select(u => u.Text));
        fullText.Should().Be("Hello from fake agent!");
    }

    private async Task SetupTestServerAsync(bool useMultiMessageAgent = false)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        this._app = builder.Build();

        if (useMultiMessageAgent)
        {
            this._app.MapAGUIAgent("/agent", (IEnumerable<ChatMessage> messages, IEnumerable<AITool> tools) =>
            {
                var fakeChatClient = new FakeMultiMessageChatClient();
                return fakeChatClient.CreateAIAgent(instructions: null, name: "fake-multi-message-agent", description: "A fake agent that sends multiple messages for testing", tools: []);
            });
        }
        else
        {
            this._app.MapAGUIAgent("/agent", (IEnumerable<ChatMessage> messages, IEnumerable<AITool> tools) =>
            {
                var fakeChatClient = new FakeChatClient();
                return fakeChatClient.CreateAIAgent(instructions: null, name: "fake-agent", description: "A fake agent for testing", tools: []);
            });
        }

        await this._app.StartAsync();

        TestServer testServer = this._app.Services.GetRequiredService<IServer>() as TestServer
            ?? throw new InvalidOperationException("TestServer not found");

        this._client = testServer.CreateClient();
        this._client.BaseAddress = new Uri("http://localhost/agent");
    }

    public async ValueTask DisposeAsync()
    {
        this._client?.Dispose();
        if (this._app != null)
        {
            await this._app.DisposeAsync();
        }
    }
}

internal sealed class FakeChatClient : IChatClient
{
    public ChatClientMetadata Metadata => new("fake-chat-client");

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string messageId = Guid.NewGuid().ToString("N");

        // Simulate streaming a deterministic response
        foreach (string chunk in new[] { "Hello", " ", "from", " ", "fake", " ", "agent", "!" })
        {
            yield return new ChatResponseUpdate
            {
                MessageId = messageId,
                Role = ChatRole.Assistant,
                Contents = [new TextContent(chunk)]
            };

            await Task.Yield();
        }
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
}

internal sealed class FakeMultiMessageChatClient : IChatClient
{
    public ChatClientMetadata Metadata => new("fake-multi-message-chat-client");

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Simulate sending first message
        string messageId1 = Guid.NewGuid().ToString("N");
        foreach (string chunk in new[] { "First", " ", "message" })
        {
            yield return new ChatResponseUpdate
            {
                MessageId = messageId1,
                Role = ChatRole.Assistant,
                Contents = [new TextContent(chunk)]
            };

            await Task.Yield();
        }

        // Simulate sending second message
        string messageId2 = Guid.NewGuid().ToString("N");
        foreach (string chunk in new[] { "Second", " ", "message" })
        {
            yield return new ChatResponseUpdate
            {
                MessageId = messageId2,
                Role = ChatRole.Assistant,
                Contents = [new TextContent(chunk)]
            };

            await Task.Yield();
        }

        // Simulate sending third message
        string messageId3 = Guid.NewGuid().ToString("N");
        foreach (string chunk in new[] { "Third", " ", "message" })
        {
            yield return new ChatResponseUpdate
            {
                MessageId = messageId3,
                Role = ChatRole.Assistant,
                Contents = [new TextContent(chunk)]
            };

            await Task.Yield();
        }
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
}