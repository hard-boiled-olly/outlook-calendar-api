using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Models.Messages;
using OutlookCalendarApi.Models.Claude;

namespace OutlookCalendarApi.Services;

public record ConversationMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content
);

public record InterviewStepResult(
    bool IsComplete,
    string? NextQuestion,
    InterviewOutput? Output,
    MilestoneSprintOutput? MilestoneSprintOutput,
    string UpdatedHistoryJson
);

public class InterviewService
{
    private readonly AnthropicClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public InterviewService()
    {
        _client = new AnthropicClient();
    }

    public async Task<(string FirstQuestion, string HistoryJson)> StartAsync()
    {
        var kickoff = new ConversationMessage("user", "[START]");

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = "claude-sonnet-4-6",
            MaxTokens = 1024,
            System = IdentitySummitSystemPrompt,
            Messages = [new() { Role = Role.User, Content = kickoff.Content }]
        });

        var firstQuestion = ExtractText(response);

        var history = new List<ConversationMessage>
        {
            kickoff,
            new("assistant", firstQuestion)
        };

        return (firstQuestion, JsonSerializer.Serialize(history, JsonOptions));
    }

    public async Task<(string FirstQuestion, string HistoryJson)> StartMilestoneSprintAsync(
        string identityStatement, string summitDescription, string proofCriteria, string? targetDate)
    {
        var context = $"""
            [START]
            Context from confirmed identity and goal:
            - Identity: {identityStatement}
            - Goal: {summitDescription}
            - Proof criteria: {proofCriteria}
            - Target date: {targetDate ?? "not set"}
            """;

        var kickoff = new ConversationMessage("user", context);

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = "claude-sonnet-4-6",
            MaxTokens = 1024,
            System = MilestoneSprintSystemPrompt,
            Messages = [new() { Role = Role.User, Content = kickoff.Content }]
        });

        var firstQuestion = ExtractText(response);

        var history = new List<ConversationMessage>
        {
            kickoff,
            new("assistant", firstQuestion)
        };

        return (firstQuestion, JsonSerializer.Serialize(history, JsonOptions));
    }

    public async Task<InterviewStepResult> ProcessResponseAsync(string type, string historyJson, string userMessage)
    {
        var systemPrompt = type == "milestone-sprint"
            ? MilestoneSprintSystemPrompt
            : IdentitySummitSystemPrompt;

        var history = JsonSerializer.Deserialize<List<ConversationMessage>>(historyJson, JsonOptions)!;
        history.Add(new ConversationMessage("user", userMessage));

        var messages = history.Select(m => new MessageParam
        {
            Role = m.Role == "user" ? Role.User : Role.Assistant,
            Content = m.Content
        }).ToList();

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = "claude-sonnet-4-6",
            MaxTokens = 1024,
            System = systemPrompt,
            Messages = messages
        });

        var assistantText = ExtractText(response);
        history.Add(new ConversationMessage("assistant", assistantText));
        var updatedHistoryJson = JsonSerializer.Serialize(history, JsonOptions);

        if (!assistantText.Contains("[[INTERVIEW_COMPLETE]]"))
        {
            return new InterviewStepResult(false, assistantText, null, null, updatedHistoryJson);
        }

        var cleanedText = assistantText.Replace("[[INTERVIEW_COMPLETE]]", "").Trim();
        history[^1] = new ConversationMessage("assistant", cleanedText);
        updatedHistoryJson = JsonSerializer.Serialize(history, JsonOptions);

        if (type == "milestone-sprint")
        {
            var msOutput = await ExtractMilestoneSprintOutputAsync(history);
            return new InterviewStepResult(true, null, null, msOutput, updatedHistoryJson);
        }

        var output = await ExtractStructuredOutputAsync(history);
        return new InterviewStepResult(true, null, output, null, updatedHistoryJson);
    }

    private async Task<InterviewOutput> ExtractStructuredOutputAsync(List<ConversationMessage> history)
    {
        var messages = history.Select(m => new MessageParam
        {
            Role = m.Role == "user" ? Role.User : Role.Assistant,
            Content = m.Content
        }).ToList();

        messages.Add(new MessageParam
        {
            Role = Role.User,
            Content = "Please extract the structured data from our conversation."
        });

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = "claude-sonnet-4-6",
            MaxTokens = 1024,
            System = IdentitySummitSystemPrompt,
            Messages = messages,
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat
                {
                    Schema = ParseSchema(ExtractionSchema)
                }
            }
        });

        return JsonSerializer.Deserialize<InterviewOutput>(ExtractText(response), JsonOptions)!;
    }

    private async Task<MilestoneSprintOutput> ExtractMilestoneSprintOutputAsync(List<ConversationMessage> history)
    {
        var messages = history.Select(m => new MessageParam
        {
            Role = m.Role == "user" ? Role.User : Role.Assistant,
            Content = m.Content
        }).ToList();

        messages.Add(new MessageParam
        {
            Role = Role.User,
            Content = "Please extract the structured milestone and sprint plan from our conversation."
        });

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = "claude-sonnet-4-6",
            MaxTokens = 2048,
            System = MilestoneSprintSystemPrompt,
            Messages = messages,
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat
                {
                    Schema = ParseSchema(MilestoneSprintExtractionSchema)
                }
            }
        });

        return JsonSerializer.Deserialize<MilestoneSprintOutput>(ExtractText(response), JsonOptions)!;
    }

    private static string ExtractText(Message response)
    {
        if (response.Content[0].TryPickText(out var textBlock))
            return textBlock.Text;
        throw new InvalidOperationException("Expected text response from Claude");
    }

    private static IReadOnlyDictionary<string, JsonElement> ParseSchema(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    // ── System Prompt ───────────────────────────────────────────────────

    private const string IdentitySummitSystemPrompt = """
        You are helping someone get clear on a goal they want to achieve. Your job is to
        ask specific questions to understand what they want, why it matters, what success
        looks like, and when they want to get there.

        Guidelines:
        - Be casual and direct — like a smart friend who's genuinely curious, not a life coach
        - Ask one question at a time
        - Build on what they've already told you — never ask about something they've already covered
        - Aim for 4-8 questions total. Don't pad it out, but don't rush past important context
        - When forming their identity statement, use plain, relatable language:
          "I'm someone who handles their money well" not "I'm becoming someone who cultivates
          financial sovereignty and makes intentional decisions aligned with my future self"
        - For proof criteria, ask specific questions — don't assume. For a savings goal: where
          will the money be, how will they know they've hit it? For fitness: what specifically
          can they do that they can't do now?
        - Don't ask about their timeline — if they mention a target date or deadline naturally,
          great, but it's not something to probe for

        When you have enough information, present a natural summary covering:
        - Their identity statement ("I'm someone who...")
        - Their summit goal (specific and concrete)
        - How they'll prove it (proof criteria)
        - When they want to achieve it (if discussed)

        For each element, briefly note which part of the conversation it came from.
        End your summary message with [[INTERVIEW_COMPLETE]] on its own line.

        If the message is "[START]", introduce yourself briefly and ask the first question.

        When asked to extract structured data, return the JSON matching the provided schema
        based on everything discussed in the conversation.
        """;

    // ── Milestone-Sprint System Prompt ──────────────────────────────────

    private static readonly string MilestoneSprintSystemPrompt = """
        You are helping someone plan the path to a goal they've already defined. You'll receive
        their identity statement, summit goal, proof criteria, and target date as context.

        Your job is to understand where they are RIGHT NOW, then collaboratively build milestones
        and a first sprint plan.

        Guidelines:
        - Be casual and direct — same tone as a smart friend helping plan, not a project manager
        - Ask one question at a time
        - Build on what they've already told you — never re-ask
        - Start with "Where are you at with this right now?" to understand their current status
        - Ask 5-8 questions total. Focus on the critical unknowns:
          - Current status and experience level
          - What resources/access they already have
          - What's worked or not worked before (if they've tried)
          - Realistic weekly time commitment
        - Schedule preferences for calendar placement — weave these in naturally, don't make it
          feel like a form:
          - "What are your typical working hours?" (just get a sense of their day)
          - "When would you prefer to do [activity]? Morning, afternoon, or evening?"
          If they've already mentioned timing preferences, don't re-ask.
        - Based on their answers, propose milestones collaboratively:
          "Based on where you are, I'd suggest starting with..." then ask for feedback
        - Keep habits minimal: 1-2 per sprint. Each must be justified by the conversation.
        - Keep tasks minimal: only concrete actions needed for the first milestone
        - Tasks should be specific ("Set up a savings account at X") not aspirational ("Research options")
        - Space milestones proportionally between now and the target date
        - Present the complete plan before finishing:
          - Milestones with target dates
          - First sprint: habits (with frequency and what exactly to do) and tasks (with deadlines)
          - Scheduling preferences: working hours and preferred times for activities
        - Ask "Any of this feel off? Want to adjust anything?" before completing
        - If they want changes, adjust and re-present

        When the user confirms the plan, present a final summary and end with [[INTERVIEW_COMPLETE]]
        on its own line.

        If the message starts with "[START]", read the context provided and ask your first question.

        When asked to extract structured data, return the JSON matching the provided schema
        based on everything discussed in the conversation.

        IMPORTANT: Today's date is {today}. Use this for calculating milestone target dates.
        """.Replace("{today}", DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));

    // ── Extraction Schema ───────────────────────────────────────────────

    private const string ExtractionSchema = """
        {
            "type": "object",
            "properties": {
                "identity_statement": {
                    "type": "string",
                    "description": "The identity statement in the form 'I'm someone who...'"
                },
                "summit_description": {
                    "type": "string",
                    "description": "The specific, concrete summit goal"
                },
                "proof_criteria": {
                    "type": "string",
                    "description": "How they will prove the goal is achieved"
                },
                "target_date": {
                    "type": "string",
                    "description": "ISO 8601 date string (YYYY-MM-DD) if a date was discussed, otherwise null"
                },
                "summary_breakdown": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "component": { "type": "string" },
                            "based_on": { "type": "string" }
                        },
                        "required": ["component", "based_on"],
                        "additionalProperties": false
                    }
                }
            },
            "required": ["identity_statement", "summit_description", "proof_criteria", "summary_breakdown"],
            "additionalProperties": false
        }
        """;

    // ── Milestone-Sprint Extraction Schema ────────────────────────────────

    private const string MilestoneSprintExtractionSchema = """
        {
            "type": "object",
            "properties": {
                "milestones": {
                    "type": "array",
                    "description": "Progressive milestones from current status to summit goal",
                    "items": {
                        "type": "object",
                        "properties": {
                            "description": { "type": "string", "description": "What this milestone achieves" },
                            "proof_criteria": { "type": "string", "description": "How to prove this milestone is done" },
                            "target_date": { "type": "string", "description": "ISO 8601 date (YYYY-MM-DD)" },
                            "sort_order": { "type": "integer", "description": "1-based order, 1 = first milestone" }
                        },
                        "required": ["description", "proof_criteria", "target_date", "sort_order"],
                        "additionalProperties": false
                    }
                },
                "first_sprint_habits": {
                    "type": "array",
                    "description": "1-2 recurring habits for the first sprint",
                    "items": {
                        "type": "object",
                        "properties": {
                            "name": { "type": "string", "description": "Short habit name" },
                            "frequency": { "type": "string", "description": "e.g. 'daily', '3x per week'" },
                            "prescription": { "type": "string", "description": "Exactly what to do each session" },
                            "duration_mins": { "type": "integer", "description": "Minutes per session" }
                        },
                        "required": ["name", "frequency", "prescription", "duration_mins"],
                        "additionalProperties": false
                    }
                },
                "first_sprint_tasks": {
                    "type": "array",
                    "description": "Concrete one-off tasks for the first sprint",
                    "items": {
                        "type": "object",
                        "properties": {
                            "name": { "type": "string", "description": "Short task name" },
                            "description": { "type": "string", "description": "What exactly to do" },
                            "deadline": { "type": "string", "description": "ISO 8601 date (YYYY-MM-DD)" },
                            "duration_mins": { "type": "integer", "description": "Estimated minutes to complete" }
                        },
                        "required": ["name", "description", "deadline", "duration_mins"],
                        "additionalProperties": false
                    }
                },
                "plan_breakdown": {
                    "type": "array",
                    "description": "Links each plan element to what the user said",
                    "items": {
                        "type": "object",
                        "properties": {
                            "component": { "type": "string" },
                            "based_on": { "type": "string" }
                        },
                        "required": ["component", "based_on"],
                        "additionalProperties": false
                    }
                },
                "scheduling_preferences": {
                    "type": "object",
                    "description": "User's scheduling preferences extracted from the conversation. Null/omitted if not discussed.",
                    "properties": {
                        "working_hours_start": { "type": "string", "description": "HH:mm format, e.g. '09:00'" },
                        "working_hours_end": { "type": "string", "description": "HH:mm format, e.g. '17:30'" },
                        "preferred_times": {
                            "type": "array",
                            "description": "Activity to preferred time slot mappings",
                            "items": {
                                "type": "object",
                                "properties": {
                                    "activity": { "type": "string", "description": "Activity description" },
                                    "time_slot": { "type": "string", "description": "'morning', 'afternoon', or 'evening'" }
                                },
                                "required": ["activity", "time_slot"],
                                "additionalProperties": false
                            }
                        }
                    },
                    "required": ["working_hours_start", "working_hours_end", "preferred_times"],
                    "additionalProperties": false
                }
            },
            "required": ["milestones", "first_sprint_habits", "first_sprint_tasks", "plan_breakdown"],
            "additionalProperties": false
        }
        """;
}
