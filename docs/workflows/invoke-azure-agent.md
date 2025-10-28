# Invoke Azure Agent

### Overview

The `InvokeAzureAgent` action is used to invoke an Azure Agent within a workflow.

This update enhances the existing `InvokeAzureAgent` action to support additional features available in the V2 Agents API,
including structured inputs and outputs, as well as response formatting options.

### Definition

#### Current

The current version of `InvokeAzureAgent` was pared down to only support basic message input/output when the V2 Agents API was introduced.

```yaml
- kind: InvokeAzureAgent
  id: invoke_agent_1
  conversationId: =System.ConversationId
  agent:
    name: asst_abc123
  input:
    additionalInstructions: |-
      The user is located in Seattle, Washington, USA.
    messages: =UserMessage("Hi!")
  output:
    autoSend: true
    messages: =Local.AgentResponse
```

#### Proposed

In addition to removing `additionalInstructions` (which are not supported by the V2 Agents API),
we are adding support for V2 features _Structured Inputs_ & _Structured Outputs__ 
as well as re-adding support for _Response Format_ that is supported in existing APIs.

```yaml
- kind: InvokeAzureAgent
  id: invoke_agent_1
  conversationId: =System.ConversationId
  agent:
    name: DemoAgent
    version: 5
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

Property|Type|Description|Required|Default
--|--|--|--|--
`kind`|`string`|The kind of action. Must be `InvokeAzureAgent`.|Required
`id`|`string`|The unique identifier for the action.|Required
`conversationId`|`StringExpression`|Identifies the conversation in which agent will participate.|Optional
`agent.name`|`StringExpression`|Identifies the Azure Agent to invoke.|Required
`agent.version`|`StringExpression`|Specifies the version of the agent to invoke.|Optional|Latest
`input`|`Node`|Defines aspects related to the agent inputs.|Optional
`input.messages`|`Value`|The messages to send to the agent.|Optional
`input.parameters`|`CollectionProperty<StructuredInput>`|Structured inputs being provided to the agent.|Optional
`output`|`Node`|Defines aspects related to the agent response/output.|Optional
`output.autoSend`|`BoolExpression`|Indicates whether to automatically include the agent response as part of the workflow (external) conversation.  If `conversationId` property identifies the workflow conversation, setting this to `false` has no impact.|Optional|
`output.messages`|`PropertyPath`|The scoped variable to store the agent response messages.|Optional
`output.responseObject`|`PropertyPath`|The scoped variable to store the structured response object.  Only assigned when for agent with response format of `json_object` or `json_schema`|Optional
`output.structuredOutputs`|`PropertyPath`|Mapping of structured output parameters from the agent to scoped variables.|Optional
`externalLoop`|`Node`|Defines if external input is needed.|Optional
`externalLoop.when`|`BoolExpression`|Expression that evaluates to true when input is needed.|Required
`externalLoop.variable`|`PropertyPath`|The scoped variable to store input.|Optional
`externalLoop.maxIterations`|`IntExpression`|Maximum number of iterations to allow.|Optional|No limit

**StructuredInput**:

Property|Type|Description|Required|Default
--|--|--|--|--
`name`|`String`|The name of the structured input parameter.|Required
`value`|`ValueExpression`|The value of the structured input parameter.|Required


### Usage

#### 1. Response Format - JSON Schema

JSON schema can be part of the agent definition in order to provide a structured agent response.
The response is provided as an object (record) in a scoped variable.
The raw message is still available via the `messages` output property.

```yaml
- kind: InvokeAzureAgent
  id: invoke_agent_1
  agent:
    name: DemoAgent
  output:
    messages: Local.AgentResponse
    responseObject: Local.AgentResponseObject

- kind: ConditionGroup
  id: check_completion
  conditions:

    - condition: =!IsBlank(Local.BestResult) And Local.AgentResponseObject.confidence > Local.BestResult.confidence
      id: check_best_result
      actions:
    
        - kind: SetVariable
          id: set_best_result
          variable: Local.BestResult
          value: =Local.AgentResponseObject
```

#### 2. Structured Inputs

Values for structured inputs can be provided to the agent as literal values or expressions.

```yaml
- kind: InvokeAzureAgent
  id: invoke_agent_1
  agent:
    name: DemoAgent
  input:
    messages: =UserMessage("Where a good place to hike?")
    parameters:
      location: Seattle, WA, USA
      now: =System.CurrentDateTime
  output:
    messages: Local.AgentResponse
```

#### 3. Structured Outputs

Values for structured outputs can be captured to scoped variables.

```yaml
- kind: InvokeAzureAgent
  id: invoke_agent_1
  conversationId: =System.ConversationId
  agent:
    name: DemoAgent
  output:
    structuredOutputs: Local.StructuredOutputs
```

#### 4. External Input - No limit

The criteria for soliciting input can be based on the agent response (e.g. low confidence).
When `maxIterations` is not specified, no limit is enforced.

```yaml
- kind: InvokeAzureAgent
  id: invoke_agent_1
  agent:
    name: DemoAgent
  externalLoop:
    when: =Local.IntentConfidence < 0.8
```

#### 5. External Input - Limit Iterations

Criteria for soliciting input may also be based on the resulting input.

```yaml
- kind: InvokeAzureAgent
  id: invoke_agent_1
  agent:
    name: DemoAgent
  externalLoop:
    variable: Local.UserInput
    when: =!IsBlank(Find("APPROVED", Upper(Local.UserInput.Text)))
    maxIterations: =Env.MaximumInputRequests
```
