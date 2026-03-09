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

    public async Task<InterviewStepResult> ProcessResponseAsync(string historyJson, string userMessage)
    {
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
            System = IdentitySummitSystemPrompt,
            Messages = messages
        });

        var assistantText = ExtractText(response);
        history.Add(new ConversationMessage("assistant", assistantText));
        var updatedHistoryJson = JsonSerializer.Serialize(history, JsonOptions);

        if (!assistantText.Contains("[[INTERVIEW_COMPLETE]]"))
        {
            return new InterviewStepResult(false, assistantText, null, updatedHistoryJson);
        }

        var cleanedText = assistantText.Replace("[[INTERVIEW_COMPLETE]]", "").Trim();
        history[^1] = new ConversationMessage("assistant", cleanedText);
        updatedHistoryJson = JsonSerializer.Serialize(history, JsonOptions);

        var output = await ExtractStructuredOutputAsync(history);

        return new InterviewStepResult(true, null, output, updatedHistoryJson);
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
}
