# Request External Input 

### Overview

The `RequestExternalInput` action is intended to support the ability to explicitly prompt the user 
or calling workflow for input during a workflow execution. 
The input is always in response and within the context of the workflow (external) conversation.

The `InvokeAzureAgent` action supports a streamlined patterns for requesting input (90%), but there are scenarios where a more explicit approach is needed (10%).

_CopilotStudio_ defines a `Question` action that can be used to request input, but it is always associated with a specific question (or prompt)
and doesn't align well with a responding to an agent's message (where an additional question/prompt is redundant and not desired.

Note: As with all input, the system will automatically capture input to the `System.LastMessage` variable.

Input shall be provided in the form of a message (`ChatMessage`), which can include text, attachments, or other supported message content.


### Definition

```yaml
- kind: RequestExternalInput
  id: user_input_1
  variable: Local.UserInput
```

Property|Type|Description|Required|Default
--|--|--|--|--
`kind`|`string`|The kind of action. Must be `RequestExternalInput`.|Required
`id`|`string`|The unique identifier for the action.|Required
`variable`|`PropertyPath`|The scoped variable to store the input.|Optional


### Usage

#### 1. Workflow Conversation

When responding within the context of the workflow (external) conversation, 
the `RequestExternalInput` action can be used without specifying the `conversationId` or capturing the actual input:

```yaml
- kind: InvokeAzureAgent
  id: invoke_agent_1
  conversationId: =System.ConversationId
  agent:
    name: DemoAgent

- kind: RequestExternalInput
  id: user_input_1

- kind: InvokeAzureAgent
  id: invoke_agent_2
  conversationId: =System.ConversationId
  agent:
    name: DemoAgent
```


#### 2. Examine Input

The user input can be examined to determine the next step in the workflow.

This can be accomplished using `System.LastMessage`:

```yaml
- kind: InvokeAzureAgent
  id: invoke_agent_1
  conversationId: =System.ConversationId
  agent:
    name: DemoAgent

- kind: RequestExternalInput
  id: user_input_1

- kind: ConditionGroup
  id: check_completion
  conditions:

    - condition: ==!IsBlank(Find("DONE", Upper(System.LastMessage.Text))
      id: check_user_input
      actions:
    
        - kind: GotoAction
          id: goto_invoke_agent
          actionId: invoke_agent_1
```

Or a custom variable (`Local.UserInput`) if the response needs to be preserved:

```yaml
- kind: InvokeAzureAgent
  id: invoke_agent_1
  conversationId: =System.ConversationId
  agent:
    name: DemoAgent

- kind: RequestExternalInput
  id: user_input_1
  variable: Local.UserInput

- kind: ConditionGroup
  id: check_completion
  conditions:

    - condition: ==!IsBlank(Find("DONE", Upper(Local.UserInput.Text))
      id: check_user_input
      actions:
    
        - kind: GotoAction
          id: goto_invoke_agent
          actionId: invoke_agent_1
```

