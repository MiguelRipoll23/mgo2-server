namespace Mgo2Server.Shared.Domain.Instructors;

/// <summary>
/// Interprets the answer byte a student sends at the end of a combat training
/// session.
/// <para>
/// The byte packs two facts: bit 0 is the answer to the save-instructor prompt, and
/// bit 5 says whether the prompt was shown at all. All three observed values are
/// named here — <c>0x00</c> prompt shown and declined, <c>0x01</c> shown and
/// accepted, <c>0x21</c> never shown.
/// </para>
/// <para>
/// Only an exact <c>0x01</c> is a recognition, and the tempting shorthand
/// <c>(answer &amp; 0x20) == 0</c> is <b>wrong</b>: it reads a declined prompt as a
/// recognition, which would permanently record an instructor the player refused to
/// name. That case is not hypothetical — it was the first "no" sample.
/// </para>
/// </summary>
public static class InstructorGraduationUtils
{
    /// <summary>The save-instructor prompt was shown and the student answered no.</summary>
    public const int DeclinedAnswer = 0x00;

    /// <summary>The prompt was shown and the student answered yes.</summary>
    public const int RecognisedAnswer = 0x01;

    /// <summary>The prompt was never shown, because the client already had a saved instructor.</summary>
    public const int NotAskedAnswer = 0x21;

    /// <summary>Stand-in when the answer byte is absent, distinct from every observed value.</summary>
    public const int NoAnswer = -1;

    /// <summary>Whether the student recognised the instructor they were trained by.</summary>
    /// <param name="answer">Raw answer byte, or <see cref="NoAnswer"/> when absent.</param>
    public static bool IsRecognised(int answer) => answer == RecognisedAnswer;
}
