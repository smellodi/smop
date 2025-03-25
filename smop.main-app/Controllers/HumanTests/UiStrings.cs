using System;

namespace Smop.MainApp.Controllers.HumanTests;

public enum Language
{
    Finnish,
    English,
    German,
}

public interface IUiStrings
{
    string ComparisonInstruction { get; }
    string ComparisonQuestion { get; }
    string RatingInstruction { get; }
    string[] RatingWords { get; }
    string Yes { get; }
    string No { get; }
    string ReleaseOdor { get; }
    string Continue { get; }
    string Submit { get; }
    string Wait { get; }
    string SelectWords { get; }
    string OdorReady { get; }
    string Sniff { get; }
    string Odor { get; }
}

internal static class UiStrings
{
    public static IUiStrings Get(Language lang) => lang switch
    {
        Language.English => new EnglishStrings(),
        Language.Finnish => new FinnishStrings(),
        Language.German => new GermanStrings(),
        _ => throw new NotImplementedException($"The language '{lang}' is not yet supported.")
    };
}

internal class EnglishStrings : IUiStrings
{
    public string ComparisonInstruction => "Sniff several pairs of odors as instructed and answer the questions";
    public string ComparisonQuestion => "Are the presented odors identical?";
    public string RatingInstruction => "Sniff an odor as many times as needed, select the words that describe the odor and then press “Submit” button";
    public string[] RatingWords => [
        "sweaty", "sour", "strong",
        "pungent", "musky", "salty",
        "sweet", "fresh", "stinky",
        "smelly", "musty", "clean",
        "fishy", "warm", "foul",
        "unpleasant", "neutral", "gross",
        "bad", "natural", "stale",
        "sharp", "dirty", "wet",
        "cheesy"
        //"biting", "flowery", "deodorized",
        //"subtle", "damp", "individual", "cold",
    ];
    public string Yes => "Yes";
    public string No => "No";
    public string ReleaseOdor => "Release the odor";
    public string Continue => "Continue";
    public string Submit => "Submit";
    public string Wait => "Wait";
    public string SelectWords => "Select the matching descriptions";
    public string OdorReady => "Ready to release the odor";
    public string Sniff => "Sniff";
    public string Odor => "Odor";
}


internal class FinnishStrings : IUiStrings
{
    public string ComparisonInstruction => "Haista useita hajuparia ohjeiden mukaan ja vastaa kysymyksiin";
    public string ComparisonQuestion => "Ovatko esitetyt hajut identtisiä?";
    public string RatingInstruction => "Haista hajua niin monta kertaa kuin tarvitset, valitse sanat, jotka kuvaavat hajua ja paina sitten ”Lähetä” painiketta";
    public string[] RatingWords => [
        "hikinen", "pistävä", "neutraali",
        "tunkkainen", "raikas", "voimakas",
        "puhdas", "epämiellyttävä", "mieto",
        "paha", "ummehtunut", "likainen",
        "makea", "hapan", "suolainen",
        "deodoranttinen", "miellyttävä", "imelä",
        "mätä", "ruokainen", "ominaistuoksuinen",
        "kostea", "kalamainen", "lämmin",
        "virtsainen"
    ];
    public string Yes => "Kyllä";
    public string No => "Ei";
    public string ReleaseOdor => "Päästä haju vapaaksi";
    public string Continue => "Jatka";
    public string Submit => "Lähetä";
    public string Wait => "Odota";
    public string SelectWords => "Valitse sopivat kuvaukset";
    public string OdorReady => "Valmis päästämään hajun vapaaksi";
    public string Sniff => "Haista";
    public string Odor => "Haju";
}

internal class GermanStrings : IUiStrings
{
    public string ComparisonInstruction => "Rieche an mehreren Pärchen von Gerüchen wie angewiesen und beantworte die Fragen";
    public string ComparisonQuestion => "Sind die präsentierten Gerüche identisch?";
    public string RatingInstruction => "Rieche an einem Geruch so oft wie nötig, wähle die Wörter aus, die den Geruch beschreiben, und drücke dann die „Absenden“-Taste";
    public string[] RatingWords => [
        "schweißig", "sauer", "NEG-angenehm",
        "neutral", "intensiv", "stinkend",
        "käsig", "süß", "frisch",
        "angenehm", "muffig", "faulig",
        "stechend", "beißend", "salzig",
        "stark", "fischig", "streng",
        "warm", "feucht", "eklig",
        "herb", "urin", "deodoriert",
        "abgestanden"
    ];
    public string Yes => "Ja";
    public string No => "Nein";
    public string ReleaseOdor => "Lass den Geruch freisetzen";
    public string Continue => "Fortfahren";
    public string Submit => "Absenden";
    public string Wait => "Warten";
    public string SelectWords => "Wähle die passenden Beschreibungen aus";
    public string OdorReady => "Bereit, den Geruch freizusetzen";
    public string Sniff => "Riechen";
    public string Odor => "Geruch";
}