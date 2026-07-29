namespace BC_CampusLearn.Services.Tutors;

public static class TutorDisplayNames
{
    private static readonly string[] Names =
    {
        "Michelle Duma",
        "Mishakaylin Diniso",
        "Naledi Mogadingoane",
        "Karabo Mosethe",
        "Mosa Msiza",
        "Thembi Sefini",
        "Karabelo Mokhubu",
        "Keleabetswe Molefe"
    };

    public static string GetName(int tutorId)
    {
        int nameIndex = Math.Max(tutorId - 1, 0);
        return Names[nameIndex % Names.Length];
    }
}
