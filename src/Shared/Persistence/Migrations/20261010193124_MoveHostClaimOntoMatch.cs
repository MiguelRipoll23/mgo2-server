using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mgo2Server.Shared.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveHostClaimOntoMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "assigned_at",
                table: "event_matches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "game_id",
                table: "event_matches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "event_matches",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            // Data statement: the one operation here the model cannot express, and
            // it contains no DDL. The room a live match holds is moved off the
            // claim's own table onto the pairing, before the table that held it is
            // dropped by the next migration, so an assignment in progress when
            // this runs is not lost — without it the match would come back naming
            // no room, and the two teams in it would be left waiting for a host
            // they already had.
            //
            // Only the active claims are copied. A released claim names a room the
            // match has already given back, and a room whose match is live carries
            // the assignment's own lock already, which is what marks the rest.
            migrationBuilder.Sql(
                """
                UPDATE event_matches
                SET game_id = lease.game_id, assigned_at = lease.leased_at
                FROM event_host_leases AS lease
                WHERE lease.match_id = event_matches.id
                  AND lease.status = 1
                  AND event_matches.state IN (1, 2)
                """);

            migrationBuilder.CreateIndex(
                name: "IX_event_matches_game_id",
                table: "event_matches",
                column: "game_id",
                unique: true,
                filter: "game_id is not null and state in (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_event_matches_game_id",
                table: "event_matches");

            migrationBuilder.DropColumn(
                name: "assigned_at",
                table: "event_matches");

            migrationBuilder.DropColumn(
                name: "game_id",
                table: "event_matches");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "event_matches");
        }
    }
}
