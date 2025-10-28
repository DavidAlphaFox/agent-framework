# Invoke Azure Response

### Overview

The `InvokeAzureResponse` action is intended to support the ability to explicitly prompt the user for input during a workflow execution. 

Note, the definition of the action corresponds direclty with `InvokeAzureAgent` in all ways other than how the agent is specified:

1. For `InvokeAzureAgent`, the agent is specified by name (and optionally version).
1. For `InvokeAzureResponse`, the agent definition is specified inline and not persisted.

The definition for the agent corresponds directly with the `PromptAgent` definition being used by declarative agents.
The `PromptAgent` definition is currently in flight and can be viewed in the active PR 
[Final State of Foundry Alignment for Prompt Agent](https://msazure.visualstudio.com/CCI/_git/ObjectModel/pullrequest/13793226).

Due to the alignment between `InvokeAzureResponse` and `InvokeAzureAgent`, a shared base type `InvokeAgentBase` will be defined to reduce duplication.

### Definition

```yaml
- kind: InvokeAzureResponse
  id: invoke_response_1
  conversationId: conv_123xyz
  agent:
    model: 
      id: gpt-5
      options:
        temperature: 0
    instructions: Be awesome!
    capabilities:
      codeInterpeter: true      
  input:
    messages: =UserMessage("Hi!")
    parameters:
      location: Seattle, WA, USA
      now: =System.CurrentDateTime
  output:
    autoSend: true
    messages: Local.AgentResponse
    responseObject: Local.AgentResponseObject
    structuredOutputs: Local.AgentStructuredOutputs
  externalLoop:
    variable: Local.UserInput
    when: =!Local.IsComplete
    maxIterations: 4
```

Since the `InvokeAzureResponse` action inherits from `InvokeAgentBase`, please refer to the `InvokeAzureAgent` action for details on all other properties.
The following table summarizes the properties specific to `PromptAgent`:

Property|Type|Description|Required|Default
--|--|--|--|--
`model`|`Model`|
`instructions`|`Template`|
`additionalInstructions`|`Template`|
`tools`|`CollectionProperty<AgentTool>`|
`capabilities`|`GptCapabilities`|
`settings`|`AISettings`|
`...`|`TBD`
