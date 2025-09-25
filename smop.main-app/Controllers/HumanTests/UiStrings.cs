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
    string TakeABreak { get; }
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
    public string ComparisonInstruction => "Next, you will be presented with pairs of scents. Evaluate whether the scents in the pair are the same or different.";
    public string RatingInstruction => "Next, it's time to evaluate the scents using descriptive words. Remember, you can smell each scent as many times as you like. Carefully go through the list of words that describe the scent and select all the words that you think describe the scent by clicking with your mouse. You can also choose not to select any words. Then, click the 'Done' button.";
    public string OneOutInstruction => "Next, you will be presented with a total of 3 scents. Two of the scents are the same, and one is different. Your task is to determine which of the scents is different from the others.";
    public string ComparisonQuestion => "Were the presented scents the same?";
    public string OneOutQuestion => "Select the box where the scent differed from the others.";
    
    public string[] RatingWords => [
        "sweaty", "sour", "strong",
        "pungent", "musky", "salty",
        "sweet", "fresh", "stinky",
        "smelly", "musty", "clean",
        "fishy", "warm", "rotten",
        "unpleasant", "neutral", "gross",
        "bad", "natural", "stale",
        "sharp", "dirty", "wet",
        "cheesy"
        //"biting", "flowery", "deodorized",
        //"subtle", "damp", "individual", "cold",
        
        // Same in Finnish and English:
        //  "sweaty","pungent", "neutral", "stale", "fresh", "strong", "clean", "unpleasant", "bad", "musty", "dirty", "sour", "salty", "wet", "fishy", "warm", "sweet", 

        // English only: "musky", "stinky", "smelly", "gross", "natural", "sharp", "cheesy"
        
        // Finnish only: "mild", "deodorant", "pleasant", "luscious", "foody", "characteristic", "uriney" 
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
    public string TakeABreak => "Wait. The test will soon continue automatically.";
}


internal class FinnishStrings : IUiStrings
{
    public string ComparisonInstruction => "Seuraavaksi sinulle esitetään tuoksupareja. Arvioi ovatko tuoksuparin tuoksut samat vai erilaiset.";
    public string RatingInstruction => "Seuraavaksi on vuorossa tuoksujen arviointi eri tuoksuja kuvaavilla sanoilla. Muista, että voit haistaa jokaista tuoksua niin monta kertaa kuin haluat. Käy tuoksua kuvaava sanalista huolella läpi ja valitse hiirellä kaikki ne sanat, jotka mielestäsi kuvaavat tuoksua. Voit myös olla valitsematta yhtään sanaa. Paina sitten ”Valmis” painiketta.";
    public string OneOutInstruction => "Seuraavaksi sinulle esitetään yhteensä 3 tuoksua. Esitettävistä tuoksuista kaksi ovat samoja ja yksi on erilainen. Sinun tehtävänäsi on arvioida mikä tuoksuista oli mielestäsi erilainen.";
    public string ComparisonQuestion => "Olivatko esitetyt tuoksut samanlaiset?";
    public string OneOutQuestion => "Valitse laatikko ja numero jonka haju erosi muista.";
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
    public string TakeABreak => "Tauko. Testi jatkuu pian automaattisesti.";
}

internal class GermanStrings : IUiStrings
{
    public string ComparisonInstruction => "Rieche an mehreren Pärchen von Gerüchen wie angewiesen und beantworte die Fragen";
    public string RatingInstruction => "Rieche an einem Geruch so oft wie nötig, wähle die Wörter aus, die den Geruch beschreiben, und drücke dann die „Absenden“-Taste";
    public string OneOutInstruction => "Anschließend werden dir insgesamt drei Düfte präsentiert. Zwei der präsentierten Düfte sind gleich, einer ist anders. Deine Aufgabe ist es, zu beurteilen, welcher der Düfte deiner Meinung nach anders ist.";
    public string ComparisonQuestion => "Sind die präsentierten Gerüche identisch?";
    public string OneOutQuestion => "Wählen Sie einen Duft, der sich von den anderen abhebt.";
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
    public string TakeABreak => "Warten Sie. Der Test wird bald automatisch fortgesetzt.";
}