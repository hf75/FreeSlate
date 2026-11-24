namespace FreeSlate.Docx.Citations.Styles;

/// <summary>What the styles share: the language a field asks for, people's names as styles write them.</summary>
static class Common
{
    /// <summary>Whether a language id (\l) is German (1031 Germany, 2055 Switzerland, 3079 Austria, 4103 Luxembourg, 5127 Liechtenstein).</summary>
    public static bool German(int lcid) => (lcid & 0x3FF) == 0x07;

    /// <summary>A person's initials as Word writes them ("John" "J.", "Anna" "B." "A. B."), first and middle names in turn.</summary>
    public static string Initials(Person person) =>
        string.Join(" ", new[] { person.First, person.Middle }.Where(n => !string.IsNullOrWhiteSpace(n))
            .SelectMany(n => n!.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Select(n => n.EndsWith('.') ? n : n[..1] + "."));

    /// <summary>A person's first and middle names as written ("Anna B.").</summary>
    public static string Given(Person person) => string.Join(" ", new[] { person.First, person.Middle }.Where(n => !string.IsNullOrWhiteSpace(n)));
}
