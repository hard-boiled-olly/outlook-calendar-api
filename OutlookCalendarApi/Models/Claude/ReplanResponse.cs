using System.Text.Json.Serialization;

namespace OutlookCalendarApi.Models.Claude;

public record ReplanResponse(
    [property: JsonPropertyName("updated_prescriptions")] List<UpdatedPrescription> UpdatedPrescriptions,
    [property: JsonPropertyName("new_tasks")] List<TaskItem> NewTasks,
    [property: JsonPropertyName("coaching_note")] string CoachingNote
);

public record UpdatedPrescription(
    [property: JsonPropertyName("habit_id")] string HabitId,
    [property: JsonPropertyName("new_prescription")] string NewPrescription
);
