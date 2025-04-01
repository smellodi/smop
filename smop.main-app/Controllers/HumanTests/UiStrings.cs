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
    string RatingInstruction { get; }
    string OneOutInstruction { get; }
    string ComparisonQuestion { get; }
    string OneOutQuestion { get; }
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
    string Done { get; }
    string ContinueWhenReady { get; }
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
    public string RatingInstruction => "Sniff an odor as many times as needed, select the words that describe the odor and then press “Submit” button";
    public string OneOutInstruction => "Next, you will be presented with a total of 3 scents. Two of the scents presented are the same and one is different. Your task is to judge which of the scents you think was different.";
    public string ComparisonQuestion => "Are the presented odors identical?";
    public string OneOutQuestion => "Choose a scent that stands out from the rest";
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
    public string Done => "Done!";
    public string ContinueWhenReady => "Take a break if needed, then continue the test";
}


internal class FinnishStrings : IUiStrings
{
    public string ComparisonInstruction => "Seuraavaksi sinulle esitetään tuoksupareja. Arvioi ovatko tuoksuparin tuoksut samat vai erilaiset.";
    public string RatingInstruction => "Seuraavaksi on vuorossa tuoksujen arviointi eri tuoksuja kuvaavilla sanoilla. Muista, että voit haistaa jokaista tuoksua niin monta kertaa kuin haluat. Käy tuoksua kuvaava sanalista huolella läpi ja valitse hiirellä kaikki ne sanat, jotka mielestäsi kuvaavat tuoksua. Voit myös olla valitsematta yhtään sanaa. Paina sitten ”Valmis” painiketta";
    public string OneOutInstruction => "Seuraavaksi sinulle esitetään yhteensä 3 tuoksua. Esitettävistä tuoksuista kaksi ovat samoja ja yksi on erilainen. Sinun tehtävänäsi on arvioida mikä tuoksuista oli mielestäsi erilainen.";
    public string ComparisonQuestion => "Olivatko esitetyt tuoksut samanlaiset?";
    public string OneOutQuestion => "Valitse laatikko ja numero jonka haju erosi muista";
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
    public string ReleaseOdor => "Käynnistä tuoksu";
    public string Continue => "Jatka";
    public string Submit => "Valmis";
    public string Wait => "Odota";
    public string SelectWords => "Valitse sopivat kuvaukset";
    public string OdorReady => "Tuoksu on valmis käynnistettäväksi";
    public string Sniff => "Haista";
    public string Odor => "Haju";
    public string Done => "Valmis!";
    public string ContinueWhenReady => "Pidä tauko, jos tarvitset, ja jatka sitten testiä";
}

internal class GermanStrings : IUiStrings
{
    public string ComparisonInstruction => "Rieche an mehreren Pärchen von Gerüchen wie angewiesen und beantworte die Fragen";
    public string RatingInstruction => "Rieche an einem Geruch so oft wie nötig, wähle die Wörter aus, die den Geruch beschreiben, und drücke dann die „Absenden“-Taste";
    public string OneOutInstruction => "Anschließend werden dir insgesamt drei Düfte präsentiert. Zwei der präsentierten Düfte sind gleich, einer ist anders. Deine Aufgabe ist es, zu beurteilen, welcher der Düfte deiner Meinung nach anders ist.";
    public string ComparisonQuestion => "Sind die präsentierten Gerüche identisch?";
    public string OneOutQuestion => "Wählen Sie einen Duft, der sich von den anderen abhebt";
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
    public string Done => "Done!";
    public string ContinueWhenReady => "Mach eine Pause, wenn du brauchst, und setze dann den Test fort";
}