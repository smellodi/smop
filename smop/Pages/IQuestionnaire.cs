using System.Collections.Generic;

namespace SMOP.Pages
{
    interface IQuestionnaire
    {
        string Header { get; }
        Dictionary<string, string> Entries { get; }
    }
}
