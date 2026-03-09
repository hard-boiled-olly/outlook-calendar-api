using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using OutlookCalendarApi.Models.Claude;

namespace OutlookCalendarApi.Services;

public class ClaudeService
{
    private readonly AnthropicClient _client;

    public ClaudeService()
    {
        // Reads ANTHROPIC_API_KEY from environment variable (set by Aspire)
        _client = new AnthropicClient();
    }

    private static IReadOnlyDictionary<string, JsonElement> ParseSchema(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    private static string ExtractText(Message response)
    {
        if (response.Content[0].TryPickText(out var textBlock))
            return textBlock.Text;
        throw new InvalidOperationException("Expected text response from Claude");
    }

    public async Task<SprintPlanResponse> GenerateSprintPlanAsync(
        string identityStatement, string summitGoal, string milestoneDescription,
        string milestoneProofCriteria, string? previousReflection)
    {
        var userMessage = $"""
            My identity: {identityStatement}

            Summit goal: {summitGoal}

            Current milestone: {milestoneDescription}

            Milestone proof criteria: {milestoneProofCriteria}
            """;

        if (!string.IsNullOrWhiteSpace(previousReflection))
        {
            userMessage += $"\n\nReflection from previous sprint: {previousReflection}";
        }

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = "claude-haiku-4-5-20251001",
            MaxTokens = 2048,
            System = SprintPlanSystemPrompt,
            Messages = [new()
            {
                Role = Role.User,
                Content = userMessage,
            }],
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat
                {
                    Schema = ParseSchema(SprintPlanSchema),
                },
            },
        });

        return JsonSerializer.Deserialize<SprintPlanResponse>(ExtractText(response))!;
    }

    public async Task<ReplanResponse> ReplanAsync(
        string identityStatement, string summitGoal,
        string completedMilestone, string nextMilestone, string nextMilestoneProofCriteria,
        string currentHabitsDescription, string reflection)
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = "claude-haiku-4-5-20251001",
            MaxTokens = 2048,
            System = ReplanSystemPrompt,
            Messages = [new()
            {
                Role = Role.User,
                Content = $"""
                    My identity: {identityStatement}

                    Summit goal: {summitGoal}

                    Milestone just proved: {completedMilestone}

                    Next milestone: {nextMilestone}

                    Next milestone proof criteria: {nextMilestoneProofCriteria}

                    Current habits: {currentHabitsDescription}

                    My reflection: {reflection}
                    """,
            }],
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat
                {
                    Schema = ParseSchema(ReplanSchema),
                },
            },
        });

        return JsonSerializer.Deserialize<ReplanResponse>(ExtractText(response))!;
    }

    // ── System Prompts ──────────────────────────────────────────────────

    private const string SprintPlanSystemPrompt = """
        You are a goal achievement coach specialising in practical habit and task planning.

        The user will share their identity, summit goal, and current milestone to work towards.
        They may also include a reflection from a previous sprint if they are replanning.

        Your job is to create a concrete sprint plan with:

        1. **Habits** — recurring behaviours that build the capability needed for the milestone.
           Each habit needs:
           - A clear name
           - A frequency (e.g. "3x per week", "daily")
           - A specific prescription — exactly what to do in each session (the "dose")
           - An estimated duration in minutes per session

        2. **Tasks** — one-off actions that support progress. Each task needs:
           - A clear name
           - A brief description
           - Suggested timing as days from sprint start
           - An estimated duration in minutes

        Keep the plan manageable: 2-4 habits and 2-5 tasks is ideal. The user should feel
        challenged but not overwhelmed. If there's a previous reflection, adjust the plan
        based on what worked and what didn't.
        """;

    private const string ReplanSystemPrompt = """
        You are a goal achievement coach helping a user progress after proving a milestone.

        The user has just proved a milestone and is moving on to the next one. They'll share:
        - Their identity and summit goal
        - The milestone they just proved
        - The next milestone to work towards
        - Their current habits and prescriptions
        - A reflection on what worked and what didn't

        Your job is to:
        1. Update habit prescriptions — increase the "dose" to match the higher demands of
           the next milestone. Keep the same habits but make them progressively harder.
        2. Create new one-off tasks if needed for the next milestone.
        3. Write a brief coaching note that acknowledges progress and motivates for the next phase.

        Return each updated prescription with the habit_id so the system can match them.
        Only include habits that need changes — omit habits whose prescription stays the same.
        """;

    // ── JSON Schemas ────────────────────────────────────────────────────

    private const string SprintPlanSchema = """
        {
            "type": "object",
            "properties": {
                "habits": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "name": {
                                "type": "string",
                                "description": "Name of the habit"
                            },
                            "frequency": {
                                "type": "string",
                                "description": "How often, e.g. '3x per week' or 'daily'"
                            },
                            "prescription": {
                                "type": "string",
                                "description": "Exactly what to do each session"
                            },
                            "duration_mins": {
                                "type": "integer",
                                "description": "Estimated minutes per session"
                            }
                        },
                        "required": ["name", "frequency", "prescription", "duration_mins"],
                        "additionalProperties": false
                    }
                },
                "tasks": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "name": {
                                "type": "string",
                                "description": "Name of the task"
                            },
                            "description": {
                                "type": "string",
                                "description": "What needs to be done"
                            },
                            "suggested_days_from_start": {
                                "type": "integer",
                                "description": "Suggested day offset from sprint start"
                            },
                            "duration_mins": {
                                "type": "integer",
                                "description": "Estimated minutes to complete"
                            }
                        },
                        "required": ["name", "description", "suggested_days_from_start", "duration_mins"],
                        "additionalProperties": false
                    }
                }
            },
            "required": ["habits", "tasks"],
            "additionalProperties": false
        }
        """;

    private const string ReplanSchema = """
        {
            "type": "object",
            "properties": {
                "updated_prescriptions": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "habit_id": {
                                "type": "string",
                                "description": "The ID of the habit to update"
                            },
                            "new_prescription": {
                                "type": "string",
                                "description": "The updated prescription for this habit"
                            }
                        },
                        "required": ["habit_id", "new_prescription"],
                        "additionalProperties": false
                    }
                },
                "new_tasks": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "name": {
                                "type": "string",
                                "description": "Name of the task"
                            },
                            "description": {
                                "type": "string",
                                "description": "What needs to be done"
                            },
                            "suggested_days_from_start": {
                                "type": "integer",
                                "description": "Suggested day offset from sprint start"
                            },
                            "duration_mins": {
                                "type": "integer",
                                "description": "Estimated minutes to complete"
                            }
                        },
                        "required": ["name", "description", "suggested_days_from_start", "duration_mins"],
                        "additionalProperties": false
                    }
                },
                "coaching_note": {
                    "type": "string",
                    "description": "Brief motivational note acknowledging progress"
                }
            },
            "required": ["updated_prescriptions", "new_tasks", "coaching_note"],
            "additionalProperties": false
        }
        """;
}
