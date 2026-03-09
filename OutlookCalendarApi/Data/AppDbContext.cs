using Microsoft.EntityFrameworkCore;
using OutlookCalendarApi.Models.Domain;

namespace OutlookCalendarApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Identity> Identities => Set<Identity>();
    public DbSet<Summit> Summits => Set<Summit>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Sprint> Sprints => Set<Sprint>();
    public DbSet<Habit> Habits => Set<Habit>();
    public DbSet<HabitPrescription> HabitPrescriptions => Set<HabitPrescription>();
    public DbSet<HabitEvent> HabitEvents => Set<HabitEvent>();
    public DbSet<SprintTask> SprintTasks => Set<SprintTask>();
    public DbSet<InterviewSession> InterviewSessions => Set<InterviewSession>();
    public DbSet<SchedulingPreferences> SchedulingPreferences => Set<SchedulingPreferences>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(u => u.Id).HasColumnName("id");
            e.Property(u => u.DisplayName).HasColumnName("display_name");
            e.Property(u => u.Email).HasColumnName("email");
            e.Property(u => u.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Identity>(e =>
        {
            e.ToTable("identities");
            e.Property(i => i.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(i => i.UserId).HasColumnName("user_id");
            e.Property(i => i.Statement).HasColumnName("statement");
            e.Property(i => i.Active).HasColumnName("active");
            e.Property(i => i.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(i => i.DeletedAt).HasColumnName("deleted_at");

            e.HasOne(i => i.User).WithMany(u => u.Identities).HasForeignKey(i => i.UserId);
        });

        modelBuilder.Entity<Summit>(e =>
        {
            e.ToTable("summits");
            e.Property(s => s.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(s => s.IdentityId).HasColumnName("identity_id");
            e.Property(s => s.Description).HasColumnName("description");
            e.Property(s => s.ProofCriteria).HasColumnName("proof_criteria");
            e.Property(s => s.TargetDate).HasColumnName("target_date");
            e.Property(s => s.Status).HasColumnName("status");
            e.Property(s => s.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasOne(s => s.Identity).WithMany(i => i.Summits).HasForeignKey(s => s.IdentityId);
        });

        modelBuilder.Entity<Milestone>(e =>
        {
            e.ToTable("milestones");
            e.Property(m => m.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(m => m.SummitId).HasColumnName("summit_id");
            e.Property(m => m.Description).HasColumnName("description");
            e.Property(m => m.ProofCriteria).HasColumnName("proof_criteria");
            e.Property(m => m.TargetDate).HasColumnName("target_date");
            e.Property(m => m.SortOrder).HasColumnName("sort_order");
            e.Property(m => m.Status).HasColumnName("status");
            e.Property(m => m.ProvedAt).HasColumnName("proved_at");
            e.Property(m => m.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasOne(m => m.Summit).WithMany(s => s.Milestones).HasForeignKey(m => m.SummitId);
        });

        modelBuilder.Entity<Sprint>(e =>
        {
            e.ToTable("sprints");
            e.Property(s => s.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(s => s.MilestoneId).HasColumnName("milestone_id");
            e.Property(s => s.SprintNumber).HasColumnName("sprint_number");
            e.Property(s => s.StartedAt).HasColumnName("started_at");
            e.Property(s => s.EndedAt).HasColumnName("ended_at");
            e.Property(s => s.Reflection).HasColumnName("reflection");
            e.Property(s => s.Status).HasColumnName("status");

            e.HasOne(s => s.Milestone).WithMany(m => m.Sprints).HasForeignKey(s => s.MilestoneId);
        });

        modelBuilder.Entity<Habit>(e =>
        {
            e.ToTable("habits");
            e.Property(h => h.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(h => h.IdentityId).HasColumnName("identity_id");
            e.Property(h => h.Name).HasColumnName("name");
            e.Property(h => h.Frequency).HasColumnName("frequency");
            e.Property(h => h.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasOne(h => h.Identity).WithMany(i => i.Habits).HasForeignKey(h => h.IdentityId);
        });

        modelBuilder.Entity<HabitPrescription>(e =>
        {
            e.ToTable("habit_prescriptions");
            e.Property(hp => hp.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(hp => hp.HabitId).HasColumnName("habit_id");
            e.Property(hp => hp.SprintId).HasColumnName("sprint_id");
            e.Property(hp => hp.Prescription).HasColumnName("prescription");
            e.Property(hp => hp.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");

            e.HasOne(hp => hp.Habit).WithMany(h => h.HabitPrescriptions).HasForeignKey(hp => hp.HabitId);
            e.HasOne(hp => hp.Sprint).WithMany(s => s.HabitPrescriptions).HasForeignKey(hp => hp.SprintId);
        });

        modelBuilder.Entity<HabitEvent>(e =>
        {
            e.ToTable("habit_events");
            e.Property(he => he.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(he => he.HabitPrescriptionId).HasColumnName("habit_prescription_id");
            e.Property(he => he.SprintId).HasColumnName("sprint_id");
            e.Property(he => he.ScheduledDate).HasColumnName("scheduled_date");
            e.Property(he => he.ScheduledTime).HasColumnName("scheduled_time");
            e.Property(he => he.DurationMins).HasColumnName("duration_mins");
            e.Property(he => he.CalendarEventId).HasColumnName("calendar_event_id");
            e.Property(he => he.Status).HasColumnName("status");

            e.HasOne(he => he.HabitPrescription).WithMany(hp => hp.HabitEvents).HasForeignKey(he => he.HabitPrescriptionId);
            e.HasOne(he => he.Sprint).WithMany(s => s.HabitEvents).HasForeignKey(he => he.SprintId);
        });

        modelBuilder.Entity<SprintTask>(e =>
        {
            e.ToTable("sprint_tasks");
            e.Property(st => st.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(st => st.SprintId).HasColumnName("sprint_id");
            e.Property(st => st.Name).HasColumnName("name");
            e.Property(st => st.Description).HasColumnName("description");
            e.Property(st => st.Deadline).HasColumnName("deadline");
            e.Property(st => st.DurationMins).HasColumnName("duration_mins");
            e.Property(st => st.CalendarEventId).HasColumnName("calendar_event_id");
            e.Property(st => st.Status).HasColumnName("status");

            e.HasOne(st => st.Sprint).WithMany(s => s.SprintTasks).HasForeignKey(st => st.SprintId);
        });

        modelBuilder.Entity<InterviewSession>(e =>
        {
            e.ToTable("interview_sessions");
            e.Property(s => s.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(s => s.Type).HasColumnName("type");
            e.Property(s => s.IdentityId).HasColumnName("identity_id");
            e.Property(s => s.UserId).HasColumnName("user_id");
            e.Property(s => s.CurrentStep).HasColumnName("current_step");
            e.Property(s => s.ConversationHistory).HasColumnName("conversation_history").HasColumnType("jsonb");
            e.Property(s => s.AccumulatedData).HasColumnName("accumulated_data").HasColumnType("jsonb");
            e.Property(s => s.Active).HasColumnName("active");
            e.Property(s => s.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(s => s.UpdatedAt).HasColumnName("updated_at");
            e.Property(s => s.ExpiresAt).HasColumnName("expires_at");
            e.Property(s => s.DeletedAt).HasColumnName("deleted_at");

            e.HasOne(s => s.Identity).WithMany().HasForeignKey(s => s.IdentityId);
            e.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId);
        });

        modelBuilder.Entity<SchedulingPreferences>(e =>
        {
            e.ToTable("scheduling_preferences");
            e.Property(sp => sp.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(sp => sp.UserId).HasColumnName("user_id");
            e.Property(sp => sp.WorkingHoursStart).HasColumnName("working_hours_start");
            e.Property(sp => sp.WorkingHoursEnd).HasColumnName("working_hours_end");
            e.Property(sp => sp.PreferredTimes).HasColumnName("preferred_times").HasColumnType("jsonb");
            e.Property(sp => sp.TimeZone).HasColumnName("time_zone");
            e.Property(sp => sp.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            e.Property(sp => sp.UpdatedAt).HasColumnName("updated_at");

            e.HasOne(sp => sp.User).WithOne().HasForeignKey<SchedulingPreferences>(sp => sp.UserId);
        });
    }
}
