using System.Collections.Generic;

namespace Smop.Pages
{
    interface IQuestionnaire
    {
        string Header { get; }
        Dictionary<string, string> Entries { get; }
    }
}
