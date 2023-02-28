using System;

namespace SMOP.Pages
{
    internal interface IPage<T>
    {
        event EventHandler<T> Next;
    }
}
