using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Secretary.Infrastructure.Common.Persistence.Migrations
{
    /// <summary>Appointment calls move from the shared CallOutcome to their own enum.
    ///
    /// Same column, same type — only the meaning of the numbers changes, which is why the
    /// scaffold came out empty and the whole migration is a translation of stored values. Orders
    /// keeps recording CallOutcome and ord.Calls is not touched.
    ///
    /// ⚠ One UPDATE with a CASE, not a statement per value. Six old numbers map onto four new
    /// ones and the two ranges overlap, so a sequence of UPDATEs would convert a row and then
    /// convert it again on a later pass — which is how 0 becomes Resolved becomes Forwarded. A
    /// single CASE evaluates each row once, against the value it had when the statement began.
    ///
    /// NoAnswer(5) has no successor: an inbound line answers by definition, and the member
    /// existed for outbound reminders that were never built. Those rows land on Unfinished, the
    /// closest true statement available — the call reached an end without booking anything.</summary>
    public partial class AppointmentCallOutcome : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
            => migrationBuilder.Sql(@"
                UPDATE app.Calls SET Outcome = CASE Outcome
                    WHEN 0 THEN 1   -- ResolvedByAgent          -> Resolved
                    WHEN 1 THEN 2   -- EscalatedResolvedByStaff -> Forwarded
                    WHEN 2 THEN 2   -- EscalatedAbandoned       -> Forwarded
                    WHEN 3 THEN 4   -- FailedNoAvailability     -> NoAvailability
                    WHEN 4 THEN 3   -- FailedAgentLimitation    -> Unfinished
                    WHEN 5 THEN 3   -- NoAnswer                 -> Unfinished (no successor)
                    ELSE Outcome
                END;");

        /// <summary>⚠ Lossy, and it cannot be otherwise. Two old members collapsed into Forwarded
        /// and two into Unfinished, so coming back has to pick one of each — the resolved
        /// escalation and the agent limitation, being the commoner of each pair. A row that was
        /// EscalatedAbandoned before Up ran does not return as EscalatedAbandoned.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
            => migrationBuilder.Sql(@"
                UPDATE app.Calls SET Outcome = CASE Outcome
                    WHEN 1 THEN 0   -- Resolved       -> ResolvedByAgent
                    WHEN 2 THEN 1   -- Forwarded      -> EscalatedResolvedByStaff
                    WHEN 3 THEN 4   -- Unfinished     -> FailedAgentLimitation
                    WHEN 4 THEN 3   -- NoAvailability -> FailedNoAvailability
                    ELSE Outcome
                END;");
    }
}
