using Smop.IonVision;

var ionVision = new Communicator();

var projects = await ionVision.ListPosts();

Console.WriteLine("Projects:");
int i = 1;
foreach (var project in projects)
{
    Console.WriteLine($"{i++}:\n" + AsDict(project));
}

string AsDict(object obj)
{
    return string.Join("\n", obj.GetType().GetProperties().Select(p => $"  {p.Name} = {p.GetValue(obj)}"));
}