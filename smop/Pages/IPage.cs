using System;

namespace Smop.Pages
{
    internal interface IPage<T>
    {
        event EventHandler<T> Next;
    }
}
