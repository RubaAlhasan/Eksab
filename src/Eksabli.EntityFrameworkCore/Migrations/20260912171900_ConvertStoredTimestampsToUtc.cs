using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eksabli.Migrations
{
    /// <inheritdoc />
    public partial class ConvertStoredTimestampsToUtc : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Pairs with <c>AbpClockOptions.Kind = DateTimeKind.Utc</c> in EksabliDomainModule. Neither is
        /// correct alone: flipping the clock without this leaves every existing row reading as UTC when
        /// it holds local time, and running this without the clock change means new rows immediately
        /// drift back out of step.
        ///
        /// Every <c>timestamp without time zone</c> column in the schema was written from
        /// <c>DateTime.Now</c> on a machine in Syria Standard Time, so each one is shifted by the zone's
        /// real offset. <c>AT TIME ZONE 'Asia/Damascus'</c> applies the historical DST rules per row
        /// rather than a flat three hours — Syria observed DST until 2022, so a fixed shift would be
        /// wrong for anything written before that, and the operation must remain correct if this is
        /// ever replayed against an older database.
        ///
        /// Discovered from information_schema rather than listed by hand: the count is 120 across
        /// application tables, ABP's own (audit logs, sessions, permissions) and OpenIddict's. They
        /// were all written by the same clock, so they all move together. Excludes ABP's migration
        /// history table, whose stamps are bookkeeping rather than domain data.
        ///
        /// Access tokens and refresh tokens move with everything else, so any token issued before this
        /// runs effectively ages by the offset — sessions open at deploy time will need a fresh login.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ShiftAllTimestamps(from: "Asia/Damascus", to: "UTC"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(ShiftAllTimestamps(from: "UTC", to: "Asia/Damascus"));
        }

        /// Reinterprets every naive timestamp as being in <paramref name="from"/> and rewrites it in
        /// <paramref name="to"/>. Both arguments are fixed literals from this file, never user input.
        private static string ShiftAllTimestamps(string from, string to) => $@"
            DO $$
            DECLARE
                target record;
            BEGIN
                FOR target IN
                    SELECT c.table_name, c.column_name
                    FROM   information_schema.columns c
                    JOIN   information_schema.tables t
                           ON t.table_schema = c.table_schema
                          AND t.table_name = c.table_name
                    WHERE  c.table_schema = 'public'
                      AND  c.data_type = 'timestamp without time zone'
                      AND  t.table_type = 'BASE TABLE'
                      AND  c.table_name <> '__EFMigrationsHistory'
                LOOP
                    EXECUTE format(
                        'UPDATE public.%I SET %I = (%I AT TIME ZONE %L) AT TIME ZONE %L WHERE %I IS NOT NULL',
                        target.table_name,
                        target.column_name,
                        target.column_name,
                        '{from}',
                        '{to}',
                        target.column_name);
                END LOOP;
            END $$;";
    }
}
