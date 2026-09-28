using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Mgo2Server.Shared.Persistence.Entities;

/// <summary>
/// A staff role an account holds. An account with no row here is a player, and
/// an account may hold several roles at once rather than exactly one, so
/// promoting somebody and then narrowing what they may do are two separate
/// writes instead of one overwriting value.
/// <para>
/// The row is the whole of the staff model. It was a single integer column on the
/// account before, which could only ever say "one of these", and every reader then
/// had to agree on what each number meant — a manager and a moderator were told
/// apart by a comparison nobody had written down. Naming the roles in their own
/// table is what lets a dashboard ask "is this a moderator" and be answered.
/// </para>
/// </summary>
[Table("account_roles")]
[PrimaryKey(nameof(AccountIdentifier), nameof(Role))]
public sealed class AccountRole
{
    /// <summary>Account the role was granted to.</summary>
    [Column("account_id")]
    public int AccountIdentifier { get; set; }

    /// <summary>
    /// Name of the role, which is what a reader matches on: <c>moderator</c> or
    /// <c>manager</c>. A name rather than an ordinal, so a row stays readable and
    /// a new role can be added without renumbering the ones already granted.
    /// </summary>
    [Column("role")]
    [MaxLength(32)]
    public required string Role { get; set; }

    /// <summary>Timestamp the role was granted at.</summary>
    [Column("granted_at")]
    public DateTimeOffset GrantedAt { get; set; }

    /// <summary>Account the role was granted to.</summary>
    [ForeignKey(nameof(AccountIdentifier))]
    public Account? Account { get; set; }
}
